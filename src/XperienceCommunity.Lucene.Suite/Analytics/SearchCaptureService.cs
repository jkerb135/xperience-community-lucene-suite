using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Records each answered search as a <see cref="SearchEvent"/>; capture failures never reach the caller.</summary>
internal sealed class SearchCaptureService(
    ISuiteSearchService inner,
    SearchEventQueue queue,
    SearchSessionCookie sessionCookie,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SuiteSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<SearchCaptureService> logger) : ISuiteSearchService
{
    private readonly LogThrottle logThrottle = new(timeProvider, TimeSpan.FromMinutes(1));

    public SuiteSearchResponse Search(SuiteSearchRequest request)
    {
        var response = inner.Search(request) with { SearchId = request.SearchId ?? Guid.NewGuid() };
        try
        {
            Capture(request, response);
        }
        catch (Exception exception)
        {
            if (logThrottle.ShouldLog())
            {
                logger.LogWarning(exception, "Lucene Suite search capture failed; the search event was discarded (further failures suppressed for a minute).");
            }
        }

        return response;
    }

    private void Capture(SuiteSearchRequest request, SuiteSearchResponse response)
    {
        if (!options.Value.Analytics.Enabled || !request.TrackAnalytics || response.IndexDocumentCount == 0)
        {
            return;
        }

        if (httpContextAccessor.HttpContext?.Request is { } httpRequest
            && (IsPrefetch(httpRequest.Headers) || BotUserAgents.IsBot(httpRequest.Headers.UserAgent)))
        {
            return;
        }

        bool minted = request.SearchId is null;
        queue.Enqueue(new SearchEvent(
            response.SearchId,
            minted ? 1 : request.Sequence ?? 1,
            sessionCookie.GetSessionId(),
            timeProvider.GetUtcNow().UtcDateTime,
            Truncate(request.IndexName, SearchEventInfo.IndexNameMaxLength),
            QueryScrubber.Normalize(request.Query),
            request.Page,
            request.PageSize,
            request.Facets.Values.Sum(values => values.Count),
            response.TotalHits,
            (int)Math.Min(int.MaxValue, Math.Round(response.Elapsed.TotalMilliseconds)),
            minted || request.IsSubmit,
            minted ? SearchEventOrigin.Server : SearchEventOrigin.Client));
    }

    private static bool IsPrefetch(IHeaderDictionary headers) =>
        headers["Sec-Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase)
        || headers["Sec-Purpose"].ToString().Contains("prerender", StringComparison.OrdinalIgnoreCase)
        || headers["Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;
}
