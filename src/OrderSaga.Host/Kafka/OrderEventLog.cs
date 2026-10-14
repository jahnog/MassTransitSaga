using OrderSaga.Contracts;

namespace OrderSaga.Host.Kafka;

public sealed record OrderEventLogView(IReadOnlyList<string> SentToSaga, IReadOnlyList<string> ReceivedFromSaga);

public sealed class OrderEventLog
{
    private const int Cap = 200;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<LoggedEvent>> _events = [];

    public void Record(Guid? messageId, ICorrelatedOrderEvent message)
    {
        lock (_gate)
        {
            if (!_events.TryGetValue(message.CorrelationId, out var list))
            {
                list = [];
                _events[message.CorrelationId] = list;
            }

            if (messageId is Guid id && list.Exists(entry => entry.MessageId == id))
                return;

            list.Add(new LoggedEvent(messageId, message.GetType().Name, message is IOrderFact));
            if (list.Count > Cap)
                list.RemoveRange(0, list.Count - Cap);
        }
    }

    public OrderEventLogView For(Guid correlationId)
    {
        lock (_gate)
        {
            if (!_events.TryGetValue(correlationId, out var list))
                return new OrderEventLogView([], []);

            return new OrderEventLogView(
                list.Where(entry => !entry.FromSaga).Select(entry => entry.TypeName).ToArray(),
                list.Where(entry => entry.FromSaga).Select(entry => entry.TypeName).ToArray());
        }
    }

    private sealed record LoggedEvent(Guid? MessageId, string TypeName, bool FromSaga);
}
