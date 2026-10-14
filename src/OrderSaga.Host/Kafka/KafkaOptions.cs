namespace OrderSaga.Host.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(BootstrapServers))
            throw new InvalidOperationException("Kafka:BootstrapServers is required.");
    }
}
