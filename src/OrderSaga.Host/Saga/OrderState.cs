using MassTransit;

namespace OrderSaga.Host.Saga;

// The saga class does not store the ETag. The repository keeps it outside this type.
public sealed class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // PendingFacts is the outbox committed by the saga create or replace.
    public List<PendingFact> PendingFacts { get; set; } = [];
}
