using System.Globalization;

using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class RetentionDatabaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-10T12:00:00Z", CultureInfo.InvariantCulture);

    [DatabaseFact]
    public void RawRowsInsideTheNextDaysInputWindow_SurviveAlthoughOlderThanRawRetention()
    {
        RollupDb.Reset();
        // Watermark 01-07: day 01-08 still needs searches from 01-07 23:00 and interactions from 01-07 22:00, although now − 48 h = 01-08 12:00.
        InsertRaw(searchesAt: ["2030-01-07T22:59:59", "2030-01-07T23:00:00", "2030-01-08T13:00:00"], interactionsAt: ["2030-01-07T21:59:59", "2030-01-07T22:00:00"]);

        RollupDb.Retention().Run(Now, new DateOnly(2030, 1, 7), Options(batchSize: 1));

        Assert.Equal(["2030-01-07T23:00:00", "2030-01-08T13:00:00"], Times("LuceneSuite_SearchEvent"));
        Assert.Equal(["2030-01-07T22:00:00"], Times("LuceneSuite_SearchInteraction"));
    }

    [DatabaseFact]
    public void UpToDateWatermark_PurgesRawRowsOlderThanRawRetention_InBatches()
    {
        RollupDb.Reset();
        InsertRaw(searchesAt: ["2030-01-08T11:00:00", "2030-01-08T11:30:00", "2030-01-08T11:59:59", "2030-01-08T12:00:00"], interactionsAt: ["2030-01-08T11:00:00", "2030-01-08T12:00:00"]);

        RollupDb.Retention().Run(Now, new DateOnly(2030, 1, 9), Options(batchSize: 2));

        Assert.Equal(["2030-01-08T12:00:00"], Times("LuceneSuite_SearchEvent"));
        Assert.Equal(["2030-01-08T12:00:00"], Times("LuceneSuite_SearchInteraction"));
    }

    [DatabaseFact]
    public void StuckWatermark_RawHardLimitStillPurges_AndLogsTheStuckDay()
    {
        RollupDb.Reset();
        // Watermark 10 days back: the normal cut-off would keep everything after 2029-12-31 23:00; the 7-day ceiling is 01-03 12:00.
        InsertRaw(searchesAt: ["2030-01-02T00:00:00", "2030-01-03T11:59:59", "2030-01-03T12:00:00", "2030-01-09T00:00:00"], interactionsAt: ["2030-01-03T11:00:00", "2030-01-04T00:00:00"]);
        var logger = new RecordingLogger<AnalyticsRetention>();

        RollupDb.Retention(logger).Run(Now, new DateOnly(2029, 12, 31), Options());

        Assert.Equal(["2030-01-03T12:00:00", "2030-01-09T00:00:00"], Times("LuceneSuite_SearchEvent"));
        Assert.Equal(["2030-01-04T00:00:00"], Times("LuceneSuite_SearchInteraction"));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("2030-01-01", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public void DailyAndItemRowsOlderThanDailyRetention_ArePurged_InBatches()
    {
        RollupDb.Reset();
        var options = Options(batchSize: 2);
        options.Retention.DailyRetention = TimeSpan.FromDays(100);
        // today 2030-01-10 − 100 days = 2029-10-02.
        InsertDaily(Row("2029-10-01", "popular", 50), Row("2029-10-02", "popular", 50));
        KenticoServices.Value.GetRequiredService<IInfoProvider<ItemDailyInfo>>().BulkInsert(
            new[] { "2029-09-30", "2029-10-01", "2029-10-01", "2029-10-02", "2029-10-03" }
                .Select((day, i) => DailyRollupStore.ToInfo(new ItemKpi(DateOnly.Parse(day, CultureInfo.InvariantCulture), RollupDb.Index, new Guid(i + 1, 0, 0, new byte[8]), 1, 1, 0))));

        RollupDb.Retention().Run(Now, null, options);

        Assert.Equal(["2029-10-02"], RollupDb.Daily().Select(row => row.Split('|')[0]));
        Assert.Equal(["2029-10-02", "2029-10-03"], RollupDb.Items().Select(row => row.Split('|')[0]));
    }

    [DatabaseFact]
    public void SubThresholdQueries_FoldIntoOther_PreservingEveryDayTotal_AndASecondRunIsANoOp()
    {
        RollupDb.Reset();
        var options = Options(batchSize: 2);
        // Prune after 30 days: groups last seen before 2029-12-11 are candidates.
        InsertDaily(
            Row("2029-11-01", "rare", 2),
            Row("2029-11-01", "few", 1),
            Row("2029-11-02", "few", 2),
            Row("2029-11-01", "one day only", 9),
            Row("2029-11-01", "popular", 4),
            Row("2029-11-02", "popular", 4),
            Row("2029-11-01", "", 1),
            Row("2029-11-02", null, 3),
            Row("2029-11-01", "recent rare", 1),
            Row("2030-01-05", "recent rare", 1),
            Row("2029-11-01", "rare", 1, index: "other-index"));
        var before = RollupDb.Daily();

        RollupDb.Retention().Run(Now, null, options);
        var after = RollupDb.Daily();

        Assert.Equal(RollupDb.DayTotals(before), RollupDb.DayTotals(after));
        Assert.Equal(
            [
                "2029-11-01|other-index|<other>",
                "2029-11-01|rollup-test|<other>",
                "2029-11-01|rollup-test|popular",
                "2029-11-01|rollup-test|recent rare",
                "2029-11-01|rollup-test|",
                "2029-11-02|rollup-test|<other>",
                "2029-11-02|rollup-test|popular",
                "2030-01-05|rollup-test|recent rare",
            ],
            after.Select(row => string.Join('|', row.Split('|')[..3])));
        // The existing (other) row of 11-02 (3 searches) absorbed "few" (2).
        Assert.Contains(after, row => row.StartsWith("2029-11-02|rollup-test|<other>|5,", StringComparison.Ordinal));

        RollupDb.Retention().Run(Now, null, options);
        Assert.Equal(after, RollupDb.Daily());
    }

    private static SuiteAnalyticsOptions Options(int batchSize = 500)
    {
        var options = new SuiteAnalyticsOptions { BatchSize = batchSize };
        return options;
    }

    private static void InsertRaw(string[] searchesAt, string[] interactionsAt)
    {
        RollupDb.Insert(new SyntheticTraffic(
            searchesAt.Select((at, i) => new RawSearchRow(i + 1, Guid.NewGuid(), 1, null, Utc(at), RollupDb.Index, "coffee", 1, 10, 1, 5, true)).ToList(),
            interactionsAt.Select((at, i) => new RawInteractionRow(i + 1, Guid.NewGuid(), 1, Guid.NewGuid(), 1, SearchInteractionType.Click, Guid.NewGuid(), Utc(at))).ToList()));
    }

    private static List<string> Times(string table)
    {
        using var connection = Open();
        using var command = new Microsoft.Data.SqlClient.SqlCommand($"SELECT ReceivedUtc FROM {table} ORDER BY ReceivedUtc", connection);
        using var reader = command.ExecuteReader();
        var times = new List<string>();
        while (reader.Read())
        {
            times.Add(reader.GetDateTime(0).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        }

        return times;
    }

    private static SearchDailyInfo Row(string day, string? queryText, int searches, string index = RollupDb.Index)
    {
        // Distinct values per column so a wrong column mapping in the fold changes a total.
        var kpi = new DailyKpi(DateOnly.Parse(day, CultureInfo.InvariantCulture), index, queryText ?? string.Empty, searches, 1, 2, 3, 4, 5, 6, 7, 8, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        var info = DailyRollupStore.ToInfo(kpi);
        if (queryText is null)
        {
            info.SetValue(nameof(SearchDailyInfo.QueryText), null);
        }

        return info;
    }

    private static void InsertDaily(params SearchDailyInfo[] rows) =>
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(rows);

    private static DateTime Utc(string at) => DateTime.Parse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
