using System.Diagnostics;
using System.Globalization;

using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

using Xunit.Abstractions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SearchReportDatabaseTests(ITestOutputHelper output)
{
    private const string Index = "report-test";
    private static readonly DateOnly From = new(2030, 1, 21);
    private static readonly DateOnly To = new(2030, 1, 30);

    [DatabaseFact]
    public void Overview_SumsEveryRowOfTheIndex_AndComparesWithThePreviousPeriod()
    {
        Seed();

        var overview = Service().Overview(Index, From, To);

        var c = overview.Current;
        Assert.Equal((43L, 7L, 43L, 9L, 10L, 16L, 1L, 1L, 18L), (c.Searches, c.ZeroResultSearches, c.SubmittedSearches, c.ClickedSearches, c.Clicks, c.ClickPositionSum, c.ConvertedSearches, c.Conversions, c.SessionDays));
        Assert.Equal([0L, 8, 5, 9, 4, 6, 5, 0, 0, 6], c.LatencyBuckets);
        Assert.Equal((20L, 10L, 2L, 8L), (overview.Previous.Searches, overview.Previous.ClickedSearches, overview.Previous.Conversions, overview.Previous.SessionDays));
        Assert.Equal(7.0 / 43, overview.Current.NoResultRate);
        Assert.Equal(1.6, overview.Current.AvgClickPosition);
        Assert.Equal(1.15, overview.Deltas.Searches!.Value, 10);
        Assert.Equal(0.0, overview.Deltas.Clicks);
        Assert.Equal(-0.5, overview.Deltas.Conversions);
        Assert.Equal(((9.0 / 43) - 0.5) / 0.5, overview.Deltas.Ctr!.Value, 10);
        Assert.Equal((new DateOnly(2030, 1, 11), false, new DateOnly(2030, 1, 25)), (overview.FirstDataDay, overview.PreviousIsPartial, overview.CompleteThrough));
    }

    [DatabaseFact]
    public void Overview_WhenThePreviousPeriodStartsBeforeTheFirstDataDay_IsPartialWithoutDeltas()
    {
        Seed();

        // 11 days → previous 01-09..01-19, before the first row (01-11).
        var overview = Service().Overview(Index, From.AddDays(-1), To);

        Assert.True(overview.PreviousIsPartial);
        Assert.Equal(new ReportDeltas(null, null, null, null, null, null, null, null, null, null), overview.Deltas);
    }

    [DatabaseFact]
    public void Trend_HasEveryDay_WithZeroDaysAndCompletenessFromTheWatermark()
    {
        Seed();

        var trend = Service().Trend(Index, From, To);

        Assert.Equal(
            [
                "2030-01-21 11 2 4 C", "2030-01-22 8 2 1 C", "2030-01-23 1 1 0 C", "2030-01-24 9 0 3 C", "2030-01-25 6 2 0 C",
                "2030-01-26 3 0 0 -", "2030-01-27 0 0 0 -", "2030-01-28 3 0 1 -", "2030-01-29 2 0 0 -", "2030-01-30 0 0 0 -",
            ],
            trend.Days.Select(day => string.Create(CultureInfo.InvariantCulture, $"{day.Day:yyyy-MM-dd} {day.Searches} {day.ZeroResultSearches} {day.ClickedSearches} {(day.IsComplete ? "C" : "-")}")));
        Assert.Null(trend.Days[6].Ctr);
        Assert.Equal(4.0 / 11, trend.Days[0].Ctr);
        Assert.Equal(new DateOnly(2030, 1, 25), trend.CompleteThrough);
    }

    [DatabaseFact]
    public void Trend_BeforeAnyRollup_HasNoCompleteDays()
    {
        Seed(watermark: null);

        var trend = Service().Trend(Index, From, To);

        Assert.Null(trend.CompleteThrough);
        Assert.DoesNotContain(trend.Days, day => day.IsComplete);
    }

    [DatabaseFact]
    public void Queries_NameOnlySurfacedTexts_MergeCaseVariantsUnderTheCollation_AndSumTheRestIntoOther()
    {
        Seed();
        var service = Service();

        var bySearches = service.Queries(Index, From, To, QuerySort.Searches, 0, 200);

        Assert.Equal(["", "coffee", "beta", "surfaced"], Texts(bySearches));
        Assert.Equal([8L, 6L, 5L, 5L], bySearches.Rows.Select(row => row.Searches));
        Assert.Equal(4, bySearches.TotalCount);
        Assert.True(bySearches.Rows[0].IsBrowse);
        Assert.DoesNotContain(bySearches.Rows.Skip(1), row => row.IsBrowse);
        // near-miss (4) + one-day (9) + NULL (6).
        var other = bySearches.Other!;
        Assert.Equal((null, 19L, 6L, 3L), (other.QueryText, other.Searches, other.ZeroResultSearches, other.ClickedSearches));
        Assert.Equal((new LatencyPercentile(250, false), new LatencyPercentile(null, true)), (other.LatencyP50, other.LatencyP95));

        var page = service.Queries(Index, From, To, QuerySort.Searches, 1, 2);
        Assert.Equal(["coffee", "beta"], Texts(page));
        Assert.Equal(4, page.TotalCount);

        var noResults = service.Queries(Index, From, To, QuerySort.NoResults, 0, 200);
        Assert.Equal(["coffee"], Texts(noResults));
        Assert.Equal(1, noResults.TotalCount);

        Assert.Equal(["coffee", "beta", "", "surfaced"], Texts(service.Queries(Index, From, To, QuerySort.LowestCtr, 0, 200)));
        Assert.Equal(["beta", "coffee", "surfaced", ""], Texts(service.Queries(Index, From, To, QuerySort.Slowest, 0, 200)));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Queries(Index, From, To, QuerySort.Searches, 0, 201));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Queries(Index, From, To, QuerySort.Searches, -1, 10));
    }

    [DatabaseFact]
    public void Latency_PercentilesComeFromTheSummedHistogram()
    {
        Seed();

        var latency = Service().Latency(Index, From, To);

        Assert.Equal([0L, 8, 5, 9, 4, 6, 5, 0, 0, 6], latency.Buckets);
        // 43 searches: p50 rank 22 → bucket 3 (≤ 100 ms); p95 rank 41 and p99 rank 43 → overflow.
        Assert.Equal(new LatencyPercentile(100, false), latency.P50);
        Assert.Equal(new LatencyPercentile(null, true), latency.P95);
        Assert.Equal(new LatencyPercentile(null, true), latency.P99);
    }

    [DatabaseFact]
    public void FutureTo_IsRejected()
    {
        Seed();
        Assert.Equal("to", Assert.Throws<ArgumentException>(() => Service().Trend(Index, From, new DateOnly(2030, 2, 1))).ParamName);
    }

    [VolumeFact(database: true)]
    public void EverySection_Over400DaysOf500Queries_TakesUnderOneSecond()
    {
        RollupDb.Reset();
        var last = new DateOnly(2030, 1, 30);
        var random = new Random(7);
        var insert = Stopwatch.StartNew();
        var provider = KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>();
        for (int day = 0; day < 400; day++)
        {
            provider.BulkInsert(Enumerable.Range(0, 500).Select(query =>
                Row(last.AddDays(-day), $"query {query:D3}", random.Next(1, 20), zero: random.Next(0, 2), clicked: random.Next(0, 2), bucket: random.Next(0, 10))));
        }

        output.WriteLine($"setup: 200,000 rows in {insert.Elapsed.TotalSeconds:N1} s");
        try
        {
            var service = Service();
            var from = last.AddDays(-399);
            Time("overview", () => service.Overview(Index, from, last));
            Time("trend", () => service.Trend(Index, from, last));
            Time("queries", () => service.Queries(Index, from, last, QuerySort.Slowest, 0, 50));
            Time("latency", () => service.Latency(Index, from, last));
        }
        finally
        {
            RollupDb.Reset();
        }
    }

    private void Time(string section, Action run)
    {
        run();
        var watch = Stopwatch.StartNew();
        run();
        output.WriteLine($"{section}: {watch.Elapsed.TotalMilliseconds:N0} ms");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"{section} took {watch.Elapsed}.");
    }

    private static List<string> Texts(ReportQueryPage page) =>
        page.Rows.Select(row => row.QueryText!.ToLowerInvariant()).ToList();

    private static SearchReportService Service() => new(
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
        RollupDb.Store(),
        Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions()),
        new ManualClock(DateTimeOffset.Parse("2030-01-31T12:00:00Z", CultureInfo.InvariantCulture)));

    private static void Seed(string? watermark = "2030-01-25")
    {
        RollupDb.Reset();
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(
        [
            Row("2030-01-21", "surfaced", 3, clicked: 2, clicks: 3, positions: 6, converted: 1, conversions: 1, sessions: 2, bucket: 2),
            Row("2030-01-22", "surfaced", 2, clicked: 1, clicks: 1, positions: 1, bucket: 2),
            // Near miss: 4 searches over 3 days.
            Row("2030-01-21", "nearmiss", 1, zero: 1, bucket: 4),
            Row("2030-01-22", "nearmiss", 2, zero: 2, bucket: 4),
            Row("2030-01-23", "nearmiss", 1, zero: 1, bucket: 4),
            // 9 searches on one day only.
            Row("2030-01-24", "oneday", 9, clicked: 3, clicks: 3, positions: 3, sessions: 2, bucket: 3),
            Row("2030-01-25", null, 6, zero: 2, sessions: 3, bucket: 9),
            // Case variants on different days: one query under the case-insensitive collation.
            Row("2030-01-21", "Coffee", 3, zero: 1, bucket: 5),
            Row("2030-01-26", "coffee", 3, bucket: 5),
            Row("2030-01-21", "", 4, clicked: 2, clicks: 2, positions: 4, sessions: 2, bucket: 1),
            Row("2030-01-22", "", 4, bucket: 1),
            Row("2030-01-28", "beta", 3, clicked: 1, clicks: 1, positions: 2, bucket: 6),
            Row("2030-01-29", "beta", 2, bucket: 6),
            // Previous period 01-11..01-20.
            Row("2030-01-11", "surfaced", 10, zero: 1, clicked: 5, clicks: 5, positions: 5, sessions: 4, bucket: 2),
            Row("2030-01-20", "surfaced", 10, zero: 1, clicked: 5, clicks: 5, positions: 10, converted: 1, conversions: 2, sessions: 4, bucket: 2),
            Row("2030-01-21", "surfaced", 100, clicked: 50, index: "report-other", bucket: 0),
        ]);
        if (watermark is not null)
        {
            Execute($"UPDATE CMS_SettingsKey SET KeyValue = '{watermark}' WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}'");
        }
    }

    private static SearchDailyInfo Row(
        string day, string? queryText, int searches, int zero = 0, int clicked = 0, int clicks = 0, long positions = 0,
        int converted = 0, int conversions = 0, int sessions = 1, int bucket = 0, string index = Index) =>
        Row(DateOnly.Parse(day, CultureInfo.InvariantCulture), queryText, searches, zero, clicked, clicks, positions, converted, conversions, sessions, bucket, index);

    private static SearchDailyInfo Row(
        DateOnly day, string? queryText, int searches, int zero = 0, int clicked = 0, int clicks = 0, long positions = 0,
        int converted = 0, int conversions = 0, int sessions = 1, int bucket = 0, string index = Index)
    {
        var buckets = new int[SearchDailyInfo.LatencyBucketCount];
        buckets[bucket] = searches;
        var info = DailyRollupStore.ToInfo(new DailyKpi(day, index, queryText ?? string.Empty, searches, zero, searches, clicked, clicks, positions, converted, conversions, sessions, buckets));
        if (queryText is null)
        {
            info.SetValue(nameof(SearchDailyInfo.QueryText), null);
        }

        return info;
    }
}
