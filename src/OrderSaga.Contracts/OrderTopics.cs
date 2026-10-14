namespace OrderSaga.Contracts;

public static class OrderTopics
{
    public const string OrderSubmitted = "order-submitted";
    public const string PaymentReceived = "payment-received";
    public const string OrderShipped = "order-shipped";
    public const string OrderDelivered = "order-delivered";
    public const string OrderCancelled = "order-cancelled";
    public const string OrderAccepted = "order-accepted";
    public const string PaymentRecorded = "payment-recorded";
    public const string ShipmentRecorded = "shipment-recorded";
    public const string OrderCompleted = "order-completed";
    public const string OrderCancellationRecorded = "order-cancellation-recorded";
}
