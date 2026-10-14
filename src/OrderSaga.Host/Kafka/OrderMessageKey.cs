namespace OrderSaga.Host.Kafka;

public static class OrderMessageKey
{
    public static string For(Guid correlationId) => correlationId.ToString();
}
