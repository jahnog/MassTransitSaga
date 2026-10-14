using MassTransit;
using OrderSaga.Contracts;

namespace OrderSaga.Host.Kafka;

public sealed class OrderEventLogConsumer<T> : IConsumer<T>
    where T : class, ICorrelatedOrderEvent
{
    private readonly OrderEventLog _log;

    public OrderEventLogConsumer(OrderEventLog log)
    {
        _log = log;
    }

    public Task Consume(ConsumeContext<T> context)
    {
        _log.Record(context.MessageId, context.Message);
        return Task.CompletedTask;
    }
}
