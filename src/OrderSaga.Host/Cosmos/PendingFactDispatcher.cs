using System.Net;
using Microsoft.Azure.Cosmos;
using OrderSaga.Host.Kafka;
using OrderSaga.Host.Saga;

namespace OrderSaga.Host.Cosmos;

public sealed class PendingFactDispatcher : BackgroundService
{
    private const string PendingFactsQuery =
        "SELECT c.id, c._etag, c.pendingFacts FROM c WHERE ARRAY_LENGTH(c.pendingFacts) > 0";

    private readonly Container _container;
    private readonly OrderFactProducer _producer;
    private readonly ILogger<PendingFactDispatcher> _logger;

    public PendingFactDispatcher(Container container, OrderFactProducer producer, ILogger<PendingFactDispatcher> logger)
    {
        _container = container;
        _producer = producer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Dispatch(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Pending fact dispatch failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task Dispatch(CancellationToken cancellationToken)
    {
        var query = _container.GetItemQueryIterator<PendingFactDocument>(new QueryDefinition(PendingFactsQuery));
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            foreach (var document in page)
                await DispatchDocument(document, cancellationToken);
        }
    }

    private async Task DispatchDocument(PendingFactDocument document, CancellationToken cancellationToken)
    {
        if (document.PendingFacts.Count == 0 || string.IsNullOrEmpty(document.Id))
            return;

        if (!Guid.TryParse(document.Id, out var correlationId))
        {
            _logger.LogWarning("Pending facts document {DocumentId} is not a correlation id.", document.Id);
            return;
        }

        var producedIds = new List<Guid>();
        foreach (var fact in document.PendingFacts)
        {
            if (!await _producer.TryProduce(correlationId, fact, cancellationToken))
            {
                _logger.LogWarning(
                    "Unknown fact {FactName} on {CorrelationId} was left on the document.",
                    fact.FactName,
                    correlationId);
                continue;
            }

            producedIds.Add(fact.FactId);
        }

        if (producedIds.Count == 0)
            return;

        if (string.IsNullOrEmpty(document.ETag))
        {
            _logger.LogWarning("Pending facts on {CorrelationId} had no ETag.", correlationId);
            return;
        }

        var remaining = PendingFacts.Without(document.PendingFacts, producedIds);
        try
        {
            await Patch(document.Id, document.ETag, remaining, cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            await PatchAfterReread(document.Id, producedIds, cancellationToken);
        }
    }

    private async Task PatchAfterReread(string id, IReadOnlyList<Guid> producedIds, CancellationToken cancellationToken)
    {
        var read = await _container.ReadItemAsync<PendingFactDocument>(id, new PartitionKey(id), cancellationToken: cancellationToken);
        var etag = string.IsNullOrEmpty(read.Resource.ETag) ? read.ETag : read.Resource.ETag;
        var remaining = PendingFacts.Without(read.Resource.PendingFacts, producedIds);
        try
        {
            await Patch(id, etag, remaining, cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogInformation("Pending facts on {CorrelationId} changed again. Waiting for the next tick.", id);
        }
    }

    private Task Patch(string id, string etag, List<PendingFact> remaining, CancellationToken cancellationToken)
    {
        return _container.PatchItemAsync<PendingFactDocument>(
            id,
            new PartitionKey(id),
            [PatchOperation.Set("/pendingFacts", remaining)],
            new PatchItemRequestOptions { IfMatchEtag = etag },
            cancellationToken);
    }
}
