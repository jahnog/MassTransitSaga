using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using OrderSaga.Host.Cosmos;

namespace OrderSaga.SmokeTests;

public sealed class ConditionalReplaceSmokeTests
{
    [Test]
    public async Task Stale_etag_replace_is_rejected()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        var endpoint = configuration["Cosmos:Endpoint"]!;
        var key = configuration["Cosmos:Key"]!;
        var databaseId = configuration["Cosmos:DatabaseId"]!;
        var containerId = configuration["Cosmos:ContainerId"]!;

        var options = new CosmosClientOptions();
        EmulatorCosmos.Apply(options, endpoint);
        using var client = new CosmosClient(endpoint, key, options);
        var container = client.GetContainer(databaseId, containerId);

        var id = Guid.NewGuid().ToString();
        var document = new
        {
            id,
            orderNumber = "etag",
            currentState = "Submitted",
            pendingFacts = Array.Empty<object>(),
        };
        await container.CreateItemAsync(document, new PartitionKey(id));

        CosmosException? exception = null;
        try
        {
            await container.ReplaceItemAsync(
                document,
                id,
                new PartitionKey(id),
                new ItemRequestOptions { IfMatchEtag = "\"stale-etag\"" });
        }
        catch (CosmosException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
    }
}
