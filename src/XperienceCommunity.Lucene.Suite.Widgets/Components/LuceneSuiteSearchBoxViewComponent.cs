using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Components;

/// <summary>Model of the search box view.</summary>
/// <param name="Query">The query text currently searched.</param>
/// <param name="ActionUrl">Where the form submits (GET).</param>
/// <param name="Label">Visible label of the input.</param>
/// <param name="Placeholder">Placeholder text, if any.</param>
public sealed record LuceneSuiteSearchBoxModel(string Query, string ActionUrl, string Label, string? Placeholder)
{
    /// <summary>True in Page Builder edit mode.</summary>
    public bool IsEditMode { get; init; }

    /// <summary>True in Kentico preview (including edit mode), where the form is rendered disabled: Kentico signs preview URLs, so a submitted query would fail.</summary>
    public bool IsPreview { get; init; }
}

/// <summary>A GET search form; submitting starts a new search (no sid/seq). Use as <c>&lt;vc:lucene-suite-search-box /&gt;</c>.</summary>
public sealed class LuceneSuiteSearchBoxViewComponent(SuitePageSearch search) : ViewComponent
{
    internal const string ViewPath = "~/Views/Shared/Components/LuceneSuiteSearchBox/Default.cshtml";

    /// <param name="actionUrl">Results page URL; defaults to the current page.</param>
    /// <param name="label">Input label.</param>
    /// <param name="placeholder">Input placeholder.</param>
    public IViewComponentResult Invoke(string? actionUrl = null, string label = "Search", string? placeholder = null) =>
        View(ViewPath, Build(search, HttpContext, actionUrl, label, placeholder));

    internal static LuceneSuiteSearchBoxModel Build(SuitePageSearch search, HttpContext context, string? actionUrl, string label, string? placeholder)
    {
        var preview = search.Preview(context);
        return new(SuiteSearchUrl.Parse(context.Request.Query, 1).Query, actionUrl ?? context.Request.PathBase + context.Request.Path, label, placeholder)
        {
            IsEditMode = preview.IsEditMode,
            IsPreview = preview.IsPreview,
        };
    }
}
