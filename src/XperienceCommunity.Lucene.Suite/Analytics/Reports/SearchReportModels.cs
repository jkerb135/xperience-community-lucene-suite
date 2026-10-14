namespace XperienceCommunity.Lucene.Suite.Analytics.Reports;

/// <summary>One index and an inclusive UTC day range.</summary>
internal sealed record ReportRange(string IndexName, DateOnly From, DateOnly To)
{
    /// <summary>Number of days in the range.</summary>
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>The equal-length range ending the day before <see cref="From"/>.</summary>
    public ReportRange Previous => this with { From = From.AddDays(-Days), To = From.AddDays(-1) };
}

/// <summary>Summed rollup counts with their rates; a rate is null when its denominator is zero.</summary>
internal sealed record ReportTotals(
    long Searches,
    long ZeroResultSearches,
    long SubmittedSearches,
    long ClickedSearches,
    long Clicks,
    long ClickPositionSum,
    long ConvertedSearches,
    long Conversions,
    long SessionDays,
    IReadOnlyList<long> LatencyBuckets)
{
    public double? NoResultRate => Rate(ZeroResultSearches, Searches);

    public double? Ctr => Rate(ClickedSearches, Searches);

    public double? ConversionRate => Rate(ConvertedSearches, Searches);

    public double? AvgClickPosition => Rate(ClickPositionSum, Clicks);

    internal static double? Rate(long numerator, long denominator) => denominator == 0 ? null : (double)numerator / denominator;
}

/// <summary>Relative change (current − previous) / previous per metric; null when the previous value is null or zero, or the previous period is partial.</summary>
internal sealed record ReportDeltas(
    double? Searches,
    double? ZeroResultSearches,
    double? ClickedSearches,
    double? Clicks,
    double? Conversions,
    double? SessionDays,
    double? NoResultRate,
    double? Ctr,
    double? ConversionRate,
    double? AvgClickPosition);

internal sealed record ReportOverview(
    ReportTotals Current,
    ReportTotals Previous,
    ReportDeltas Deltas,
    DateOnly? FirstDataDay,
    bool PreviousIsPartial,
    DateOnly? CompleteThrough);

/// <summary>One trend day; <see cref="IsComplete"/> is false for days after the rollup watermark.</summary>
internal sealed record ReportDay(
    DateOnly Day,
    long Searches,
    long ZeroResultSearches,
    long ClickedSearches,
    long ConvertedSearches,
    long Clicks,
    long ClickPositionSum,
    long SessionDays,
    double? Ctr,
    bool IsComplete);

internal sealed record ReportTrend(IReadOnlyList<ReportDay> Days, DateOnly? CompleteThrough);

/// <summary>A histogram percentile: the upper bound of its bucket, or <see cref="IsOverflow"/> (over the last bound) with a null value.</summary>
internal sealed record LatencyPercentile(int? Ms, bool IsOverflow);

/// <summary>A query table row; <see cref="QueryText"/> is null for the "(other)" row.</summary>
internal sealed record ReportQueryRow(
    string? QueryText,
    bool IsBrowse,
    long Searches,
    long ZeroResultSearches,
    long ClickedSearches,
    long Clicks,
    double? Ctr,
    double? NoResultRate,
    double? AvgClickPosition,
    LatencyPercentile? LatencyP50,
    LatencyPercentile? LatencyP95);

/// <summary>A page of surfaced queries; <see cref="Other"/> sums everything else in range and is never paged.</summary>
internal sealed record ReportQueryPage(IReadOnlyList<ReportQueryRow> Rows, ReportQueryRow? Other, int TotalCount, DateOnly? CompleteThrough);

internal sealed record ReportLatency(LatencyPercentile? P50, LatencyPercentile? P95, LatencyPercentile? P99, IReadOnlyList<long> Buckets, DateOnly? CompleteThrough);

/// <summary>An item's sums over a range; <see cref="AvgClickPosition"/> is null without clicked searches.</summary>
internal sealed record ReportItemRow(Guid ItemGuid, long ClickedSearches, long Conversions, double? AvgClickPosition);

/// <summary>A page of items; <see cref="TotalCount"/> and the sums cover every item in range.</summary>
internal sealed record ReportItemPage(IReadOnlyList<ReportItemRow> Rows, int TotalCount, long ClickedSearches, long Conversions);

/// <summary>Item orders: the counts descending, the average position ascending with items without clicks last; ties by ItemGuid.</summary>
internal enum ItemSort
{
    ClickedSearches,
    Conversions,
    AvgPosition,
}

internal enum QuerySort
{
    Searches,
    NoResults,
    LowestCtr,
    Slowest,
}
