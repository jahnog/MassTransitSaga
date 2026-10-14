using System.Text.Json.Serialization;
using OrderSaga.Host.Saga;

namespace OrderSaga.Host.Cosmos;

public sealed class PendingFactDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("_etag")]
    public string ETag { get; set; } = "";

    [JsonPropertyName("pendingFacts")]
    public List<PendingFact> PendingFacts { get; set; } = [];
}
