using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics.Reports.Insights;

/// <summary>Pure insight rules over aggregated query rows; every cutoff is inclusive.</summary>
internal static class InsightEngine
{
    /// <summary>Findings of <paramref name="rows"/>, ordered by affected searches desc, kind, then text, capped at MaxFindings; regressions are skipped when <paramref name="previousComplete"/> is false.</summary>
    public static (IReadOnlyList<InsightFinding> Findings, int Omitted) Evaluate(IEnumerable<InsightRow> rows, SuiteInsightsOptions options, bool previousComplete)
    {
        var all = rows
            .SelectMany(row => Findings(row, options, previousComplete))
            .Where(finding => finding.Affected >= options.MinAffected)
            .OrderByDescending(finding => finding.Affected)
            .ThenBy(finding => finding.Kind)
            .ThenBy(finding => finding.QueryText, StringComparer.Ordinal)
            .ToList();
        return (all.Take(options.MaxFindings).ToList(), Math.Max(0, all.Count - options.MaxFindings));
    }

    private static IEnumerable<InsightFinding> Findings(InsightRow row, SuiteInsightsOptions options, bool previousComplete)
    {
        if (row.NoResultRate is not double noResultRate || row.Ctr is not double ctr)
        {
            yield break;
        }

        if (noResultRate >= options.ContentGapMinNoResultRate)
        {
            yield return Finding(row, InsightKind.ContentGap, row.ZeroResultSearches);
        }
        else if (ctr <= options.RelevanceMissMaxCtr)
        {
            yield return Finding(row, InsightKind.RelevanceMiss, Math.Max(0, row.Searches - row.ZeroResultSearches - row.ClickedSearches));
        }

        if (row.ClickedSearches >= options.BuriedMinClickedSearches && row.AvgClickPosition >= options.BuriedMinAvgPosition)
        {
            yield return Finding(row, InsightKind.BuriedAnswer, row.ClickedSearches);
        }

        // A bucket only says "between its lower and upper edge", so p95 counts as slow when its whole bucket is at or above the cutoff.
        if (row.LatencyP95 is { } p95 && LowerEdgeMs(p95) >= options.SlowMinP95Ms)
        {
            yield return Finding(row, InsightKind.SlowQuery, SlowSearches(row, options.SlowMinP95Ms));
        }

        if (previousComplete && row.PreviousSearches >= options.MinSearches)
        {
            long rise = Worsened(noResultRate, row.PreviousNoResultRate, row.ZeroResultSearches + row.PreviousZeroResultSearches, options)
                ? Share(row.Searches, noResultRate, row.PreviousNoResultRate!.Value)
                : 0;
            long fall = Worsened(row.PreviousCtr, ctr, row.ClickedSearches + row.PreviousClickedSearches, options)
                ? Share(row.Searches, row.PreviousCtr!.Value, ctr)
                : 0;
            if (rise > 0 || fall > 0)
            {
                // Affected = the searches that went wrong because of the change, not every search of the query.
                yield return Finding(row, InsightKind.Regression, Math.Max(rise, fall));
            }
        }
    }

    private static long Share(long searches, double higher, double lower) =>
        (long)Math.Round(searches * ((decimal)higher - (decimal)lower), MidpointRounding.AwayFromZero);

    private static int LowerEdgeMs(LatencyPercentile p95)
    {
        int[] edges = DailyKpi.LatencyBucketUpperBoundsMs;
        int index = p95.IsOverflow ? edges.Length : Array.IndexOf(edges, p95.Ms ?? 0);
        return index <= 0 ? 0 : edges[index - 1];
    }

    // Without buckets (unit-test rows) every search counts.
    private static long SlowSearches(InsightRow row, int cutoffMs) =>
        row.LatencyBuckets is not { } buckets
            ? row.Searches
            : buckets.Where((count, i) => (i == 0 ? 0 : DailyKpi.LatencyBucketUpperBoundsMs[i - 1]) >= cutoffMs).Sum();

    // Decimal so a 0.3 − 0.2 change counts as the 0.1 it is, not 0.0999…; the worsened metric needs MinAffected searches over both periods.
    private static bool Worsened(double? higher, double? lower, long metricSearches, SuiteInsightsOptions options) =>
        higher is double h && lower is double l && h > l
        && (decimal)h - (decimal)l >= (decimal)options.RegressionMinPoints
        && metricSearches >= options.MinAffected;

    private static InsightFinding Finding(InsightRow row, InsightKind kind, long affected) => new(
        kind,
        row.QueryText,
        row.Searches,
        affected,
        row.NoResultRate,
        row.Ctr,
        row.AvgClickPosition,
        row.LatencyP95,
        row.PreviousSearches,
        row.PreviousNoResultRate,
        row.PreviousCtr);
}
