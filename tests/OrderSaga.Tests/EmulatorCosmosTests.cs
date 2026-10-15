using Microsoft.Azure.Cosmos;
using OrderSaga.Host.Cosmos;

namespace OrderSaga.Tests;

public sealed class EmulatorCosmosTests
{
    [Test]
    public void Apply_trusts_a_localhost_endpoint()
    {
        var options = new CosmosClientOptions();

        EmulatorCosmos.Apply(options, "https://localhost:8081/");

        Assert.That(options.ConnectionMode, Is.EqualTo(ConnectionMode.Gateway));
        Assert.That(options.LimitToEndpoint, Is.True);
        Assert.That((object?)options.HttpClientFactory, Is.Not.Null);
    }

    [Test]
    public void Apply_does_not_replace_the_handler_for_a_remote_endpoint()
    {
        var options = new CosmosClientOptions();

        EmulatorCosmos.Apply(options, "https://example.com");

        Assert.That(options.ConnectionMode, Is.EqualTo(ConnectionMode.Gateway));
        Assert.That(options.LimitToEndpoint, Is.True);
        Assert.That((object?)options.HttpClientFactory, Is.Null);
    }

    [Test]
    public void Apply_does_not_replace_the_handler_for_an_invalid_endpoint()
    {
        var options = new CosmosClientOptions();

        EmulatorCosmos.Apply(options, "not a uri");

        Assert.That(options.ConnectionMode, Is.EqualTo(ConnectionMode.Gateway));
        Assert.That(options.LimitToEndpoint, Is.True);
        Assert.That((object?)options.HttpClientFactory, Is.Null);
    }
}
