namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Options for suite search.</summary>
public sealed class SuiteSearchOptions
{
    /// <summary>Searchable fields per index: index name (case-insensitive) → field name → boost.</summary>
    public Dictionary<string, Dictionary<string, float>> SearchFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum number of values returned per facet dimension.</summary>
    public int FacetValuesPerDimension { get; set; } = 10;

    /// <summary>Search event capture settings.</summary>
    public SuiteAnalyticsOptions Analytics { get; } = new();
}

/// <summary>Options for search event capture.</summary>
public sealed class SuiteAnalyticsOptions
{
    /// <summary>Whether searches are recorded.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum buffered events; further events are dropped until the writer catches up. Read once at startup.</summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Longest time an event waits in the buffer before it is written.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum events per bulk insert.</summary>
    public int BatchSize { get; set; } = 500;
}
