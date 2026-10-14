namespace OrderSaga.Host.Cosmos;

public sealed class CosmosOptions
{
    public string Endpoint { get; set; } = "";
    public string Key { get; set; } = "";
    public string DatabaseId { get; set; } = "order-saga";
    public string ContainerId { get; set; } = "order-state";

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("Cosmos:Endpoint is required.");

        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidOperationException("Cosmos:Key is required.");

        if (string.IsNullOrWhiteSpace(DatabaseId))
            throw new InvalidOperationException("Cosmos:DatabaseId is required.");

        if (string.IsNullOrWhiteSpace(ContainerId))
            throw new InvalidOperationException("Cosmos:ContainerId is required.");
    }
}
