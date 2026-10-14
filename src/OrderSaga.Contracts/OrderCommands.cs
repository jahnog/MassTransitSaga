namespace OrderSaga.Contracts;

public sealed record OrderSubmitted(Guid CorrelationId, string OrderNumber) : IOrderCommand;

public sealed record PaymentReceived(Guid CorrelationId) : IOrderCommand;

public sealed record OrderShipped(Guid CorrelationId) : IOrderCommand;

public sealed record OrderDelivered(Guid CorrelationId) : IOrderCommand;

public sealed record OrderCancelled(Guid CorrelationId) : IOrderCommand;
