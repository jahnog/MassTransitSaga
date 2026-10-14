namespace OrderSaga.Host.Saga;

public static class PendingFacts
{
    public static List<PendingFact> Without(IReadOnlyList<PendingFact> facts, IEnumerable<Guid> producedIds)
    {
        var produced = producedIds as ISet<Guid> ?? producedIds.ToHashSet();
        return facts.Where(fact => !produced.Contains(fact.FactId)).ToList();
    }
}
