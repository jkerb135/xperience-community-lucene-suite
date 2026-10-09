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
}
