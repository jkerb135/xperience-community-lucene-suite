namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Options for suite search; bound from the configuration section <c>LuceneSuite</c>, then the AddLuceneSuite delegate.</summary>
public sealed class SuiteSearchOptions
{
    /// <summary>Configuration section the options are bound from.</summary>
    public const string SectionName = "LuceneSuite";

    /// <summary>Searchable fields per index: index name (case-insensitive) → field name → boost.</summary>
    public Dictionary<string, Dictionary<string, float>> SearchFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum number of values returned per facet dimension.</summary>
    public int FacetValuesPerDimension { get; set; } = 10;

    /// <summary>Search analytics settings (<c>LuceneSuite:Analytics</c>).</summary>
    public SuiteAnalyticsOptions Analytics { get; } = new();
}

/// <summary>Search analytics settings; the capture settings sit directly on this class, the rest in groups.</summary>
public sealed class SuiteAnalyticsOptions
{
    /// <summary>Whether searches are recorded. Default true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum buffered events; further events are dropped until the writer catches up. Read once at startup. Default 10 000.</summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Longest time an event waits in the buffer before it is written. Default 2 s.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum events per bulk insert. Default 500.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>How requests merge into logical searches; clients must use the same values.</summary>
    public SuiteMergeOptions Merge { get; } = new();

    /// <summary>How clicks and conversions are attributed to searches.</summary>
    public SuiteAttributionOptions Attribution { get; } = new();

    /// <summary>How long analytics data is kept.</summary>
    public SuiteRetentionOptions Retention { get; } = new();

    /// <summary>When a query is shown in reports and suggestions.</summary>
    public SuiteSurfacingOptions Surfacing { get; } = new();

    /// <summary>Background rollup job settings.</summary>
    public SuiteJobOptions Job { get; } = new();
}

/// <summary>Merge settings (<c>LuceneSuite:Analytics:Merge</c>).</summary>
public sealed class SuiteMergeOptions
{
    /// <summary>A text change after a longer gap starts a new logical search; page/filter changes never time out. Default 30 s.</summary>
    public TimeSpan TextChangeWindow { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Shortest query (characters) that tolerates a one-edit typo. Default 4.</summary>
    public int TypoOneEditMinLength { get; set; } = 4;

    /// <summary>Shortest query (characters) that tolerates a two-edit typo; must exceed <see cref="TypoOneEditMinLength"/>. Default 8.</summary>
    public int TypoTwoEditMinLength { get; set; } = 8;

    /// <summary>Backspacing below this fraction of the original text, then typing something else, starts a new search. Default 0.5, range (0, 1).</summary>
    public double DeepBackspaceRatio { get; set; } = 0.5;
}

/// <summary>Attribution settings (<c>LuceneSuite:Analytics:Attribution</c>).</summary>
public sealed class SuiteAttributionOptions
{
    /// <summary>A click or conversion counts only within this time of the response it refers to; also the longest a logical search lasts. Default 1 h.</summary>
    public TimeSpan AttributionWindow { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>Retention settings (<c>LuceneSuite:Analytics:Retention</c>).</summary>
public sealed class SuiteRetentionOptions
{
    /// <summary>How long raw search and interaction rows are kept; at least 1 day + attribution window + rollup interval + 1 h. Default 48 h.</summary>
    public TimeSpan RawRetention { get; set; } = TimeSpan.FromHours(48);

    /// <summary>How long daily KPI rows are kept. Default 25 months (762 days).</summary>
    public TimeSpan DailyRetention { get; set; } = TimeSpan.FromDays(762);

    /// <summary>Daily rows of a query still below the surfacing threshold are pruned this long after it was last seen. Default 30 days.</summary>
    public TimeSpan SubThresholdPruneAfter { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>Surfacing threshold (<c>LuceneSuite:Analytics:Surfacing</c>): a query surfaces only with at least both values.</summary>
public sealed class SuiteSurfacingOptions
{
    /// <summary>Minimum logical searches. Default 5.</summary>
    public int MinSearches { get; set; } = 5;

    /// <summary>Minimum distinct UTC days with a search. Default 2.</summary>
    public int MinDistinctDays { get; set; } = 2;
}

/// <summary>Rollup job settings (<c>LuceneSuite:Analytics:Job</c>).</summary>
public sealed class SuiteJobOptions
{
    /// <summary>How often the rollup job runs. Default 15 min.</summary>
    public TimeSpan RollupInterval { get; set; } = TimeSpan.FromMinutes(15);
}
