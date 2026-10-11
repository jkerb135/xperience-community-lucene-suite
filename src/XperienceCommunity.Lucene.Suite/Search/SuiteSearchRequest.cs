namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>A search request against one Kentico Lucene index.</summary>
public sealed record SuiteSearchRequest
{
    /// <summary>Name of the Kentico Lucene index to search.</summary>
    public required string IndexName { get; init; }

    /// <summary>Query text; null, empty or whitespace matches every document.</summary>
    public string? Query { get; init; }

    /// <summary>Facet filters: dimension → values; values within a dimension are OR'd, dimensions are AND'd.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Facets { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Hits per page (1–100).</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>Browser-minted id of the logical search; null lets the server mint one (server-rendered / no-JS request).</summary>
    public Guid? SearchId { get; init; }

    /// <summary>1-based position of this request within <see cref="SearchId"/>; ignored when <see cref="SearchId"/> is null.</summary>
    public int? Sequence { get; init; }

    /// <summary>Whether the visitor explicitly submitted (Enter / button) rather than typed, paged or filtered.</summary>
    public bool IsSubmit { get; init; }

    /// <summary>Whether to record this search in analytics; false for admin tools, previews and probes.</summary>
    public bool TrackAnalytics { get; init; } = true;

    /// <summary>Visitor contexts rules can target, such as contact group code names; matched ordinally.</summary>
    public IReadOnlyCollection<string> Contexts { get; init; } = [];

    /// <summary>Whether search rules apply; false shows the unruled results (for example in a preview).</summary>
    public bool ApplyRules { get; init; } = true;

    /// <summary>Whether the index's synonyms expand the query text. Default true.</summary>
    public bool ApplySynonyms { get; init; } = true;
}
