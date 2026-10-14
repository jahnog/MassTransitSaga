namespace OrderSaga.Contracts;

public interface ICorrelatedOrderEvent
{
    Guid CorrelationId { get; }
}

public interface IOrderCommand : ICorrelatedOrderEvent;

public interface IOrderFact : ICorrelatedOrderEvent;
