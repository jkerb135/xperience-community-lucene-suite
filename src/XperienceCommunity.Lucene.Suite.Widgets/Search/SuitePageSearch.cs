using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Events;

namespace XperienceCommunity.Lucene.Suite.Widgets.Search;

/// <summary>Outcome of the page's search.</summary>
public enum SuitePageSearchStatus
{
    /// <summary>Nothing to search for (no text, no filter).</summary>
    NotSearched,

    /// <summary>The search ran.</summary>
    Ok,

    /// <summary>The URL held values the search rejects.</summary>
    Invalid,

    /// <summary>The search failed; the error was logged.</summary>
    Unavailable,
}

/// <summary>The search shared by all Lucene Suite components on a page for one index.</summary>
/// <param name="Url">The search state read from the URL.</param>
/// <param name="Status">Outcome.</param>
/// <param name="Response">The search response when <paramref name="Status"/> is <see cref="SuitePageSearchStatus.Ok"/>.</param>
/// <param name="PageSize">Results per page.</param>
/// <param name="Sequence">Sequence this request was recorded under.</param>
public sealed record SuitePageSearchResult(SuiteSearchUrl Url, SuitePageSearchStatus Status, SuiteSearchResponse? Response, int PageSize, int Sequence)
{
    /// <summary>Sequence for links that continue this search.</summary>
    public int NextSequence => Sequence + 1;
}

/// <summary>The search behind the Lucene Suite components: at most one per index per request, driven by the URL (<see cref="SuiteSearchUrl"/>).</summary>
public sealed class SuitePageSearch
{
    private readonly ISuiteSearchService search;
    private readonly ILuceneIndexManager indexes;
    private readonly SuiteContactGroupContexts contactGroups;
    private readonly SuiteEventsPath eventsPath;
    private readonly IOptions<SuiteWidgetsOptions> options;
    private readonly ILogger<SuitePageSearch> logger;
    private readonly LogThrottle failures;

    internal SuitePageSearch(
        ISuiteSearchService search,
        ILuceneIndexManager indexes,
        SuiteContactGroupContexts contactGroups,
        SuiteEventsPath eventsPath,
        IOptions<SuiteWidgetsOptions> options,
        TimeProvider timeProvider,
        ILogger<SuitePageSearch> logger)
    {
        this.search = search;
        this.indexes = indexes;
        this.contactGroups = contactGroups;
        this.eventsPath = eventsPath;
        this.options = options;
        this.logger = logger;
        failures = new LogThrottle(timeProvider, TimeSpan.FromMinutes(1));
    }

    /// <summary>Where click beacons go (with PathBase), or null when MapLuceneSuiteEvents was not called.</summary>
    public string? EventsUrl(HttpContext context) => eventsPath.Value is { } path ? context.Request.PathBase + path : null;

    /// <summary>Runs the page's search for <paramref name="indexName"/> once per request; later calls return the same result.</summary>
    /// <remarks>Marks the response uncacheable whenever a search runs. Rejected URL values and search failures become statuses, never exceptions.</remarks>
    /// <exception cref="InvalidOperationException">When <paramref name="indexName"/> is not a Kentico Lucene index.</exception>
    public SuitePageSearchResult For(HttpContext context, string indexName)
    {
        var key = (typeof(SuitePageSearch), indexName);
        if (context.Items.TryGetValue(key, out var cached))
        {
            return (SuitePageSearchResult)cached!;
        }

        var result = Run(context, indexName);
        context.Items[key] = result;
        return result;
    }

    private SuitePageSearchResult Run(HttpContext context, string indexName)
    {
        if (indexes.GetIndex(indexName) is null)
        {
            throw new InvalidOperationException($"Lucene Suite search components refer to Lucene index '{indexName}', which does not exist.");
        }

        var settings = options.Value;
        var url = SuiteSearchUrl.Parse(context.Request.Query, settings.PageSize);
        if (url.IsEmpty && !settings.SearchOnEmptyQuery)
        {
            return new(url, SuitePageSearchStatus.NotSearched, null, settings.PageSize, 0);
        }

        DisableCaching(context);
        bool continues = url.SearchId is not null && IsSameOrigin(context.Request);
        int sequence = continues ? url.Sequence!.Value : 1;
        if (url.Invalid)
        {
            return new(url, SuitePageSearchStatus.Invalid, null, settings.PageSize, sequence);
        }

        try
        {
            var response = search.Search(new SuiteSearchRequest
            {
                IndexName = indexName,
                Query = url.Query,
                Facets = url.Facets,
                Page = url.Page,
                PageSize = settings.PageSize,
                SearchId = continues ? url.SearchId : null,
                Sequence = continues ? sequence : null,
                IsSubmit = !continues && url.Query.Length > 0,
                Contexts = settings.ContactGroupContexts ? contactGroups.Get() : [],
            });
            return new(url, SuitePageSearchStatus.Ok, response, settings.PageSize, sequence);
        }
        catch (ArgumentException exception)
        {
            logger.LogDebug(exception, "Lucene Suite ignored an invalid search URL for index {Index}.", indexName);
            return new(url, SuitePageSearchStatus.Invalid, null, settings.PageSize, sequence);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (failures.ShouldLog())
            {
                logger.LogError(exception, "Lucene Suite search on index {Index} failed; the page shows the search as unavailable.", indexName);
            }

            return new(url, SuitePageSearchStatus.Unavailable, null, settings.PageSize, sequence);
        }
    }

    // A pasted, bookmarked or cross-site link starts a new search instead of colliding with the original visitor's rows.
    private static bool IsSameOrigin(HttpRequest request) =>
        request.Headers["Sec-Fetch-Site"].FirstOrDefault() is null or "same-origin";

    // Cached HTML would replay sid/seq links and context-specific results to other visitors and skip analytics.
    private static void DisableCaching(HttpContext context)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        if (context.Features.Get<IOutputCacheFeature>() is { } outputCache)
        {
            outputCache.Context.AllowCacheStorage = false;
        }
    }
}
