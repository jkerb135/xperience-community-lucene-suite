using System.Globalization;

using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics.Reports;

/// <summary>Report numbers from <see cref="SearchDailyInfo"/> rollups. Performs no authorization: callers gate on Site Search View and validate the index.</summary>
internal sealed class SearchReportService(
    IInfoProvider<SearchDailyInfo> daily,
    DailyRollupStore store,
    IOptions<SuiteSearchOptions> options,
    TimeProvider clock)
{
    public const int MaxTake = 200;

    private static readonly string[] SummedColumns =
    [
        nameof(SearchDailyInfo.Searches), nameof(SearchDailyInfo.ZeroResultSearches), nameof(SearchDailyInfo.SubmittedSearches),
        nameof(SearchDailyInfo.ClickedSearches), nameof(SearchDailyInfo.Clicks), nameof(SearchDailyInfo.ClickPositionSum),
        nameof(SearchDailyInfo.ConvertedSearches), nameof(SearchDailyInfo.Conversions), nameof(SearchDailyInfo.SessionCount),
        .. Enumerable.Range(0, SearchDailyInfo.LatencyBucketCount).Select(i => $"LatencyBucket{i}"),
    ];

    public ReportOverview Overview(string indexName, DateOnly from, DateOnly to)
    {
        var range = Validate(indexName, from, to);
        var previous = range.Previous;
        var days = Summed(range with { From = previous.From }, nameof(SearchDailyInfo.Day)).GetEnumerableTypedResult().ToList();
        var current = ToTotals(days.Where(row => DateOnly.FromDateTime(row.Day) >= range.From));
        var before = ToTotals(days.Where(row => DateOnly.FromDateTime(row.Day) < range.From));

        var first = daily.Get()
            .TopN(1)
            .Column(nameof(SearchDailyInfo.Day))
            .WhereEquals(nameof(SearchDailyInfo.IndexName), range.IndexName)
            .OrderBy(nameof(SearchDailyInfo.Day))
            .GetScalarResult(DateTime.MinValue);
        DateOnly? firstDataDay = first == DateTime.MinValue ? null : DateOnly.FromDateTime(first);
        bool partial = firstDataDay is not DateOnly firstDay || previous.From < firstDay;

        return new ReportOverview(current, before, Deltas(current, before, partial), firstDataDay, partial, store.ReadWatermark());
    }

    public ReportTrend Trend(string indexName, DateOnly from, DateOnly to)
    {
        var range = Validate(indexName, from, to);
        var byDay = Summed(range, nameof(SearchDailyInfo.Day)).GetEnumerableTypedResult()
            .ToDictionary(row => DateOnly.FromDateTime(row.Day), row => ToTotals([row]));
        var watermark = store.ReadWatermark();
        var days = Enumerable.Range(0, range.Days).Select(offset =>
        {
            var day = range.From.AddDays(offset);
            var totals = byDay.GetValueOrDefault(day);
            return new ReportDay(
                day,
                totals?.Searches ?? 0,
                totals?.ZeroResultSearches ?? 0,
                totals?.ClickedSearches ?? 0,
                totals?.ConvertedSearches ?? 0,
                totals?.Clicks ?? 0,
                totals?.ClickPositionSum ?? 0,
                totals?.SessionDays ?? 0,
                totals?.Ctr,
                day <= watermark);
        }).ToList();
        return new ReportTrend(days, watermark);
    }

    public ReportQueryPage Queries(string indexName, DateOnly from, DateOnly to, QuerySort sort, int skip, int take)
    {
        var range = Validate(indexName, from, to);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, MaxTake);

        // Surfaced membership is decided by SQL on both sides, so the database collation alone defines text equality.
        var namedRows = Summed(range, nameof(SearchDailyInfo.QueryText))
            .WhereIn(nameof(SearchDailyInfo.QueryText), Surfacing.Query(daily, range.IndexName, options.Value.Analytics.Surfacing))
            .GetEnumerableTypedResult()
            .ToList();
        var named = namedRows.Select(row => ToRow(row.QueryText, ToTotals([row])));
        // Named rows and "(other)" partition the range, so Other = all − named (a NOT IN over the nullable column scans per row).
        var all = Sums(Summed(range, null).GetEnumerableTypedResult());
        var namedSums = Sums(namedRows);
        var otherTotals = ToTotals(all.Select((sum, i) => sum - namedSums[i]).ToArray());

        var rows = (sort switch
        {
            QuerySort.NoResults => named.Where(row => row.ZeroResultSearches > 0).OrderByDescending(row => row.ZeroResultSearches),
            QuerySort.LowestCtr => named.OrderBy(row => row.Ctr ?? double.MaxValue),
            QuerySort.Slowest => named.OrderByDescending(row => row.LatencyP95 is null ? -1 : row.LatencyP95.Ms ?? int.MaxValue),
            _ => named.OrderByDescending(row => row.Searches),
        }).ThenBy(row => row.QueryText, StringComparer.Ordinal).ToList();

        return new ReportQueryPage(
            rows.Skip(skip).Take(take).ToList(),
            otherTotals.Searches == 0 ? null : ToRow(null, otherTotals),
            rows.Count,
            store.ReadWatermark());
    }

    public ReportLatency Latency(string indexName, DateOnly from, DateOnly to)
    {
        var range = Validate(indexName, from, to);
        var buckets = ToTotals(Summed(range, null).GetEnumerableTypedResult()).LatencyBuckets;
        return new ReportLatency(
            LatencyPercentiles.Of(buckets, 0.50m),
            LatencyPercentiles.Of(buckets, 0.95m),
            LatencyPercentiles.Of(buckets, 0.99m),
            buckets,
            store.ReadWatermark());
    }

    internal ReportRange Validate(string indexName, DateOnly from, DateOnly to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        if (from > to)
        {
            throw new ArgumentException($"From ({from:yyyy-MM-dd}) must not be after To ({to:yyyy-MM-dd}).", nameof(from));
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (to > today)
        {
            throw new ArgumentException($"To ({to:yyyy-MM-dd}) must not be after today ({today:yyyy-MM-dd}, UTC).", nameof(to));
        }

        var range = new ReportRange(indexName, from, to);
        int maxDays = (int)options.Value.Analytics.Retention.DailyRetention.TotalDays;
        if (range.Days > maxDays)
        {
            throw new ArgumentException($"The range has {range.Days} days; at most {maxDays} (Retention:DailyRetention) are allowed.", nameof(from));
        }

        return range;
    }

    internal static ReportDeltas Deltas(ReportTotals current, ReportTotals previous, bool previousIsPartial)
    {
        double? D(double? now, double? before) => previousIsPartial || now is null || before is null or 0 ? null : (now - before) / before;

        return new ReportDeltas(
            D(current.Searches, previous.Searches),
            D(current.ZeroResultSearches, previous.ZeroResultSearches),
            D(current.ClickedSearches, previous.ClickedSearches),
            D(current.Clicks, previous.Clicks),
            D(current.Conversions, previous.Conversions),
            D(current.SessionDays, previous.SessionDays),
            D(current.NoResultRate, previous.NoResultRate),
            D(current.Ctr, previous.Ctr),
            D(current.ConversionRate, previous.ConversionRate),
            D(current.AvgClickPosition, previous.AvgClickPosition));
    }

    internal static ReportTotals ToTotals(IEnumerable<SearchDailyInfo> rows) => ToTotals(Sums(rows));

    private static ReportTotals ToTotals(long[] sums) =>
        new(sums[0], sums[1], sums[2], sums[3], sums[4], sums[5], sums[6], sums[7], sums[8], sums[9..]);

    private static long[] Sums(IEnumerable<SearchDailyInfo> rows)
    {
        var sums = new long[SummedColumns.Length];
        foreach (var row in rows)
        {
            for (int i = 0; i < sums.Length; i++)
            {
                // SUM over no rows is NULL, read as 0.
                sums[i] += ValidationHelper.GetLong(row.GetValue(SummedColumns[i]), 0, CultureInfo.InvariantCulture);
            }
        }

        return sums;
    }

    private static ReportQueryRow ToRow(string? queryText, ReportTotals totals) => new(
        queryText,
        queryText is { Length: 0 },
        totals.Searches,
        totals.ZeroResultSearches,
        totals.ClickedSearches,
        totals.Clicks,
        totals.Ctr,
        totals.NoResultRate,
        totals.AvgClickPosition,
        LatencyPercentiles.Of(totals.LatencyBuckets, 0.50m),
        LatencyPercentiles.Of(totals.LatencyBuckets, 0.95m));

    private ObjectQuery<SearchDailyInfo> Summed(ReportRange range, string? groupBy)
    {
        var sums = SummedColumns.Select(column => (IQueryColumn)new AggregatedColumn(AggregationType.Sum, column).As(column));
        var query = daily.Get()
            .WhereEquals(nameof(SearchDailyInfo.IndexName), range.IndexName)
            .WhereGreaterOrEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(range.From))
            .WhereLessOrEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(range.To));
        return groupBy is null
            ? query.Columns(sums.ToArray())
            : query.Columns([new QueryColumn(groupBy), .. sums]).GroupBy(groupBy);
    }
}
