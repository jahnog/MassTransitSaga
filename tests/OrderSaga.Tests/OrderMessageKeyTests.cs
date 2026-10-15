using OrderSaga.Host.Kafka;

namespace OrderSaga.Tests;

public sealed class OrderMessageKeyTests
{
    [Test]
    public void For_uses_the_correlation_id_text()
    {
        var correlationId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        Assert.That(OrderMessageKey.For(correlationId), Is.EqualTo("01234567-89ab-cdef-0123-456789abcdef"));
    }
}
