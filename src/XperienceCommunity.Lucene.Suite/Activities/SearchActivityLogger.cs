using System.Text.RegularExpressions;

using CMS.Activities;
using CMS.DataEngine;
using CMS.Websites.Internal;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Analytics;

namespace XperienceCommunity.Lucene.Suite.Activities;

/// <summary>
/// Logs the Suite's custom activities for the current contact; never throws. Consent and cookie level are checked by
/// Kentico's logger, the enabled flag and the HTTP context here, before any other work.
/// <c>isEnabled</c> answers whether an activity type code name exists and is enabled, <c>isWebPage</c> whether a web page item has a GUID.
/// </summary>
internal sealed partial class SearchActivityLogger(
    ICustomActivityLogger activities,
    Func<string, bool> isEnabled,
    Func<Guid, bool> isWebPage,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<SearchActivityLogger> logger)
{
    /// <summary>Longest activity title or value written.</summary>
    internal const int MaxTextLength = 250;

    private readonly LogThrottle logThrottle = new(timeProvider, TimeSpan.FromMinutes(1));

    /// <summary>The logger on Kentico's providers.</summary>
    public static SearchActivityLogger Create(IServiceProvider services)
    {
        var activityTypes = services.GetRequiredService<IInfoProvider<ActivityTypeInfo>>();
        var webPageItems = services.GetRequiredService<IInfoProvider<WebPageItemInfo>>();
        return new SearchActivityLogger(
            services.GetRequiredService<ICustomActivityLogger>(),
            codeName => IsEnabled(activityTypes, codeName),
            guid => IsWebPage(webPageItems, guid),
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<SearchActivityLogger>>());
    }

    // Kentico's provider caches activity types by code name and invalidates them across the web farm.
    internal static bool IsEnabled(IInfoProvider<ActivityTypeInfo> activityTypes, string codeName) =>
        activityTypes.Get(codeName)?.ActivityTypeEnabled == true;

    internal static bool IsWebPage(IInfoProvider<WebPageItemInfo> webPageItems, Guid guid) =>
        webPageItems.Get()
            .WhereEquals(nameof(WebPageItemInfo.WebPageItemGUID), guid)
            .TopN(1)
            .Column(nameof(WebPageItemInfo.WebPageItemID))
            .Any();

    /// <param name="indexName">Index searched; stored as the comment.</param>
    /// <param name="scrubbedQuery">Query as <see cref="QueryScrubber.Normalize"/> returned it; nothing is logged when empty.</param>
    /// <param name="totalHits">Hits found; 0 also logs <see cref="SuiteActivityTypes.SearchNoResults"/>.</param>
    public void LogSearch(string indexName, string scrubbedQuery, int totalHits) => Guarded(request =>
    {
        if (scrubbedQuery.Length == 0)
        {
            return;
        }

        if (isEnabled(SuiteActivityTypes.Search))
        {
            activities.Log(SuiteActivityTypes.Search, Data(request, $"Searched for \"{scrubbedQuery}\"", scrubbedQuery, indexName));
        }

        if (totalHits == 0 && isEnabled(SuiteActivityTypes.SearchNoResults))
        {
            activities.Log(SuiteActivityTypes.SearchNoResults, Data(request, $"Search without results: \"{scrubbedQuery}\"", scrubbedQuery, indexName));
        }
    });

    /// <param name="itemGuid">Clicked item; linked as the activity's web page only when a web page item has this GUID.</param>
    /// <param name="position">1-based absolute position.</param>
    /// <param name="queryText">Untrusted query text; scrubbed with <see cref="QueryScrubber.Normalize"/>.</param>
    /// <param name="resultTitle">Untrusted link text; trimmed, whitespace collapsed, display only.</param>
    public void LogClick(Guid itemGuid, int position, string? queryText, string? resultTitle) => Guarded(request =>
    {
        if (!isEnabled(SuiteActivityTypes.ResultClick))
        {
            return;
        }

        string scrubbedQuery = QueryScrubber.Normalize(queryText);
        string? title = resultTitle is null ? null : Whitespace().Replace(resultTitle.Trim(), " ");
        var data = Data(
            request,
            $"Search result clicked: \"{(string.IsNullOrEmpty(title) ? itemGuid.ToString() : title)}\" (position {position})",
            scrubbedQuery.Length == 0 ? null : scrubbedQuery,
            null);
        // The beacon's own URL is the events endpoint; the page the click happened on is its referrer (query removed).
        if (data.ActivityURLReferrer is { } resultsPage)
        {
            data.ActivityURL = resultsPage;
            data.ActivityURLReferrer = null;
        }

        if (isWebPage(itemGuid))
        {
            data.ActivityWebPageItemGUID = itemGuid;
        }

        activities.Log(SuiteActivityTypes.ResultClick, data);
    });

    /// <summary>Path-only URL (scheme, host, path; no user info, query or fragment) of an absolute http(s) URL; null otherwise.</summary>
    internal static string? WithoutQuery(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)
            : null;

    /// <summary>Removes control characters and cuts to <see cref="MaxTextLength"/>.</summary>
    internal static string Clean(string value)
    {
        string text = string.Concat(value.Where(c => !char.IsControl(c)));
        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    // Kentico fills an unset URL from the raw request (Referer for MVC), which carries the unscrubbed q, so both are always set here.
    private static CustomActivityData Data(HttpRequest request, string title, string? value, string? comment) => new()
    {
        ActivityTitle = Clean(title),
        ActivityValue = value is null ? null : Clean(value),
        ActivityComment = comment,
        ActivityURL = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path),
        ActivityURLReferrer = WithoutQuery(request.Headers.Referer.ToString()),
    };

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Whitespace();

    private void Guarded(Action<HttpRequest> log)
    {
        try
        {
            if (httpContextAccessor.HttpContext?.Request is { } request)
            {
                log(request);
            }
        }
        catch (Exception exception)
        {
            if (logThrottle.ShouldLog())
            {
                logger.LogWarning(exception, "Lucene Suite activity logging failed; the activity was discarded (further failures suppressed for a minute).");
            }
        }
    }
}
