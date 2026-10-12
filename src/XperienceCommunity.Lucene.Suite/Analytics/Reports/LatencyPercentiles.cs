using XperienceCommunity.Lucene.Suite.Analytics.Rollup;

namespace XperienceCommunity.Lucene.Suite.Analytics.Reports;

/// <summary>Percentiles over the fixed latency histogram (buckets 0–8 bounded, 9 overflow).</summary>
internal static class LatencyPercentiles
{
    /// <summary>Upper bound of the first bucket whose cumulative count reaches ceil(p × total); null for an empty histogram.</summary>
    public static LatencyPercentile? Of(IReadOnlyList<long> buckets, decimal p)
    {
        long total = buckets.Sum();
        if (total == 0)
        {
            return null;
        }

        // decimal: 0.95 × 20 must be exactly 19, not 19.000000000000004.
        long rank = Math.Max(1, (long)Math.Ceiling(p * total));
        long cumulative = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            cumulative += buckets[i];
            if (cumulative >= rank)
            {
                return i < DailyKpi.LatencyBucketUpperBoundsMs.Length
                    ? new LatencyPercentile(DailyKpi.LatencyBucketUpperBoundsMs[i], false)
                    : new LatencyPercentile(null, true);
            }
        }

        return new LatencyPercentile(null, true);
    }
}
