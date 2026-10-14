using MassTransit;
using OrderSaga.Contracts;
using OrderSaga.Host.Cosmos;
using OrderSaga.Host.Kafka;

namespace OrderSaga.Host.Orders;

public static class OrderApi
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/orders", async (CreateOrder body, OrderCommandSender sender, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(body.OrderNumber))
                return Results.BadRequest();

            var correlationId = NewId.NextGuid();
            await sender.Send(new OrderSubmitted(correlationId, body.OrderNumber.Trim()), cancellationToken);
            return Results.Ok(new { CorrelationId = correlationId });
        });

        app.MapGet("/orders", async (OrderStateReader reader, CancellationToken cancellationToken) =>
        {
            var orders = await reader.List(cancellationToken);
            return Results.Ok(orders.Select(order => new
            {
                CorrelationId = order.Id,
                order.OrderNumber,
                order.CurrentState,
                Commands = OrderFlow.CommandsFor(order.CurrentState),
            }));
        });

        app.MapPost("/orders/{correlationId:guid}/events", async (
            Guid correlationId,
            SendOrderEvent body,
            OrderCommandSender sender,
            CancellationToken cancellationToken) =>
        {
            var command = OrderFlow.TryCreate(correlationId, body.Name);
            if (command is null)
                return Results.BadRequest();

            await sender.Send(command, cancellationToken);
            return Results.Accepted();
        });

        app.MapGet("/orders/{correlationId:guid}/events", (Guid correlationId, OrderEventLog log) =>
        {
            var events = log.For(correlationId);
            return Results.Ok(new { events.SentToSaga, events.ReceivedFromSaga });
        });
    }

    public sealed record CreateOrder(string? OrderNumber);
    public sealed record SendOrderEvent(string? Name);
}
