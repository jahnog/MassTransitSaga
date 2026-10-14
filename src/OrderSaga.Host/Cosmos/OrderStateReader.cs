using Microsoft.Azure.Cosmos;

namespace OrderSaga.Host.Cosmos;

public sealed class OrderStateReader
{
    private readonly Container _container;

    public OrderStateReader(Container container)
    {
        _container = container;
    }

    public async Task<IReadOnlyList<OrderDocument>> List(CancellationToken cancellationToken)
    {
        var query = _container.GetItemQueryIterator<OrderDocument>(
            new QueryDefinition("SELECT c.id, c.orderNumber, c.currentState FROM c"));
        var orders = new List<OrderDocument>();
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            orders.AddRange(page);
        }

        return orders;
    }
}
