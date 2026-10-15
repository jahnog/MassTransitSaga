using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace OrderSaga.Host.Orders;

// One endpoint policy can hold a single partition. The three buckets are chained
// and shared by every /orders route. RequireRateLimiting only marks those routes;
// unmarked requests, including a 404, take the open partition and spend nothing.
public sealed class DemoLimits : IDisposable
{
    public const string Policy = "demo";
    public const int ApiPermitLimit = 6;
    public const int WritePermitLimit = 2;
    public const int MaxInFlight = 6;
    public const int MaxOrders = 50;
    public const int MaxOrderNumberLength = 40;
    public const long MaxBodyBytes = 1024;

    private const string SitePartition = "demo";
    private const string WritePartition = "writes";
    private const string OpenPartition = "open";

    private readonly PartitionedRateLimiter<HttpContext> _concurrency;
    private readonly PartitionedRateLimiter<HttpContext> _api;
    private readonly PartitionedRateLimiter<HttpContext> _writes;

    private DemoLimits(
        PartitionedRateLimiter<HttpContext> concurrency,
        PartitionedRateLimiter<HttpContext> api,
        PartitionedRateLimiter<HttpContext> writes,
        PartitionedRateLimiter<HttpContext> limiter)
    {
        _concurrency = concurrency;
        _api = api;
        _writes = writes;
        Limiter = limiter;
    }

    public PartitionedRateLimiter<HttpContext> Limiter { get; }

    public PartitionedRateLimiter<HttpContext> Concurrency => _concurrency;

    public void Configure(IServiceCollection services)
    {
        var limiter = Limiter;
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "1";
                return ValueTask.CompletedTask;
            };
            // The named policy marks /orders. This chain is the site-wide counter.
            // Unmarked requests take an open partition and do not spend a token.
            options.GlobalLimiter = limiter;
            options.AddPolicy(Policy, _ => RateLimitPartition.GetNoLimiter(OpenPartition));
        });
    }

    public static DemoLimits Create()
    {
        var concurrency = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Marked(context)
                ? RateLimitPartition.GetConcurrencyLimiter(SitePartition, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = MaxInFlight,
                    QueueLimit = 0,
                })
                : RateLimitPartition.GetNoLimiter(OpenPartition));

        var api = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Marked(context)
                ? RateLimitPartition.GetTokenBucketLimiter(SitePartition, _ => ApiOptions())
                : RateLimitPartition.GetNoLimiter(OpenPartition));

        var writes = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Marked(context) && HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetTokenBucketLimiter(WritePartition, _ => WriteOptions())
                : RateLimitPartition.GetNoLimiter(OpenPartition));

        // Concurrency first, then the shared API bucket, then the write bucket.
        // A later refusal disposes the earlier leases, so a rejected POST does not keep an in-flight permit.
        return new DemoLimits(concurrency, api, writes, PartitionedRateLimiter.CreateChained(concurrency, api, writes));
    }

    public static bool TryNormalizeOrderNumber(string? value, out string orderNumber)
    {
        orderNumber = value?.Trim() ?? "";
        if (orderNumber.Length == 0 || orderNumber.Length > MaxOrderNumberLength)
        {
            orderNumber = "";
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        // CreateChained does not dispose the limiters it wraps.
        Limiter.Dispose();
        _concurrency.Dispose();
        _api.Dispose();
        _writes.Dispose();
    }

    private static bool Marked(HttpContext context)
    {
        return context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == Policy;
    }

    private static TokenBucketRateLimiterOptions ApiOptions() => new()
    {
        TokenLimit = ApiPermitLimit,
        TokensPerPeriod = ApiPermitLimit,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    private static TokenBucketRateLimiterOptions WriteOptions() => new()
    {
        TokenLimit = WritePermitLimit,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
