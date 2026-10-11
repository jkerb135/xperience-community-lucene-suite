using Kentico.Xperience.Lucene.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Events;
using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Components;

/// <summary>Model of the results view.</summary>
/// <param name="Search">The page's search.</param>
/// <param name="Hits">Hits on this page.</param>
/// <param name="FirstPosition">Absolute 1-based position of the first hit.</param>
/// <param name="TotalPages">Pages reachable within the result window.</param>
/// <param name="Pages">Links to nearby pages, including the current one.</param>
/// <param name="PreviousLink">Link to the previous page, if any.</param>
/// <param name="NextLink">Link to the next page, if any.</param>
/// <param name="EventsUrl">Where click beacons go; null disables them.</param>
public sealed record LuceneSuiteResultsModel(
    SuitePageSearchResult Search,
    IReadOnlyList<LuceneSuiteResultHit> Hits,
    int FirstPosition,
    int TotalPages,
    IReadOnlyList<LuceneSuitePageLink> Pages,
    string? PreviousLink,
    string? NextLink,
    string? EventsUrl);

/// <summary>A hit ready to render.</summary>
/// <param name="Title">Title field, else the URL, else the item GUID.</param>
/// <param name="Summary">Summary field, if configured and present.</param>
/// <param name="Href">Safe link (relative or http/https), with sr= when it stays on the site; null renders the hit unlinked.</param>
/// <param name="Tracked">Whether the link carries sr= and click beacon data (same-site link with an item GUID).</param>
/// <param name="Position">Absolute 1-based position.</param>
/// <param name="Hit">The raw hit, for views that render more fields.</param>
public sealed record LuceneSuiteResultHit(string Title, string? Summary, string? Href, bool Tracked, int Position, SuiteSearchHit Hit);

/// <summary>A pagination link.</summary>
/// <param name="Page">1-based page.</param>
/// <param name="Href">Query string link.</param>
/// <param name="Current">Whether this is the page shown.</param>
public sealed record LuceneSuitePageLink(int Page, string Href, bool Current);

/// <summary>Hits, result count and pagination of the page's search, with tracked result links. Use as <c>&lt;vc:lucene-suite-results index-name="…" /&gt;</c>.</summary>
public sealed class LuceneSuiteResultsViewComponent(SuitePageSearch search) : ViewComponent
{
    private const int PageWindow = 2;

    /// <param name="indexName">Kentico Lucene index to search.</param>
    /// <param name="titleField">Stored field holding the hit title.</param>
    /// <param name="urlField">Stored field holding the hit URL (Kentico stores "~/…" paths).</param>
    /// <param name="summaryField">Stored field holding a summary; none by default.</param>
    public IViewComponentResult Invoke(string indexName, string titleField = "Title", string urlField = BaseDocumentProperties.URL, string? summaryField = null)
    {
        var result = search.For(HttpContext, indexName);
        var response = result.Response;
        int first = ((result.Url.Page - 1) * result.PageSize) + 1;
        var hits = (response?.Hits ?? []).Select((hit, i) => ToHit(hit, first + i, result, titleField, urlField, summaryField)).ToList();

        int totalPages = response is null ? 0 : (int)Math.Ceiling(Math.Min(response.TotalHits, SearchExecutor.MaxResultWindow) / (double)result.PageSize);
        int page = result.Url.Page;
        Guid? searchId = response?.SearchId;
        var pages = Enumerable.Range(Math.Max(1, page - PageWindow), Math.Max(0, Math.Min(totalPages, page + PageWindow) - Math.Max(1, page - PageWindow) + 1))
            .Select(p => new LuceneSuitePageLink(p, result.Url.PageLink(p, searchId, result.NextSequence), p == page))
            .ToList();

        return View(new LuceneSuiteResultsModel(
            result,
            hits,
            first,
            totalPages,
            pages,
            page > 1 && page <= totalPages ? result.Url.PageLink(page - 1, searchId, result.NextSequence) : null,
            page < totalPages ? result.Url.PageLink(page + 1, searchId, result.NextSequence) : null,
            search.EventsUrl(HttpContext)));
    }

    private LuceneSuiteResultHit ToHit(SuiteSearchHit hit, int position, SuitePageSearchResult result, string titleField, string urlField, string? summaryField)
    {
        string? raw = First(hit, urlField);
        var (href, sameSite) = SafeLink(raw is not null && raw.StartsWith("~/", StringComparison.Ordinal) ? Url.Content(raw) : raw, Request);
        bool tracked = href is not null && sameSite && hit.ItemGuid is not null && result.Response is not null;
        if (tracked)
        {
            href = QueryHelpers.AddQueryString(href!, SuiteResultReference.QueryParameter, SuiteResultReference.Format(result.Response!.SearchId, result.Sequence, position));
        }

        string title = First(hit, titleField) ?? raw ?? hit.ItemGuid?.ToString() ?? string.Empty;
        return new LuceneSuiteResultHit(title, summaryField is null ? null : First(hit, summaryField), href, tracked, position, hit);
    }

    /// <summary>Only relative paths and http/https URLs become links; "//host" and "/\host" are other sites in browsers.</summary>
    internal static (string? Href, bool SameSite) SafeLink(string? value, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, false);
        }

        if (value.StartsWith('/'))
        {
            return value.Length > 1 && value[1] is '/' or '\\' ? (null, false) : (value, true);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? (uri.AbsoluteUri, string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
            : (null, false);
    }

    private static string? First(SuiteSearchHit hit, string field) =>
        hit.Fields.TryGetValue(field, out var values) ? values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) : null;
}
