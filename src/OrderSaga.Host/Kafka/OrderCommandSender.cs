using MassTransit;
using OrderSaga.Contracts;

namespace OrderSaga.Host.Kafka;

public sealed class OrderCommandSender
{
    private readonly IServiceScopeFactory _scopes;

    public OrderCommandSender(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public async Task Send(IOrderCommand command, CancellationToken cancellationToken)
    {
        var key = OrderMessageKey.For(command.CorrelationId);
        await using var scope = _scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        switch (command)
        {
            case OrderSubmitted message:
                await provider.GetRequiredService<ITopicProducer<string, OrderSubmitted>>().Produce(key, message, cancellationToken);
                break;
            case PaymentReceived message:
                await provider.GetRequiredService<ITopicProducer<string, PaymentReceived>>().Produce(key, message, cancellationToken);
                break;
            case OrderShipped message:
                await provider.GetRequiredService<ITopicProducer<string, OrderShipped>>().Produce(key, message, cancellationToken);
                break;
            case OrderDelivered message:
                await provider.GetRequiredService<ITopicProducer<string, OrderDelivered>>().Produce(key, message, cancellationToken);
                break;
            case OrderCancelled message:
                await provider.GetRequiredService<ITopicProducer<string, OrderCancelled>>().Produce(key, message, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "No producer is registered for this command.");
        }
    }
}
