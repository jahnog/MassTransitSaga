using System.Text.Json.Serialization;

namespace OrderSaga.Host.Cosmos;

public sealed class OrderDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = "";

    [JsonPropertyName("currentState")]
    public string CurrentState { get; set; } = "";
}
