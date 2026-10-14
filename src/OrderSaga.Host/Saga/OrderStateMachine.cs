using MassTransit;
using OrderSaga.Contracts;

namespace OrderSaga.Host.Saga;

public sealed class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(instance => instance.CurrentState);

        Event(() => OrderSubmitted, correlation => correlation.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentReceived, ConfigureCommand);
        Event(() => OrderShipped, ConfigureCommand);
        Event(() => OrderDelivered, ConfigureCommand);
        Event(() => OrderCancelled, ConfigureCommand);

        OnUnhandledEvent(behavior => behavior.Ignore());

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.OrderNumber = context.Message.OrderNumber;
                    context.Saga.SubmittedAt = DateTime.UtcNow;
                    Append(context.Saga, nameof(OrderAccepted));
                })
                .TransitionTo(Submitted));

        During(Submitted,
            When(PaymentReceived)
                .Then(context =>
                {
                    context.Saga.PaidAt = DateTime.UtcNow;
                    Append(context.Saga, nameof(PaymentRecorded));
                })
                .TransitionTo(Paid),
            When(OrderCancelled)
                .Then(context =>
                {
                    context.Saga.CancelledAt = DateTime.UtcNow;
                    Append(context.Saga, nameof(OrderCancellationRecorded));
                })
                .TransitionTo(Cancelled));

        During(Paid,
            When(OrderShipped)
                .Then(context =>
                {
                    context.Saga.ShippedAt = DateTime.UtcNow;
                    Append(context.Saga, nameof(ShipmentRecorded));
                })
                .TransitionTo(Shipped),
            When(OrderCancelled)
                .Then(context =>
                {
                    context.Saga.CancelledAt = DateTime.UtcNow;
                    Append(context.Saga, nameof(OrderCancellationRecorded));
                })
                .TransitionTo(Cancelled));

        During(Shipped,
            When(OrderDelivered)
                .Then(context =>
                {
                    context.Saga.DeliveredAt = DateTime.UtcNow;
                    Append(context.Saga, nameof(OrderCompleted));
                })
                .TransitionTo(Completed));
    }

    public State Submitted { get; private set; } = null!;
    public State Paid { get; private set; } = null!;
    public State Shipped { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<PaymentReceived> PaymentReceived { get; private set; } = null!;
    public Event<OrderShipped> OrderShipped { get; private set; } = null!;
    public Event<OrderDelivered> OrderDelivered { get; private set; } = null!;
    public Event<OrderCancelled> OrderCancelled { get; private set; } = null!;

    private static void ConfigureCommand<T>(IEventCorrelationConfigurator<OrderState, T> correlation)
        where T : class, ICorrelatedOrderEvent
    {
        correlation.CorrelateById(context => context.Message.CorrelationId);
        correlation.OnMissingInstance(missing => missing.Discard());
    }

    private static void Append(OrderState saga, string factName)
    {
        saga.PendingFacts.Add(new PendingFact(NewId.NextGuid(), factName));
    }
}
