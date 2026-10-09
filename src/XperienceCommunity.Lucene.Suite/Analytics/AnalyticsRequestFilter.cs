using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Request-level exclusions shared by search and interaction capture.</summary>
internal sealed class AnalyticsRequestFilter(IHttpContextAccessor httpContextAccessor, IOptions<SuiteSearchOptions> options)
{
    /// <summary>False when analytics is disabled or the current request is a bot or a prefetch/prerender; true outside a request.</summary>
    public bool ShouldCapture() =>
        options.Value.Analytics.Enabled
        && (httpContextAccessor.HttpContext?.Request is not { } request
            || !(IsPrefetch(request.Headers) || BotUserAgents.IsBot(request.Headers.UserAgent)));

    private static bool IsPrefetch(IHeaderDictionary headers) =>
        headers["Sec-Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase)
        || headers["Sec-Purpose"].ToString().Contains("prerender", StringComparison.OrdinalIgnoreCase)
        || headers["Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase);
}
