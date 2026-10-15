using OrderSaga.Host.Kafka;

namespace OrderSaga.Tests;

public sealed class KafkaOptionsTests
{
    [Test]
    public void EnsureValid_accepts_the_default_bootstrap_servers()
    {
        Assert.DoesNotThrow((Action)(() => new KafkaOptions().EnsureValid()));
    }

    [Test]
    public void EnsureValid_rejects_missing_bootstrap_servers()
    {
        var options = new KafkaOptions { BootstrapServers = "" };

        var exception = Assert.Throws<InvalidOperationException>((Action)options.EnsureValid);
        Assert.That(exception!.Message, Is.EqualTo("Kafka:BootstrapServers is required."));
    }
}
