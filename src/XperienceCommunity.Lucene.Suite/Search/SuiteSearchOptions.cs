namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Options for suite search.</summary>
public sealed class SuiteSearchOptions
{
    /// <summary>Searchable fields per index: index name (case-insensitive) → field name → boost.</summary>
    public Dictionary<string, Dictionary<string, float>> SearchFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum number of values returned per facet dimension.</summary>
    public int FacetValuesPerDimension { get; set; } = 10;
}
