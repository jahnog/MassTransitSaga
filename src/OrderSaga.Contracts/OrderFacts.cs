namespace OrderSaga.Contracts;

public sealed record OrderAccepted(Guid CorrelationId) : IOrderFact;

public sealed record PaymentRecorded(Guid CorrelationId) : IOrderFact;

public sealed record ShipmentRecorded(Guid CorrelationId) : IOrderFact;

public sealed record OrderCompleted(Guid CorrelationId) : IOrderFact;

public sealed record OrderCancellationRecorded(Guid CorrelationId) : IOrderFact;
