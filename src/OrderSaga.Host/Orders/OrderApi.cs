using MassTransit;
using Microsoft.AspNetCore.RateLimiting;
using OrderSaga.Contracts;
using OrderSaga.Host.Cosmos;
using OrderSaga.Host.Kafka;

namespace OrderSaga.Host.Orders;

public static class OrderApi
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/orders", async (
            CreateOrder body,
            OrderCommandSender sender,
            OrderStateReader reader,
            CancellationToken cancellationToken) =>
        {
            var rejected = RejectNewOrder(body.OrderNumber, 0, out var orderNumber);
            if (rejected is not null)
                return rejected;

            var orders = await reader.List(cancellationToken);
            rejected = RejectNewOrder(orderNumber, orders.Count, out orderNumber);
            if (rejected is not null)
                return rejected;

            var correlationId = NewId.NextGuid();
            await sender.Send(new OrderSubmitted(correlationId, orderNumber), cancellationToken);
            return Results.Ok(new { CorrelationId = correlationId });
        }).RequireRateLimiting(DemoLimits.Policy);

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
        }).RequireRateLimiting(DemoLimits.Policy);

        app.MapPost("/orders/{correlationId:guid}/events", async (
            Guid correlationId,
            SendOrderEvent body,
            OrderCommandSender sender,
            OrderStateReader reader,
            CancellationToken cancellationToken) =>
        {
            if (OrderFlow.TryCreate(correlationId, body.Name) is null)
                return Results.BadRequest();

            var orders = await reader.List(cancellationToken);
            var rejected = RejectCommand(orders, correlationId, body.Name, out var command);
            if (rejected is not null || command is null)
                return rejected ?? Results.BadRequest();

            await sender.Send(command, cancellationToken);
            return Results.Accepted();
        }).RequireRateLimiting(DemoLimits.Policy);

        app.MapGet("/orders/{correlationId:guid}/events", (Guid correlationId, OrderEventLog log) =>
        {
            var events = log.For(correlationId);
            return Results.Ok(new { events.SentToSaga, events.ReceivedFromSaga });
        }).RequireRateLimiting(DemoLimits.Policy);
    }

    internal static IResult? RejectNewOrder(string? orderNumber, int existingOrders, out string normalized)
    {
        if (!DemoLimits.TryNormalizeOrderNumber(orderNumber, out normalized))
            return Results.BadRequest();

        if (existingOrders >= DemoLimits.MaxOrders)
            return Results.Conflict();

        return null;
    }

    internal static IResult? RejectCommand(
        IReadOnlyList<OrderDocument> orders,
        Guid correlationId,
        string? name,
        out IOrderCommand? command)
    {
        command = OrderFlow.TryCreate(correlationId, name);
        if (command is null)
            return Results.BadRequest();

        var order = Find(orders, correlationId);
        if (order is null)
            return Results.NotFound();

        if (!OrderFlow.CommandsFor(order.CurrentState).Contains(name))
            return Results.Conflict();

        return null;
    }

    private static OrderDocument? Find(IReadOnlyList<OrderDocument> orders, Guid correlationId)
    {
        foreach (var order in orders)
        {
            if (Guid.TryParse(order.Id, out var id) && id == correlationId)
                return order;
        }

        return null;
    }

    public sealed record CreateOrder(string? OrderNumber);
    public sealed record SendOrderEvent(string? Name);
}
