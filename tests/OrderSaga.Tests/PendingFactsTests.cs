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
    }

    [Test]
    public void Without_does_not_change_the_input_list()
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

        PendingFacts.Without(facts, [first, Guid.NewGuid()]);

        Assert.That(facts, Has.Count.EqualTo(3));
    }

    [Test]
    public void Without_keeps_every_fact_when_nothing_was_produced()
    {
        var fact = new PendingFact(Guid.NewGuid(), "OrderAccepted");

        var remaining = PendingFacts.Without([fact], []);

        Assert.That(remaining, Is.EqualTo(new[] { fact }));
    }

    [Test]
    public void Without_returns_empty_when_there_are_no_facts()
    {
        var remaining = PendingFacts.Without([], [Guid.NewGuid()]);

        Assert.That(remaining, Is.Empty);
    }

    [Test]
    public void Without_returns_empty_when_every_id_was_produced()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var facts = new List<PendingFact>
        {
            new(first, "OrderAccepted"),
            new(second, "PaymentRecorded"),
        };

        var remaining = PendingFacts.Without(facts, new HashSet<Guid> { first, second });

        Assert.That(remaining, Is.Empty);
    }

    [Test]
    public void Without_removes_every_copy_of_a_produced_id()
    {
        var shared = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var facts = new List<PendingFact>
        {
            new(shared, "OrderAccepted"),
            new(kept, "PaymentRecorded"),
            new(shared, "OrderAccepted"),
        };

        var remaining = PendingFacts.Without(facts, [shared]);

        Assert.That(remaining.Select(fact => fact.FactId), Is.EqualTo(new[] { kept }));
    }
}
