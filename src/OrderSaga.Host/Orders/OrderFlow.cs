using OrderSaga.Contracts;

namespace OrderSaga.Host.Orders;

public static class OrderFlow
{
    public static IReadOnlyList<string> CommandsFor(string? currentState) => currentState switch
    {
        "Submitted" => ["pay", "cancel"],
        "Paid" => ["ship", "cancel"],
        "Shipped" => ["deliver"],
        _ => [],
    };

    public static IOrderCommand? TryCreate(Guid correlationId, string? name) => name switch
    {
        "pay" => new PaymentReceived(correlationId),
        "ship" => new OrderShipped(correlationId),
        "deliver" => new OrderDelivered(correlationId),
        "cancel" => new OrderCancelled(correlationId),
        _ => null,
    };
}
