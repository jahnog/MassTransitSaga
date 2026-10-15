using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderSaga.Contracts;
using OrderSaga.Host.Saga;

namespace OrderSaga.Tests;

public sealed class OrderStateMachineTests
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<OrderStateMachine, OrderState> _saga = null!;

    [SetUp]
    public async Task SetUp()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(configurator =>
            {
                configurator.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        _saga = _harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
        await _harness.Start();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _provider.DisposeAsync();
    }

    [Test]
    public async Task Happy_path_appends_one_fact_per_transition()
    {
        var correlationId = await Deliver();

        var saga = await InState(correlationId, machine => machine.Completed);
        Assert.That(saga.OrderNumber, Is.EqualTo("1001"));
        Assert.That(saga.PendingFacts.Select(fact => fact.FactName), Is.EqualTo(new[]
        {
            nameof(OrderAccepted),
            nameof(PaymentRecorded),
            nameof(ShipmentRecorded),
            nameof(OrderCompleted),
        }));
        Assert.That(saga.PendingFacts.Select(fact => fact.FactId), Is.Unique);
        Assert.That(saga.PendingFacts.Select(fact => fact.FactId), Does.Not.Contain(Guid.Empty));
    }

    [Test]
    public async Task Happy_path_records_utc_times_and_does_not_cancel()
    {
        var correlationId = await Deliver();

        var saga = await InState(correlationId, machine => machine.Completed);
        Assert.That(saga.SubmittedAt, Is.Not.Null);
        Assert.That(saga.PaidAt, Is.Not.Null);
        Assert.That(saga.ShippedAt, Is.Not.Null);
        Assert.That(saga.DeliveredAt, Is.Not.Null);
        Assert.That(saga.SubmittedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(saga.PaidAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(saga.ShippedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(saga.DeliveredAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(saga.CancelledAt, Is.Null);
    }

    [Test]
    public async Task Cancel_from_submitted_records_the_cancellation()
    {
        var correlationId = NewId.NextGuid();
        await Publish(new OrderSubmitted(correlationId, "1001"));

        await Publish(new OrderCancelled(correlationId));

        var saga = await InState(correlationId, machine => machine.Cancelled);
        Assert.That(saga.PendingFacts.Select(fact => fact.FactName), Is.EqualTo(new[]
        {
            nameof(OrderAccepted),
            nameof(OrderCancellationRecorded),
        }));
        Assert.That(saga.CancelledAt, Is.Not.Null);
        Assert.That(saga.PaidAt, Is.Null);
        Assert.That(saga.ShippedAt, Is.Null);
        Assert.That(saga.DeliveredAt, Is.Null);
    }

    [Test]
    public async Task Cancel_from_paid_records_the_cancellation()
    {
        var correlationId = NewId.NextGuid();
        await Publish(new OrderSubmitted(correlationId, "1001"));
        await Publish(new PaymentReceived(correlationId));

        await Publish(new OrderCancelled(correlationId));

        var saga = await InState(correlationId, machine => machine.Cancelled);
        Assert.That(saga.PendingFacts.Select(fact => fact.FactName), Is.EqualTo(new[]
        {
            nameof(OrderAccepted),
            nameof(PaymentRecorded),
            nameof(OrderCancellationRecorded),
        }));
        Assert.That(saga.CancelledAt, Is.Not.Null);
        Assert.That(saga.ShippedAt, Is.Null);
        Assert.That(saga.DeliveredAt, Is.Null);
    }

    [Test]
    public async Task Cancel_from_shipped_does_not_append_a_fact()
    {
        var correlationId = NewId.NextGuid();
        await Publish(new OrderSubmitted(correlationId, "1001"));
        await Publish(new PaymentReceived(correlationId));
        await Publish(new OrderShipped(correlationId));
        var shipped = await InState(correlationId, machine => machine.Shipped);
        var stateBefore = shipped.CurrentState;
        var factsBefore = shipped.PendingFacts.Select(fact => fact.FactName).ToArray();

        await Publish(new OrderCancelled(correlationId));
        Assert.That(await _saga.Consumed.Any<OrderCancelled>(), Is.True);

        var saga = _saga.Sagas.Contains(correlationId);
        Assert.That(saga.CurrentState, Is.EqualTo(shipped.CurrentState));
        Assert.That(saga.CurrentState, Is.EqualTo(stateBefore));
        Assert.That(saga.PendingFacts, Has.Count.EqualTo(shipped.PendingFacts.Count));
        Assert.That(saga.PendingFacts.Select(fact => fact.FactName), Is.EqualTo(factsBefore));
    }

    [Test]
    public async Task A_second_submit_does_not_append_another_fact()
    {
        var correlationId = NewId.NextGuid();
        await Publish(new OrderSubmitted(correlationId, "1001"));
        var submitted = await InState(correlationId, machine => machine.Submitted);
        var factsBefore = submitted.PendingFacts.Select(fact => fact.FactName).ToArray();

        await Publish(new OrderSubmitted(correlationId, "1002"));
        Assert.That(await _saga.Consumed.Any<OrderSubmitted>(context => context.Context.Message.OrderNumber == "1002"), Is.True);

        var saga = _saga.Sagas.Contains(correlationId);
        Assert.That(saga.CurrentState, Is.EqualTo("Submitted"));
        Assert.That(saga.OrderNumber, Is.EqualTo("1001"));
        Assert.That(saga.PendingFacts, Has.Count.EqualTo(submitted.PendingFacts.Count));
        Assert.That(saga.PendingFacts.Select(fact => fact.FactName), Is.EqualTo(factsBefore));
    }

    [Test]
    public async Task Payment_with_no_instance_does_not_create_one()
    {
        var correlationId = NewId.NextGuid();

        await Publish(new PaymentReceived(correlationId));
        Assert.That(await _harness.Consumed.Any<PaymentReceived>(), Is.True);

        Assert.That(_saga.Created.Contains(correlationId), Is.Null);
    }

    [Test]
    public async Task Cancel_after_completion_does_not_change_the_order()
    {
        var correlationId = await Deliver();
        var completed = await InState(correlationId, machine => machine.Completed);
        var factsBefore = completed.PendingFacts.Select(fact => (fact.FactId, fact.FactName)).ToArray();
        var submittedAt = completed.SubmittedAt;
        var paidAt = completed.PaidAt;
        var shippedAt = completed.ShippedAt;
        var deliveredAt = completed.DeliveredAt;
        var cancelledAt = completed.CancelledAt;

        await Publish(new OrderCancelled(correlationId));
        Assert.That(await _saga.Consumed.Any<OrderCancelled>(), Is.True);

        var saga = _saga.Sagas.Contains(correlationId);
        Assert.That(saga.CurrentState, Is.EqualTo("Completed"));
        Assert.That(saga.OrderNumber, Is.EqualTo("1001"));
        Assert.That(saga.PendingFacts.Select(fact => (fact.FactId, fact.FactName)), Is.EqualTo(factsBefore));
        Assert.That(saga.SubmittedAt, Is.EqualTo(submittedAt));
        Assert.That(saga.PaidAt, Is.EqualTo(paidAt));
        Assert.That(saga.ShippedAt, Is.EqualTo(shippedAt));
        Assert.That(saga.DeliveredAt, Is.EqualTo(deliveredAt));
        Assert.That(saga.CancelledAt, Is.EqualTo(cancelledAt));
    }

    private async Task<Guid> Deliver()
    {
        var correlationId = NewId.NextGuid();
        await Publish(new OrderSubmitted(correlationId, "1001"));
        await Publish(new PaymentReceived(correlationId));
        await Publish(new OrderShipped(correlationId));
        await Publish(new OrderDelivered(correlationId));
        return correlationId;
    }

    private async Task Publish<T>(T message)
        where T : class
    {
        await _harness.Bus.Publish(message);
    }

    private async Task<OrderState> InState(Guid correlationId, Func<OrderStateMachine, State> state)
    {
        Assert.That(await _saga.Exists(correlationId, state), Is.EqualTo(correlationId));
        return _saga.Sagas.Contains(correlationId);
    }
}
