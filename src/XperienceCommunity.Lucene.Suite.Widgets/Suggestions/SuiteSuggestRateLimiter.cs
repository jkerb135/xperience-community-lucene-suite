using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;

namespace XperienceCommunity.Lucene.Suite.Widgets.Suggestions;

/// <summary>Per-IP fixed-window limit for the suggest endpoint, separate from the events limit and owned by the package so the host's rate limiter is not involved.</summary>
internal sealed class SuiteSuggestRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext>? limiter;
    private readonly LogThrottle warnings;
    private readonly ILogger<SuiteSuggestRateLimiter> logger;

    public SuiteSuggestRateLimiter(IOptions<SuiteWidgetsOptions> options, TimeProvider timeProvider, ILogger<SuiteSuggestRateLimiter> logger)
    {
        int permits = options.Value.SuggestionsPerMinute;
        if (permits > 0)
        {
            limiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        }

        warnings = new LogThrottle(timeProvider, TimeSpan.FromMinutes(1));
        this.logger = logger;
    }

    public bool TryAcquire(HttpContext context)
    {
        if (limiter is null)
        {
            return true;
        }

        using var lease = limiter.AttemptAcquire(context);
        if (!lease.IsAcquired && warnings.ShouldLog())
        {
            logger.LogWarning(
                "Lucene Suite suggestions were rejected by the per-IP limit (LuceneSuite Widgets SuggestionsPerMinute). Behind a proxy or CDN, configure forwarded headers so each visitor has their own IP, or set SuggestionsPerMinute to 0 if the proxy limits requests.");
        }

        return lease.IsAcquired;
    }

    public void Dispose() => limiter?.Dispose();
}
