using OrderSaga.Contracts;
using OrderSaga.Host.Orders;

namespace OrderSaga.Tests;

public sealed class OrderFlowTests
{
    [Test]
    public void Commands_for_submitted_are_pay_and_cancel()
    {
        Assert.That(OrderFlow.CommandsFor("Submitted"), Is.EqualTo(new[] { "pay", "cancel" }));
    }

    [Test]
    public void Commands_for_paid_are_ship_and_cancel()
    {
        Assert.That(OrderFlow.CommandsFor("Paid"), Is.EqualTo(new[] { "ship", "cancel" }));
    }

    [Test]
    public void Commands_for_shipped_are_deliver()
    {
        Assert.That(OrderFlow.CommandsFor("Shipped"), Is.EqualTo(new[] { "deliver" }));
    }

    [Test]
    public void Commands_for_completed_are_empty()
    {
        Assert.That(OrderFlow.CommandsFor("Completed"), Is.Empty);
    }

    [Test]
    public void Commands_for_cancelled_are_empty()
    {
        Assert.That(OrderFlow.CommandsFor("Cancelled"), Is.Empty);
    }

    [Test]
    public void TryCreate_pay_returns_payment_received_for_that_order()
    {
        AssertCommand<PaymentReceived>("pay");
    }

    [Test]
    public void TryCreate_ship_returns_order_shipped_for_that_order()
    {
        AssertCommand<OrderShipped>("ship");
    }

    [Test]
    public void TryCreate_deliver_returns_order_delivered_for_that_order()
    {
        AssertCommand<OrderDelivered>("deliver");
    }

    [Test]
    public void TryCreate_cancel_returns_order_cancelled_for_that_order()
    {
        AssertCommand<OrderCancelled>("cancel");
    }

    [Test]
    public void TryCreate_rejects_submit()
    {
        Assert.That(OrderFlow.TryCreate(Guid.NewGuid(), "submit"), Is.Null);
    }

    [Test]
    public void TryCreate_rejects_null()
    {
        Assert.That(OrderFlow.TryCreate(Guid.NewGuid(), null), Is.Null);
    }

    private static void AssertCommand<TCommand>(string name)
        where TCommand : IOrderCommand
    {
        var correlationId = Guid.NewGuid();
        var command = OrderFlow.TryCreate(correlationId, name);

        Assert.That(command, Is.TypeOf<TCommand>());
        Assert.That(command!.CorrelationId, Is.EqualTo(correlationId));
    }
}
