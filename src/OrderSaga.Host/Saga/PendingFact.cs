namespace OrderSaga.Host.Saga;

public sealed record PendingFact(Guid FactId, string FactName);
