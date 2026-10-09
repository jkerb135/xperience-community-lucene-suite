namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Rollup output for one (UTC day, index, counted query text); maps onto <see cref="SearchDailyInfo"/>.</summary>
internal sealed record DailyKpi(
    DateOnly Day,
    string IndexName,
    string QueryText,
    int Searches,
    int ZeroResultSearches,
    int SubmittedSearches,
    int ClickedSearches,
    int Clicks,
    long ClickPositionSum,
    int ConvertedSearches,
    int Conversions,
    int SessionCount,
    IReadOnlyList<int> LatencyBuckets)
{
    /// <summary>Inclusive upper bounds (ms) of latency buckets 0–8; bucket 9 is the overflow. Fixed, because changing them would make stored days incomparable.</summary>
    internal static readonly int[] LatencyBucketUpperBoundsMs = [10, 25, 50, 100, 250, 500, 1000, 2500, 5000];

    /// <summary>Histogram bucket (0–9) for a latency.</summary>
    internal static int LatencyBucket(int latencyMs)
    {
        int index = Array.FindIndex(LatencyBucketUpperBoundsMs, bound => latencyMs <= bound);
        return index < 0 ? LatencyBucketUpperBoundsMs.Length : index;
    }
}
