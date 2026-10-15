using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrderSaga.Contracts;
using OrderSaga.Host.Cosmos;
using OrderSaga.Host.Kafka;
using OrderSaga.Host.Orders;

namespace OrderSaga.Tests;

public sealed class DemoLimitsTests
{
    [Test]
    public void Six_api_calls_succeed_and_the_seventh_is_rejected()
    {
        using var limits = DemoLimits.Create();
        for (var i = 0; i < DemoLimits.ApiPermitLimit; i++)
            using (var lease = Acquire(limits, Marked("GET")))
                Assert.That(lease.IsAcquired, Is.True);

        using var rejected = limits.Limiter.AttemptAcquire(Marked("GET"));
        Assert.That(rejected.IsAcquired, Is.False);
    }

    [Test]
    public void Third_immediate_post_is_rejected_and_a_get_still_succeeds()
    {
        using var limits = DemoLimits.Create();
        using (var first = Acquire(limits, Marked("POST")))
            Assert.That(first.IsAcquired, Is.True);
        using (var second = Acquire(limits, Marked("POST")))
            Assert.That(second.IsAcquired, Is.True);

        using var third = limits.Limiter.AttemptAcquire(Marked("POST"));
        Assert.That(third.IsAcquired, Is.False);

        using var read = limits.Limiter.AttemptAcquire(Marked("GET"));
        Assert.That(read.IsAcquired, Is.True);
    }

    [Test]
    public void Concurrency_frees_a_permit_when_a_lease_is_released()
    {
        using var limits = DemoLimits.Create();
        var held = new List<RateLimitLease>();
        try
        {
            for (var i = 0; i < DemoLimits.MaxInFlight; i++)
            {
                var lease = limits.Concurrency.AttemptAcquire(Marked("GET"));
                Assert.That(lease.IsAcquired, Is.True);
                held.Add(lease);
            }

            using var blocked = limits.Concurrency.AttemptAcquire(Marked("GET"));
            Assert.That(blocked.IsAcquired, Is.False);

            held[0].Dispose();
            held.RemoveAt(0);

            using var next = limits.Concurrency.AttemptAcquire(Marked("GET"));
            Assert.That(next.IsAcquired, Is.True);
        }
        finally
        {
            foreach (var lease in held)
                lease.Dispose();
        }
    }

    [Test]
    public void Rejected_post_does_not_keep_an_in_flight_permit()
    {
        using var limits = DemoLimits.Create();
        using (var first = Acquire(limits, Marked("POST")))
            Assert.That(first.IsAcquired, Is.True);
        using (var second = Acquire(limits, Marked("POST")))
            Assert.That(second.IsAcquired, Is.True);

        using var rejected = limits.Limiter.AttemptAcquire(Marked("POST"));
        Assert.That(rejected.IsAcquired, Is.False);

        var stats = limits.Concurrency.GetStatistics(Marked("GET"));
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.CurrentAvailablePermits, Is.EqualTo(DemoLimits.MaxInFlight));
    }

    [Test]
    public void Unmarked_requests_do_not_spend_the_demo_budget()
    {
        using var limits = DemoLimits.Create();
        for (var i = 0; i < DemoLimits.ApiPermitLimit + 2; i++)
        {
            using var lease = limits.Limiter.AttemptAcquire(new DefaultHttpContext());
            Assert.That(lease.IsAcquired, Is.True);
        }
    }

    [Test]
    public void Order_number_accepts_one_to_forty_trimmed_characters()
    {
        Assert.That(DemoLimits.TryNormalizeOrderNumber("  1001  ", out var trimmed), Is.True);
        Assert.That(trimmed, Is.EqualTo("1001"));
        Assert.That(DemoLimits.TryNormalizeOrderNumber(new string('a', DemoLimits.MaxOrderNumberLength), out _), Is.True);
        Assert.That(DemoLimits.TryNormalizeOrderNumber(null, out _), Is.False);
        Assert.That(DemoLimits.TryNormalizeOrderNumber("   ", out _), Is.False);
        Assert.That(DemoLimits.TryNormalizeOrderNumber(new string('a', DemoLimits.MaxOrderNumberLength + 1), out _), Is.False);
    }

    [Test]
    public void New_order_is_rejected_when_the_list_is_full_or_the_number_is_unusable()
    {
        Assert.That(OrderApi.RejectNewOrder("1001", DemoLimits.MaxOrders - 1, out var number), Is.Null);
        Assert.That(number, Is.EqualTo("1001"));
        Assert.That(Status(OrderApi.RejectNewOrder("1001", DemoLimits.MaxOrders, out _)), Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(Status(OrderApi.RejectNewOrder("  ", 0, out _)), Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(
            Status(OrderApi.RejectNewOrder(new string('a', DemoLimits.MaxOrderNumberLength + 1), 0, out _)),
            Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public void Command_is_sent_only_when_the_order_exists_and_the_action_is_legal()
    {
        var id = Guid.NewGuid();
        var orders = new List<OrderDocument>
        {
            new() { Id = id.ToString(), OrderNumber = "1001", CurrentState = "Submitted" },
        };

        Assert.That(OrderApi.RejectCommand(orders, id, "pay", out var command), Is.Null);
        Assert.That(command, Is.TypeOf<PaymentReceived>());
        Assert.That(Status(OrderApi.RejectCommand(orders, id, "ship", out _)), Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(Status(OrderApi.RejectCommand(orders, Guid.NewGuid(), "pay", out _)), Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(Status(OrderApi.RejectCommand(orders, id, "submit", out _)), Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Pipeline_limits_marked_routes_and_leaves_other_routes_open()
    {
        using var limits = DemoLimits.Create();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        limits.Configure(builder.Services);
        var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/orders", () => Results.Text("ok")).RequireRateLimiting(DemoLimits.Policy);
        app.MapGet("/free", () => Results.Text("ok"));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            for (var i = 0; i < DemoLimits.ApiPermitLimit; i++)
            {
                using var ok = await client.GetAsync("/orders");
                Assert.That((int)ok.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            }

            using var blocked = await client.GetAsync("/orders");
            Assert.That((int)blocked.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
            Assert.That(blocked.Headers.RetryAfter?.Delta, Is.EqualTo(TimeSpan.FromSeconds(1)));

            for (var i = 0; i < DemoLimits.ApiPermitLimit + 2; i++)
            {
                using var free = await client.GetAsync("/free");
                Assert.That((int)free.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Test]
    public void Order_routes_carry_the_demo_policy_and_the_page_does_not()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });
        // Metadata inference requires the handler services to be registered. Nothing is resolved.
        builder.Services.AddSingleton(_ => new OrderCommandSender(null!));
        builder.Services.AddSingleton(_ => new OrderEventLog());
        builder.Services.AddSingleton(_ => new OrderStateReader(null!));
        var app = builder.Build();
        OrderApi.Map(app);

        try
        {
            var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList();
            var limited = endpoints
                .Where(endpoint => endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == DemoLimits.Policy)
                .Select(Describe)
                .ToList();

            Assert.That(limited, Is.EquivalentTo(new[]
            {
                "POST /orders",
                "GET /orders",
                "POST /orders/{correlationId:guid}/events",
                "GET /orders/{correlationId:guid}/events",
            }));
            Assert.That(endpoints.Select(Describe), Does.Not.Contain("GET /"));
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static RateLimitLease Acquire(DemoLimits limits, HttpContext context)
    {
        return limits.Limiter.AttemptAcquire(context);
    }

    private static HttpContext Marked(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(DemoLimits.Policy)),
            "orders"));
        return context;
    }

    private static int? Status(IResult? result)
    {
        return (result as IStatusCodeHttpResult)?.StatusCode;
    }

    private static string Describe(Endpoint endpoint)
    {
        var route = (RouteEndpoint)endpoint;
        var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single();
        return $"{method} {route.RoutePattern.RawText}";
    }
}
