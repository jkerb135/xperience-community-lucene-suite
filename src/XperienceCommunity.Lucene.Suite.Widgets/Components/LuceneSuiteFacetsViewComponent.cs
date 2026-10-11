using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Components;

/// <summary>Model of the facets view.</summary>
/// <param name="Search">The page's search.</param>
/// <param name="Groups">One group per facet dimension.</param>
public sealed record LuceneSuiteFacetsModel(SuitePageSearchResult Search, IReadOnlyList<LuceneSuiteFacetGroup> Groups)
{
    /// <summary>Display labels by dimension, as configured.</summary>
    public IReadOnlyDictionary<string, string> DimensionLabels { get; init; } = new Dictionary<string, string>();
}

/// <summary>Values of one facet dimension.</summary>
/// <param name="Dimension">Dimension name (used in links).</param>
/// <param name="Label">Heading text: the configured label, else the dimension name.</param>
/// <param name="Options">Values with counts and toggle links.</param>
public sealed record LuceneSuiteFacetGroup(string Dimension, string Label, IReadOnlyList<LuceneSuiteFacetOption> Options);

/// <summary>A facet value.</summary>
/// <param name="Label">Value.</param>
/// <param name="Count">Matching documents (0 for a selected value outside the top values).</param>
/// <param name="Selected">Whether the value filters the search.</param>
/// <param name="Href">Query string link that toggles the value.</param>
public sealed record LuceneSuiteFacetOption(string Label, int Count, bool Selected, string Href);

/// <summary>Facet values of the page's search as toggle links. Use as <c>&lt;vc:lucene-suite-facets index-name="…" /&gt;</c>.</summary>
public sealed class LuceneSuiteFacetsViewComponent(SuitePageSearch search) : ViewComponent
{
    internal const string ViewPath = "~/Views/Shared/Components/LuceneSuiteFacets/Default.cshtml";

    /// <param name="indexName">Kentico Lucene index to search.</param>
    /// <param name="dimensionLabels">Heading labels, one "Dimension = Label" per line.</param>
    public IViewComponentResult Invoke(string indexName, string? dimensionLabels = null) =>
        View(ViewPath, Build(search, HttpContext, indexName, dimensionLabels));

    internal static LuceneSuiteFacetsModel Build(SuitePageSearch search, HttpContext context, string indexName, string? dimensionLabels)
    {
        var result = search.For(context, indexName);
        var url = result.Url;
        var labels = ParseLabels(dimensionLabels);
        Guid? searchId = result.Response?.SearchId;
        LuceneSuiteFacetOption Option(string dimension, string label, int count) =>
            new(label, count, url.IsSelected(dimension, label), url.ToggleFacetLink(dimension, label, searchId, result.NextSequence));
        LuceneSuiteFacetGroup Group(string dimension, List<LuceneSuiteFacetOption> options) =>
            new(dimension, labels.GetValueOrDefault(dimension, dimension), options);

        var groups = new List<LuceneSuiteFacetGroup>();
        foreach (var facet in result.Response?.Facets ?? [])
        {
            var options = facet.Values.Select(v => Option(facet.Dimension, v.Label, v.Count)).ToList();
            var shown = options.Select(o => o.Label).ToHashSet(StringComparer.Ordinal);
            options.AddRange(Selected(url, facet.Dimension).Where(v => !shown.Contains(v)).Select(v => Option(facet.Dimension, v, 0)));
            groups.Add(Group(facet.Dimension, options));
        }

        // Filters the response did not return (or a rejected search) stay listed so they can be removed.
        foreach (var (dimension, values) in url.Facets.Where(f => groups.TrueForAll(g => g.Dimension != f.Key)))
        {
            groups.Add(Group(dimension, values.Select(v => Option(dimension, v, 0)).ToList()));
        }

        return new LuceneSuiteFacetsModel(result, groups) { DimensionLabels = labels };
    }

    /// <summary>"Dimension = Label" per line; the first "=" splits, blank or incomplete lines are skipped, a repeated dimension keeps the last label.</summary>
    internal static IReadOnlyDictionary<string, string> ParseLabels(string? text)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in (text ?? string.Empty).Split('\n'))
        {
            int split = line.IndexOf('=', StringComparison.Ordinal);
            string dimension = split < 0 ? string.Empty : line[..split].Trim();
            string label = split < 0 ? string.Empty : line[(split + 1)..].Trim();
            if (dimension.Length > 0 && label.Length > 0)
            {
                labels[dimension] = label;
            }
        }

        return labels;
    }

    private static IEnumerable<string> Selected(SuiteSearchUrl url, string dimension) =>
        url.Facets.TryGetValue(dimension, out var values) ? values : [];
}
