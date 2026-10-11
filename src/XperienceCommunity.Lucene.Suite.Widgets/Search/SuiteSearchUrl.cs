using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Search;

/// <summary>The search state carried in the URL: <c>q</c>, <c>page</c>, <c>f.{dimension}</c>, <c>sid</c> and <c>seq</c>.</summary>
public sealed record SuiteSearchUrl
{
    /// <summary>Query text parameter.</summary>
    public const string QueryParameter = "q";

    /// <summary>1-based page parameter.</summary>
    public const string PageParameter = "page";

    /// <summary>Prefix of facet filter parameters; the rest of the name is the dimension.</summary>
    public const string FacetPrefix = "f.";

    /// <summary>Search id parameter of continuation links.</summary>
    public const string SearchIdParameter = "sid";

    /// <summary>Sequence parameter of continuation links.</summary>
    public const string SequenceParameter = "seq";

    /// <summary>Most facet dimensions read from a URL.</summary>
    public const int MaxDimensions = 10;

    /// <summary>Most values read per facet dimension.</summary>
    public const int MaxValuesPerDimension = 20;

    /// <summary>Highest sequence a continuation link may carry.</summary>
    public const int MaxSequence = 10_000;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoFacets = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Trimmed query text; empty when missing or <see cref="Invalid"/>.</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>1-based page.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Selected facet values by dimension, in URL order.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Facets { get; init; } = NoFacets;

    /// <summary>Search id of a continuation link; set only together with <see cref="Sequence"/>.</summary>
    public Guid? SearchId { get; init; }

    /// <summary>Sequence of a continuation link; set only together with <see cref="SearchId"/>.</summary>
    public int? Sequence { get; init; }

    /// <summary>True when the query text is longer than the search accepts.</summary>
    public bool Invalid { get; init; }

    /// <summary>True when there is no query text and no filter.</summary>
    public bool IsEmpty => Query.Length == 0 && Facets.Count == 0 && !Invalid;

    /// <summary>Reads the search state from a query string; never throws. Values beyond the limits are ignored.</summary>
    public static SuiteSearchUrl Parse(IQueryCollection query, int pageSize)
    {
        string raw = query[QueryParameter].FirstOrDefault()?.Trim() ?? string.Empty;
        bool invalid = raw.Length > SearchExecutor.MaxTextLength;
        int maxPage = (SearchExecutor.MaxResultWindow + pageSize - 1) / Math.Max(pageSize, 1);
        int page = TryInt(query[PageParameter].FirstOrDefault(), 1, maxPage) ?? 1;

        var facets = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, values) in query)
        {
            if (facets.Count == MaxDimensions || !key.StartsWith(FacetPrefix, StringComparison.Ordinal) || key.Length == FacetPrefix.Length)
            {
                continue;
            }

            var kept = values.Where(v => !string.IsNullOrEmpty(v) && v.Length <= SearchExecutor.MaxTextLength)
                .Distinct(StringComparer.Ordinal).Take(MaxValuesPerDimension).ToList();
            if (kept.Count > 0)
            {
                facets[key[FacetPrefix.Length..]] = kept!;
            }
        }

        bool continued = Guid.TryParse(query[SearchIdParameter].FirstOrDefault(), out var searchId) && searchId != Guid.Empty;
        int? sequence = continued ? TryInt(query[SequenceParameter].FirstOrDefault(), 1, MaxSequence) : null;
        return new SuiteSearchUrl
        {
            Query = invalid ? string.Empty : raw,
            Invalid = invalid,
            Page = page,
            Facets = facets,
            SearchId = sequence is null ? null : searchId,
            Sequence = sequence,
        };
    }

    /// <summary>Whether <paramref name="value"/> is selected in <paramref name="dimension"/>.</summary>
    public bool IsSelected(string dimension, string value) =>
        Facets.TryGetValue(dimension, out var values) && values.Contains(value, StringComparer.Ordinal);

    /// <summary>Query string ("?…") of the same search on another page; without <paramref name="searchId"/> the link starts a new search.</summary>
    public string PageLink(int page, Guid? searchId, int nextSequence) => Build(Query, Facets, page, searchId, nextSequence);

    /// <summary>Query string ("?…") with <paramref name="value"/> added to or removed from <paramref name="dimension"/>, back on page 1; without <paramref name="searchId"/> the link starts a new search.</summary>
    public string ToggleFacetLink(string dimension, string value, Guid? searchId, int nextSequence)
    {
        var facets = Facets.ToDictionary(f => f.Key, f => f.Value.ToList(), StringComparer.Ordinal);
        if (!facets.TryGetValue(dimension, out var values))
        {
            facets[dimension] = values = [];
        }

        if (!values.Remove(value))
        {
            values.Add(value);
        }

        return Build(Query, facets.Where(f => f.Value.Count > 0).ToDictionary(f => f.Key, f => (IReadOnlyList<string>)f.Value), 1, searchId, nextSequence);
    }

    private static string Build(string query, IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> facets, int page, Guid? searchId, int nextSequence)
    {
        var builder = new QueryBuilder();
        if (query.Length > 0)
        {
            builder.Add(QueryParameter, query);
        }

        foreach (var (dimension, values) in facets)
        {
            builder.Add(FacetPrefix + dimension, values);
        }

        if (page > 1)
        {
            builder.Add(PageParameter, page.ToString(CultureInfo.InvariantCulture));
        }

        if (searchId is { } id)
        {
            builder.Add(SearchIdParameter, id.ToString("N"));
            builder.Add(SequenceParameter, nextSequence.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static int? TryInt(string? value, int min, int max) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) && result >= min && result <= max ? result : null;
}
