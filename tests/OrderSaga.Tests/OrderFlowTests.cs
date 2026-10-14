using OrderSaga.Contracts;
using OrderSaga.Host.Orders;

namespace OrderSaga.Tests;

public sealed class OrderFlowTests
{
    [Test]
    public void Commands_follow_the_saga_edges()
    {
        Assert.That(OrderFlow.CommandsFor("Submitted"), Is.EqualTo(new[] { "pay", "cancel" }));
        Assert.That(OrderFlow.CommandsFor("Paid"), Is.EqualTo(new[] { "ship", "cancel" }));
        Assert.That(OrderFlow.CommandsFor("Shipped"), Is.EqualTo(new[] { "deliver" }));
        Assert.That(OrderFlow.CommandsFor("Completed"), Is.Empty);
        Assert.That(OrderFlow.CommandsFor("Cancelled"), Is.Empty);
    }

    [Test]
    public void TryCreate_maps_page_names_and_rejects_unknown_names()
    {
        var correlationId = Guid.NewGuid();

        Assert.That(OrderFlow.TryCreate(correlationId, "pay"), Is.TypeOf<PaymentReceived>());
        Assert.That(OrderFlow.TryCreate(correlationId, "ship"), Is.TypeOf<OrderShipped>());
        Assert.That(OrderFlow.TryCreate(correlationId, "deliver"), Is.TypeOf<OrderDelivered>());
        Assert.That(OrderFlow.TryCreate(correlationId, "cancel"), Is.TypeOf<OrderCancelled>());
        Assert.That(OrderFlow.TryCreate(correlationId, "submit"), Is.Null);
        Assert.That(OrderFlow.TryCreate(correlationId, null), Is.Null);
    }
}
