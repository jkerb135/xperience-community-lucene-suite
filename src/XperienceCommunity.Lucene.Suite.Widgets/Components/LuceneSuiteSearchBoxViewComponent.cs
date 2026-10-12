using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Suggestions;

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

    /// <summary>Suggest endpoint URL (with PathBase) when suggestions are enabled for this box; null otherwise.</summary>
    public string? SuggestUrl { get; init; }

    /// <summary>Index the suggestions come from; null when suggestions are off.</summary>
    public string? SuggestIndex { get; init; }

    /// <summary>Shortest input that requests suggestions (<c>LuceneSuite:Suggestions:MinInputLength</c>); null when suggestions are off.</summary>
    public int? SuggestMinLength { get; init; }

    /// <summary>Input id, unique per request; null when suggestions are off. The listbox and status ids derive from it.</summary>
    public string? InputId { get; init; }

    /// <summary>Whether this box references the suggest script (the first enhanced box of the request).</summary>
    public bool IncludeAssets { get; init; }

    /// <summary>Whether this box also references the shipped stylesheet (<see cref="IncludeAssets"/> and <see cref="SuiteWidgetsOptions.SuggestionStyles"/>).</summary>
    public bool IncludeStyles { get; init; }
}

/// <summary>A GET search form; submitting starts a new search (no sid/seq). Use as <c>&lt;vc:lucene-suite-search-box /&gt;</c>.</summary>
public sealed class LuceneSuiteSearchBoxViewComponent(SuitePageSearch search) : ViewComponent
{
    internal const string ViewPath = "~/Views/Shared/Components/LuceneSuiteSearchBox/Default.cshtml";

    private static readonly object SuggestBoxes = new();

    /// <param name="actionUrl">Results page URL; defaults to the current page.</param>
    /// <param name="label">Input label.</param>
    /// <param name="placeholder">Input placeholder.</param>
    /// <param name="indexName">Index for as-you-type suggestions; empty uses the only <see cref="SuiteWidgetsOptions.SuggestionIndexes"/> entry when exactly one is configured. Suggestions need the index in SuggestionIndexes and MapLuceneSuiteSuggestions, and are off in preview.</param>
    public IViewComponentResult Invoke(string? actionUrl = null, string label = "Search", string? placeholder = null, string? indexName = null) =>
        View(ViewPath, Build(search, HttpContext, actionUrl, label, placeholder, indexName));

    internal static LuceneSuiteSearchBoxModel Build(SuitePageSearch search, HttpContext context, string? actionUrl, string label, string? placeholder, string? indexName)
    {
        var preview = search.Preview(context);
        var model = new LuceneSuiteSearchBoxModel(SuiteSearchUrl.Parse(context.Request.Query, 1).Query, actionUrl ?? context.Request.PathBase + context.Request.Path, label, placeholder)
        {
            IsEditMode = preview.IsEditMode,
            IsPreview = preview.IsPreview,
        };

        var services = context.RequestServices;
        string? path = services.GetRequiredService<SuiteSuggestPath>().Value;
        var widgets = services.GetRequiredService<IOptions<SuiteWidgetsOptions>>().Value;
        string? index = string.IsNullOrWhiteSpace(indexName)
            ? widgets.SuggestionIndexes is [var only] ? only : null
            : widgets.SuggestionIndexes.FirstOrDefault(name => string.Equals(name, indexName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (preview.IsPreview || path is null || string.IsNullOrWhiteSpace(index))
        {
            return model;
        }

        int box = (context.Items.TryGetValue(SuggestBoxes, out var count) ? (int)count! : 0) + 1;
        context.Items[SuggestBoxes] = box;
        return model with
        {
            SuggestUrl = context.Request.PathBase + path,
            SuggestIndex = index,
            SuggestMinLength = services.GetRequiredService<IOptions<SuiteSearchOptions>>().Value.Suggestions.MinInputLength,
            InputId = "lucene-suite-q-" + box,
            IncludeAssets = box == 1,
            IncludeStyles = box == 1 && widgets.SuggestionStyles,
        };
    }
}
