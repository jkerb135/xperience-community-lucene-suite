using Microsoft.AspNetCore.Mvc;

using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Components;

/// <summary>Model of the search box view.</summary>
/// <param name="Query">The query text currently searched.</param>
/// <param name="ActionUrl">Where the form submits (GET).</param>
/// <param name="Label">Visible label of the input.</param>
/// <param name="Placeholder">Placeholder text, if any.</param>
public sealed record LuceneSuiteSearchBoxModel(string Query, string ActionUrl, string Label, string? Placeholder);

/// <summary>A GET search form; submitting starts a new search (no sid/seq). Use as <c>&lt;vc:lucene-suite-search-box /&gt;</c>.</summary>
public sealed class LuceneSuiteSearchBoxViewComponent : ViewComponent
{
    /// <param name="actionUrl">Results page URL; defaults to the current page.</param>
    /// <param name="label">Input label.</param>
    /// <param name="placeholder">Input placeholder.</param>
    public IViewComponentResult Invoke(string? actionUrl = null, string label = "Search", string? placeholder = null) =>
        View(new LuceneSuiteSearchBoxModel(
            SuiteSearchUrl.Parse(Request.Query, 1).Query,
            actionUrl ?? Request.PathBase + Request.Path,
            label,
            placeholder));
}
