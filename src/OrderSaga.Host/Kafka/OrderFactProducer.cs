using MassTransit;
using OrderSaga.Contracts;
using OrderSaga.Host.Saga;

namespace OrderSaga.Host.Kafka;

public sealed class OrderFactProducer
{
    private readonly IServiceScopeFactory _scopes;

    public OrderFactProducer(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public async Task<bool> TryProduce(Guid correlationId, PendingFact fact, CancellationToken cancellationToken)
    {
        var key = OrderMessageKey.For(correlationId);
        await using var scope = _scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        switch (fact.FactName)
        {
            case nameof(OrderAccepted):
                await Produce(provider.GetRequiredService<ITopicProducer<string, OrderAccepted>>(), key, new OrderAccepted(correlationId), fact.FactId, cancellationToken);
                return true;
            case nameof(PaymentRecorded):
                await Produce(provider.GetRequiredService<ITopicProducer<string, PaymentRecorded>>(), key, new PaymentRecorded(correlationId), fact.FactId, cancellationToken);
                return true;
            case nameof(ShipmentRecorded):
                await Produce(provider.GetRequiredService<ITopicProducer<string, ShipmentRecorded>>(), key, new ShipmentRecorded(correlationId), fact.FactId, cancellationToken);
                return true;
            case nameof(OrderCompleted):
                await Produce(provider.GetRequiredService<ITopicProducer<string, OrderCompleted>>(), key, new OrderCompleted(correlationId), fact.FactId, cancellationToken);
                return true;
            case nameof(OrderCancellationRecorded):
                await Produce(provider.GetRequiredService<ITopicProducer<string, OrderCancellationRecorded>>(), key, new OrderCancellationRecorded(correlationId), fact.FactId, cancellationToken);
                return true;
            default:
                return false;
        }
    }

    private static Task Produce<T>(ITopicProducer<string, T> producer, string key, T message, Guid factId, CancellationToken cancellationToken)
        where T : class
    {
        return producer.Produce(
            key,
            message,
            Pipe.Execute<KafkaSendContext<string, T>>(context => context.MessageId = factId),
            cancellationToken);
    }
}
