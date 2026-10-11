using Microsoft.AspNetCore.Mvc;

using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Components;

/// <summary>Model of the facets view.</summary>
/// <param name="Search">The page's search.</param>
/// <param name="Groups">One group per facet dimension.</param>
public sealed record LuceneSuiteFacetsModel(SuitePageSearchResult Search, IReadOnlyList<LuceneSuiteFacetGroup> Groups);

/// <summary>Values of one facet dimension.</summary>
/// <param name="Dimension">Dimension name.</param>
/// <param name="Options">Values with counts and toggle links.</param>
public sealed record LuceneSuiteFacetGroup(string Dimension, IReadOnlyList<LuceneSuiteFacetOption> Options);

/// <summary>A facet value.</summary>
/// <param name="Label">Value.</param>
/// <param name="Count">Matching documents (0 for a selected value outside the top values).</param>
/// <param name="Selected">Whether the value filters the search.</param>
/// <param name="Href">Query string link that toggles the value.</param>
public sealed record LuceneSuiteFacetOption(string Label, int Count, bool Selected, string Href);

/// <summary>Facet values of the page's search as toggle links. Use as <c>&lt;vc:lucene-suite-facets index-name="…" /&gt;</c>.</summary>
public sealed class LuceneSuiteFacetsViewComponent(SuitePageSearch search) : ViewComponent
{
    /// <param name="indexName">Kentico Lucene index to search.</param>
    public IViewComponentResult Invoke(string indexName)
    {
        var result = search.For(HttpContext, indexName);
        var url = result.Url;
        Guid? searchId = result.Response?.SearchId;
        LuceneSuiteFacetOption Option(string dimension, string label, int count) =>
            new(label, count, url.IsSelected(dimension, label), url.ToggleFacetLink(dimension, label, searchId, result.NextSequence));

        var groups = new List<LuceneSuiteFacetGroup>();
        foreach (var facet in result.Response?.Facets ?? [])
        {
            var options = facet.Values.Select(v => Option(facet.Dimension, v.Label, v.Count)).ToList();
            var shown = options.Select(o => o.Label).ToHashSet(StringComparer.Ordinal);
            options.AddRange(Selected(url, facet.Dimension).Where(v => !shown.Contains(v)).Select(v => Option(facet.Dimension, v, 0)));
            groups.Add(new(facet.Dimension, options));
        }

        // Filters the response did not return (or a rejected search) stay listed so they can be removed.
        foreach (var (dimension, values) in url.Facets.Where(f => groups.TrueForAll(g => g.Dimension != f.Key)))
        {
            groups.Add(new(dimension, values.Select(v => Option(dimension, v, 0)).ToList()));
        }

        return View(new LuceneSuiteFacetsModel(result, groups));
    }

    private static IEnumerable<string> Selected(SuiteSearchUrl url, string dimension) =>
        url.Facets.TryGetValue(dimension, out var values) ? values : [];
}
