using OrderSaga.Contracts;
using OrderSaga.Host.Kafka;

namespace OrderSaga.Tests;

public sealed class OrderEventLogTests
{
    [Test]
    public void Separates_commands_sent_to_the_saga_from_facts_received_from_it()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();

        log.Record(Guid.NewGuid(), new OrderSubmitted(correlationId, "1001"));
        log.Record(Guid.NewGuid(), new OrderAccepted(correlationId));

        var view = log.For(correlationId);
        Assert.That(view.SentToSaga, Is.EqualTo(new[] { nameof(OrderSubmitted) }));
        Assert.That(view.ReceivedFromSaga, Is.EqualTo(new[] { nameof(OrderAccepted) }));
    }

    [Test]
    public void Returns_records_in_arrival_order()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();

        log.Record(Guid.NewGuid(), new OrderSubmitted(correlationId, "1001"));
        log.Record(Guid.NewGuid(), new PaymentReceived(correlationId));

        Assert.That(log.For(correlationId).SentToSaga, Is.EqualTo(new[]
        {
            nameof(OrderSubmitted),
            nameof(PaymentReceived),
        }));
    }

    [Test]
    public void Drops_a_second_record_with_a_message_id_already_stored()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        log.Record(messageId, new OrderSubmitted(correlationId, "1001"));
        log.Record(messageId, new PaymentReceived(correlationId));

        Assert.That(log.For(correlationId).SentToSaga, Is.EqualTo(new[] { nameof(OrderSubmitted) }));
    }

    [Test]
    public void Records_events_that_have_no_message_id()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();

        log.Record(null, new OrderSubmitted(correlationId, "1001"));
        log.Record(null, new PaymentReceived(correlationId));

        Assert.That(log.For(correlationId).SentToSaga, Is.EqualTo(new[]
        {
            nameof(OrderSubmitted),
            nameof(PaymentReceived),
        }));
    }

    [Test]
    public void Keeps_the_same_message_id_on_a_different_order()
    {
        var log = new OrderEventLog();
        var messageId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        log.Record(messageId, new OrderSubmitted(first, "1001"));
        log.Record(messageId, new OrderSubmitted(second, "1002"));

        Assert.That(log.For(first).SentToSaga, Is.EqualTo(new[] { nameof(OrderSubmitted) }));
        Assert.That(log.For(second).SentToSaga, Is.EqualTo(new[] { nameof(OrderSubmitted) }));
    }

    [Test]
    public void For_an_unknown_order_returns_no_events()
    {
        var view = new OrderEventLog().For(Guid.NewGuid());

        Assert.That(view.SentToSaga, Is.Empty);
        Assert.That(view.ReceivedFromSaga, Is.Empty);
    }

    [Test]
    public void For_returns_a_snapshot()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();
        log.Record(Guid.NewGuid(), new OrderSubmitted(correlationId, "1001"));

        var view = log.For(correlationId);
        log.Record(Guid.NewGuid(), new PaymentReceived(correlationId));

        Assert.That(view.SentToSaga, Is.EqualTo(new[] { nameof(OrderSubmitted) }));
    }

    [Test]
    public void Keeps_every_event_until_the_log_reaches_two_hundred()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();
        log.Record(Guid.NewGuid(), new OrderSubmitted(correlationId, "1001"));
        for (var i = 0; i < 199; i++)
            log.Record(Guid.NewGuid(), new PaymentReceived(correlationId));

        var sent = log.For(correlationId).SentToSaga;
        Assert.That(sent, Has.Count.EqualTo(200));
        Assert.That(sent[0], Is.EqualTo(nameof(OrderSubmitted)));
        Assert.That(sent[^1], Is.EqualTo(nameof(PaymentReceived)));
    }

    [Test]
    public void Drops_the_oldest_event_after_two_hundred()
    {
        var log = new OrderEventLog();
        var correlationId = Guid.NewGuid();
        log.Record(Guid.NewGuid(), new OrderSubmitted(correlationId, "1001"));
        for (var i = 0; i < 200; i++)
            log.Record(Guid.NewGuid(), new PaymentReceived(correlationId));

        var sent = log.For(correlationId).SentToSaga;
        Assert.That(sent, Has.Count.EqualTo(200));
        Assert.That(sent, Is.All.EqualTo(nameof(PaymentReceived)));
    }
}
