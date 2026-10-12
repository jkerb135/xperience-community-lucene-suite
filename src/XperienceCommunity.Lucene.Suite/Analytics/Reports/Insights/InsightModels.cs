namespace XperienceCommunity.Lucene.Suite.Analytics.Reports.Insights;

/// <summary>One surfaced query's figures over the range and the previous period; rates as in <see cref="ReportTotals"/>.</summary>
internal sealed record InsightRow(
    string QueryText,
    long Searches,
    long ZeroResultSearches,
    long ClickedSearches,
    long Clicks,
    long ClickPositionSum,
    LatencyPercentile? LatencyP95,
    long PreviousSearches,
    long PreviousZeroResultSearches,
    long PreviousClickedSearches,
    IReadOnlyList<long>? LatencyBuckets = null)
{
    public double? NoResultRate => ReportTotals.Rate(ZeroResultSearches, Searches);

    public double? Ctr => ReportTotals.Rate(ClickedSearches, Searches);

    public double? AvgClickPosition => ReportTotals.Rate(ClickPositionSum, Clicks);

    public double? PreviousNoResultRate => ReportTotals.Rate(PreviousZeroResultSearches, PreviousSearches);

    public double? PreviousCtr => ReportTotals.Rate(PreviousClickedSearches, PreviousSearches);
}

/// <summary>What a finding says is wrong; the order is the tie-break after affected searches.</summary>
internal enum InsightKind
{
    ContentGap,
    RelevanceMiss,
    BuriedAnswer,
    SlowQuery,
    Regression,
}

/// <summary>A rule that fires for the finding's query.</summary>
internal sealed record InsightRule(int Id, string Name);

/// <summary>One problem of one query, with its evidence and the synonyms and rules that already apply to the query.</summary>
internal sealed record InsightFinding(
    InsightKind Kind,
    string QueryText,
    long Searches,
    long Affected,
    double? NoResultRate,
    double? Ctr,
    double? AvgClickPosition,
    LatencyPercentile? LatencyP95,
    long PreviousSearches,
    double? PreviousNoResultRate,
    double? PreviousCtr)
{
    public IReadOnlyList<int> AppliedSynonymIds { get; init; } = [];

    public IReadOnlyList<InsightRule> FiredRules { get; init; } = [];
}

/// <summary>Ranked findings of one index and range.</summary>
/// <param name="Findings">At most MaxFindings, most affected searches first.</param>
/// <param name="Omitted">Findings left out by the MaxFindings cap.</param>
/// <param name="Considered">Queries evaluated (at most MaxQueries).</param>
/// <param name="Truncated">More queries qualified than MaxQueries.</param>
/// <param name="CompleteThrough">The rollup watermark, as in reports.</param>
/// <param name="PreviousIsPartial">The previous period starts before the first data day, so no regression is reported.</param>
/// <param name="AnnotationsFailed">Rules or synonyms could not be loaded or evaluated; the annotations are empty.</param>
internal sealed record InsightReport(
    IReadOnlyList<InsightFinding> Findings,
    int Omitted,
    int Considered,
    bool Truncated,
    DateOnly? CompleteThrough,
    bool PreviousIsPartial,
    bool AnnotationsFailed);
