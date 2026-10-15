using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using OrderSaga.Contracts;
using OrderSaga.Host.Kafka;
using OrderSaga.Host.Saga;

namespace OrderSaga.Tests;

public sealed class OrderFactProducerTests
{
    [Test]
    public Task TryProduce_publishes_an_accepted_order()
        => Publishes<OrderAccepted>(nameof(OrderAccepted));

    [Test]
    public Task TryProduce_publishes_a_recorded_payment()
        => Publishes<PaymentRecorded>(nameof(PaymentRecorded));

    [Test]
    public Task TryProduce_publishes_a_recorded_shipment()
        => Publishes<ShipmentRecorded>(nameof(ShipmentRecorded));

    [Test]
    public Task TryProduce_publishes_a_completed_order()
        => Publishes<OrderCompleted>(nameof(OrderCompleted));

    [Test]
    public Task TryProduce_publishes_a_recorded_cancellation()
        => Publishes<OrderCancellationRecorded>(nameof(OrderCancellationRecorded));

    [Test]
    public async Task TryProduce_does_not_publish_an_unknown_fact()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var producer = new OrderFactProducer(provider.GetRequiredService<IServiceScopeFactory>());

        var produced = await producer.TryProduce(
            Guid.NewGuid(),
            new PendingFact(Guid.NewGuid(), "Unknown"),
            CancellationToken.None);

        Assert.That(produced, Is.False);
    }

    private static async Task Publishes<TFact>(string factName)
        where TFact : class, ICorrelatedOrderEvent
    {
        var correlationId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var factId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var (topicProducer, recorded) = RecordingProducer<TFact>.Create();
        var services = new ServiceCollection();
        services.AddSingleton(topicProducer);
        await using var provider = services.BuildServiceProvider();
        var producer = new OrderFactProducer(provider.GetRequiredService<IServiceScopeFactory>());

        var produced = await producer.TryProduce(correlationId, new PendingFact(factId, factName), CancellationToken.None);

        Assert.That(produced, Is.True);
        Assert.That(recorded.Calls, Has.Count.EqualTo(1));
        Assert.That(recorded.Calls[0].Key, Is.EqualTo("01234567-89ab-cdef-0123-456789abcdef"));
        Assert.That(recorded.Calls[0].Message, Is.TypeOf<TFact>());
        Assert.That(recorded.Calls[0].Message.CorrelationId, Is.EqualTo(correlationId));
        Assert.That(recorded.Calls[0].MessageId, Is.EqualTo(factId));
    }

    private class RecordingProducer<T> : DispatchProxy
        where T : class, ICorrelatedOrderEvent
    {
        public List<(string Key, T Message, Guid? MessageId)> Calls { get; } = [];

        public static (ITopicProducer<string, T> Producer, RecordingProducer<T> Recorded) Create()
        {
            var proxy = DispatchProxy.Create<ITopicProducer<string, T>, RecordingProducer<T>>();
            return (proxy, (RecordingProducer<T>)(object)proxy);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 4 } && args[1] is T message && args[2] is not null)
            {
                var contextProxy = DispatchProxy.Create<KafkaSendContext<string, T>, RecordingContext<T>>();
                var context = (RecordingContext<T>)(object)contextProxy;
                var pipe = (IPipe<KafkaSendContext<string, T>>)args[2]!;
                pipe.Send(contextProxy).GetAwaiter().GetResult();
                Calls.Add(((string)args[0]!, message, context.MessageId));
            }

            return Task.CompletedTask;
        }
    }

    private class RecordingContext<T> : DispatchProxy
        where T : class
    {
        public Guid? MessageId { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "set_MessageId")
                MessageId = (Guid?)args?[0];

            return null;
        }
    }
}
