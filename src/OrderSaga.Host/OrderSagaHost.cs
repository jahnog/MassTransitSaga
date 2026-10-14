using Confluent.Kafka;
using MassTransit;
using Microsoft.Azure.Cosmos;
using OrderSaga.Contracts;
using OrderSaga.Host.Cosmos;
using OrderSaga.Host.Kafka;
using OrderSaga.Host.Orders;
using OrderSaga.Host.Saga;

namespace OrderSaga.Host;

public static class OrderSagaHost
{
    public const string SagaGroup = "order-saga";
    public const string PageGroup = "order-page";

    public static async Task<WebApplication> Build(string[] args, string? listenUrl = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        // UseUrls writes a host setting that overrides ASPNETCORE_URLS.
        // Callers and the workstation unit keep port 5080. Deploy sets ASPNETCORE_URLS.
        if (listenUrl is not null)
            builder.WebHost.UseUrls(listenUrl);
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
            builder.WebHost.UseUrls("http://localhost:5080");

        var cosmos = builder.Configuration.GetSection("Cosmos").Get<CosmosOptions>()
            ?? throw new InvalidOperationException("Cosmos settings are missing.");
        cosmos.EnsureValid();
        var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaOptions>()
            ?? throw new InvalidOperationException("Kafka settings are missing.");
        kafka.EnsureValid();

        builder.Services.AddOptions<CosmosClientOptions>()
            .Configure(options => EmulatorCosmos.Apply(options, cosmos.Endpoint));

        var emulator = IsEmulator(cosmos.Endpoint);
        builder.Services.AddMassTransit(bus =>
        {
            bus.AddRider(rider =>
            {
                rider.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .CosmosRepository(repository =>
                    {
                        if (emulator)
                            repository.ConfigureEmulator();
                        else
                        {
                            repository.AccountEndpoint = cosmos.Endpoint;
                            repository.AuthKeyOrResourceToken = cosmos.Key;
                        }

                        repository.DatabaseId = cosmos.DatabaseId;
                        repository.CollectionId = cosmos.ContainerId;
                    });

                Register<OrderSubmitted>(rider, OrderTopics.OrderSubmitted);
                Register<PaymentReceived>(rider, OrderTopics.PaymentReceived);
                Register<OrderShipped>(rider, OrderTopics.OrderShipped);
                Register<OrderDelivered>(rider, OrderTopics.OrderDelivered);
                Register<OrderCancelled>(rider, OrderTopics.OrderCancelled);
                Register<OrderAccepted>(rider, OrderTopics.OrderAccepted);
                Register<PaymentRecorded>(rider, OrderTopics.PaymentRecorded);
                Register<ShipmentRecorded>(rider, OrderTopics.ShipmentRecorded);
                Register<OrderCompleted>(rider, OrderTopics.OrderCompleted);
                Register<OrderCancellationRecorded>(rider, OrderTopics.OrderCancellationRecorded);

                rider.UsingKafka((context, configurator) =>
                {
                    configurator.Host(kafka.BootstrapServers);

                    void Bind<T>(string topic, bool saga)
                        where T : class, ICorrelatedOrderEvent
                    {
                        configurator.TopicEndpoint<string, T>(topic, saga ? SagaGroup : PageGroup, endpoint =>
                        {
                            endpoint.AutoOffsetReset = AutoOffsetReset.Earliest;
                            endpoint.ConcurrentDeliveryLimit = 1;
                            endpoint.CreateIfMissing(options =>
                            {
                                options.NumPartitions = 1;
                                options.ReplicationFactor = 1;
                            });

                            if (saga)
                            {
                                endpoint.UseMessageRetry(retry =>
                                {
                                    retry.Handle<CosmosConcurrencyException>();
                                    retry.Interval(5, TimeSpan.FromMilliseconds(200));
                                });
                                endpoint.ConfigureSaga<OrderState>(context);
                            }
                            else
                            {
                                endpoint.ConfigureConsumer<OrderEventLogConsumer<T>>(context);
                            }
                        });
                    }

                    Action[] endpoints =
                    [
                        () => Bind<OrderSubmitted>(OrderTopics.OrderSubmitted, true),
                        () => Bind<PaymentReceived>(OrderTopics.PaymentReceived, true),
                        () => Bind<OrderShipped>(OrderTopics.OrderShipped, true),
                        () => Bind<OrderDelivered>(OrderTopics.OrderDelivered, true),
                        () => Bind<OrderCancelled>(OrderTopics.OrderCancelled, true),
                        () => Bind<OrderSubmitted>(OrderTopics.OrderSubmitted, false),
                        () => Bind<PaymentReceived>(OrderTopics.PaymentReceived, false),
                        () => Bind<OrderShipped>(OrderTopics.OrderShipped, false),
                        () => Bind<OrderDelivered>(OrderTopics.OrderDelivered, false),
                        () => Bind<OrderCancelled>(OrderTopics.OrderCancelled, false),
                        () => Bind<OrderAccepted>(OrderTopics.OrderAccepted, false),
                        () => Bind<PaymentRecorded>(OrderTopics.PaymentRecorded, false),
                        () => Bind<ShipmentRecorded>(OrderTopics.ShipmentRecorded, false),
                        () => Bind<OrderCompleted>(OrderTopics.OrderCompleted, false),
                        () => Bind<OrderCancellationRecorded>(OrderTopics.OrderCancellationRecorded, false),
                    ];

                    foreach (var endpoint in endpoints)
                        endpoint();
                });
            });

            // The in-memory bus is not the transport. The rider requires a bus, and this one has no receive endpoints.
            bus.UsingInMemory((_, _) => { });
        });

        builder.Services.AddSingleton<OrderEventLog>();
        builder.Services.AddSingleton<OrderCommandSender>();
        builder.Services.AddSingleton<OrderFactProducer>();
        builder.Services.AddSingleton(provider =>
        {
            var clients = provider.GetRequiredService<ICosmosClientFactory>();
            var client = clients.GetCosmosClient<OrderState>("order-saga");
            return client.GetContainer(cosmos.DatabaseId, cosmos.ContainerId);
        });
        builder.Services.AddSingleton<OrderStateReader>();
        builder.Services.AddHostedService<PendingFactDispatcher>();

        var app = builder.Build();
        await EnsureContainer(cosmos);
        app.UseDefaultFiles();
        app.UseStaticFiles();
        OrderApi.Map(app);
        return app;
    }

    private static void Register<T>(IRiderRegistrationConfigurator rider, string topic)
        where T : class, ICorrelatedOrderEvent
    {
        rider.AddProducer<string, T>(topic);
        rider.AddConsumer<OrderEventLogConsumer<T>>();
    }

    private static bool IsEmulator(string endpoint)
    {
        return string.Equals(
            endpoint.TrimEnd('/'),
            AzureCosmosEmulatorConstants.AccountEndpoint.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureContainer(CosmosOptions cosmos)
    {
        var options = new CosmosClientOptions();
        EmulatorCosmos.Apply(options, cosmos.Endpoint);
        using var client = new CosmosClient(cosmos.Endpoint, cosmos.Key, options);
        var database = await client.CreateDatabaseIfNotExistsAsync(cosmos.DatabaseId);
        await database.Database.CreateContainerIfNotExistsAsync(cosmos.ContainerId, "/id");
    }
}
