using OrderSaga.Host.Cosmos;

namespace OrderSaga.Tests;

public sealed class CosmosOptionsTests
{
    [Test]
    public void EnsureValid_accepts_every_required_field()
    {
        var options = Valid();

        Assert.DoesNotThrow((Action)options.EnsureValid);
    }

    [Test]
    public void EnsureValid_rejects_a_missing_endpoint()
    {
        var options = Valid();
        options.Endpoint = "";

        var exception = Assert.Throws<InvalidOperationException>((Action)options.EnsureValid);
        Assert.That(exception!.Message, Is.EqualTo("Cosmos:Endpoint is required."));
    }

    [Test]
    public void EnsureValid_rejects_a_missing_key()
    {
        var options = Valid();
        options.Key = "";

        var exception = Assert.Throws<InvalidOperationException>((Action)options.EnsureValid);
        Assert.That(exception!.Message, Is.EqualTo("Cosmos:Key is required."));
    }

    [Test]
    public void EnsureValid_rejects_a_missing_database_id()
    {
        var options = Valid();
        options.DatabaseId = "";

        var exception = Assert.Throws<InvalidOperationException>((Action)options.EnsureValid);
        Assert.That(exception!.Message, Is.EqualTo("Cosmos:DatabaseId is required."));
    }

    [Test]
    public void EnsureValid_rejects_a_missing_container_id()
    {
        var options = Valid();
        options.ContainerId = "";

        var exception = Assert.Throws<InvalidOperationException>((Action)options.EnsureValid);
        Assert.That(exception!.Message, Is.EqualTo("Cosmos:ContainerId is required."));
    }

    private static CosmosOptions Valid() => new()
    {
        Endpoint = "https://localhost:8081/",
        Key = "key",
        DatabaseId = "order-saga",
        ContainerId = "order-state",
    };
}
