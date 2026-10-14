using OrderSaga.Host.Saga;

namespace OrderSaga.Tests;

public sealed class PendingFactsTests
{
    [Test]
    public void Without_removes_only_the_produced_ids_and_keeps_order()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var facts = new List<PendingFact>
        {
            new(first, "OrderAccepted"),
            new(second, "PaymentRecorded"),
            new(third, "Unknown"),
        };

        var remaining = PendingFacts.Without(facts, [first, Guid.NewGuid()]);

        Assert.That(remaining.Select(fact => fact.FactId), Is.EqualTo(new[] { second, third }));
        Assert.That(facts, Has.Count.EqualTo(3));
    }

    [Test]
    public void Without_keeps_every_fact_when_nothing_was_produced()
    {
        var fact = new PendingFact(Guid.NewGuid(), "OrderAccepted");

        var remaining = PendingFacts.Without([fact], []);

        Assert.That(remaining, Is.EqualTo(new[] { fact }));
    }
}
