using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests.Seeding;

[Collection(DatabaseCollection.Name)]
public sealed class SearchSeederDatabaseTests
{
    private const string Index = "seed-test";
    private static readonly DateOnly Today = new(2030, 3, 1);
    private static readonly SuiteAnalyticsOptions Options = new();

    private static string ConnectionString => Environment.GetEnvironmentVariable(DatabaseFactAttribute.Variable)!;

    [DatabaseFact]
    public void Range_EndsBeforeTheEarliestOfToday_Watermark_RawAndDaily()
    {
        RollupDb.Reset();
        Assert.Equal(new DateOnly(2030, 2, 28), Range().To);

        SetWatermark("2030-02-20");
        Assert.Equal(new DateOnly(2030, 2, 20), Range().To);

        RollupDb.Insert(new SyntheticTraffic([new RawSearchRow(1, Guid.NewGuid(), 1, null, new DateTime(2030, 2, 15, 8, 0, 0, DateTimeKind.Utc), Index, "tea", 1, 10, 3, 20, true)], []));
        Assert.Equal(new DateOnly(2030, 2, 14), Range().To);

        InsertDaily(Kpi(new DateOnly(2030, 2, 1), "other-index"));
        Assert.Equal(new DateOnly(2030, 2, 14), Range().To);
        InsertDaily(Kpi(new DateOnly(2030, 2, 10), Index));
        Assert.Equal((new DateOnly(2030, 1, 31), new DateOnly(2030, 2, 9)), Range(days: 10));
    }

    [DatabaseFact]
    public void Seed_WritesOnlyTheRange_LeavesWatermarkRawAndOtherIndexes_AndReportsWork()
    {
        RollupDb.Reset();
        InsertDaily(Kpi(new DateOnly(2030, 1, 15), "other-index"));
        var rawBefore = Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent");

        var (from, to, rows, itemRows) = Seed(days: 60);

        Assert.Equal((new DateOnly(2029, 12, 31), new DateOnly(2030, 2, 28)), (from, to));
        Assert.Equal(itemRows, Scalar($"SELECT COUNT(*) FROM LuceneSuite_ItemDaily WHERE IndexName = '{Index}' AND Day BETWEEN '20291231' AND '20300228'"));
        Assert.Equal(itemRows, Scalar("SELECT COUNT(*) FROM LuceneSuite_ItemDaily"));
        // Consistency invariant on every seeded day: item clicks and conversions sum to the query KPIs'.
        Assert.Equal(
            0,
            Scalar(
                $"""
                SELECT COUNT(*) FROM (SELECT Day, SUM(Clicks) AS Clicks, SUM(ClickPositionSum) AS Positions, SUM(Conversions) AS Conversions FROM LuceneSuite_SearchDaily WHERE IndexName = '{Index}' GROUP BY Day) q
                FULL JOIN (SELECT Day, SUM(ClickedSearches) AS Clicks, SUM(ClickPositionSum) AS Positions, SUM(Conversions) AS Conversions FROM LuceneSuite_ItemDaily WHERE IndexName = '{Index}' GROUP BY Day) i ON i.Day = q.Day
                WHERE ISNULL(q.Clicks, 0) <> ISNULL(i.Clicks, 0) OR ISNULL(q.Positions, 0) <> ISNULL(i.Positions, 0) OR ISNULL(q.Conversions, 0) <> ISNULL(i.Conversions, 0)
                """));
        Assert.True(itemRows > 60 && Scalar($"SELECT COUNT(*) FROM LuceneSuite_ItemDaily WHERE Conversions > 0") > 0, "seeded items are non-trivial");
        // Clicked and converted items are the vocabulary's documents (BuiltIn ItemGuids are n-0-0-0…0).
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_ItemDaily WHERE CONVERT(char(36), ItemGuid) NOT LIKE '%-0000-0000-0000-000000000000'"));
        Assert.Equal(rows, Scalar($"SELECT COUNT(*) FROM LuceneSuite_SearchDaily WHERE IndexName = '{Index}' AND Day BETWEEN '20291231' AND '20300228'"));
        Assert.Equal(rows, Scalar($"SELECT COUNT(*) FROM LuceneSuite_SearchDaily WHERE IndexName = '{Index}'"));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchDaily WHERE QueryText IS NULL"));
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchDaily WHERE IndexName = 'other-index'"));
        Assert.Equal(rawBefore, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}' AND KeyValue = ''"));

        var reports = new SearchReportService(
            KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            RollupDb.Store(),
            Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions()),
            new ManualClock(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));
        var overview = reports.Overview(Index, from, to);
        Assert.True(overview.Current.Searches > 0 && overview.Current.Ctr > 0 && overview.Current.NoResultRate > 0);
        Assert.Equal(60, reports.Trend(Index, from, to).Days.Count);
        var queries = reports.Queries(Index, from, to, QuerySort.Searches, 0, 50);
        Assert.NotEmpty(queries.Rows);
        Assert.NotNull(queries.Other);
        var latency = reports.Latency(Index, from, to);
        Assert.True(latency.P95!.IsOverflow || latency.P50!.Ms < latency.P95.Ms);

        // The next seed goes before the seeded history, never into it.
        Assert.Equal(from.AddDays(-1), Seed(days: 5).To);
    }

    [DatabaseFact]
    public void Seed_RefusesRowsOutsideTheRange_AndWritesNothing()
    {
        RollupDb.Reset();

        Assert.Throws<InvalidOperationException>(() => SearchSeeder.Seed(ConnectionString, Index, 5, Today, (_, to) => new DailyRollup([Kpi(to.AddDays(1), Index)], [])));
        Assert.Throws<InvalidOperationException>(() => SearchSeeder.Seed(ConnectionString, Index, 5, Today, (from, _) => new DailyRollup([Kpi(from, Index)], [new ItemKpi(from, "other-index", Guid.NewGuid(), 1, 1, 0)])));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchDaily"));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_ItemDaily"));
    }

    [DatabaseFact]
    public void Seed_WhileTheRollupLockIsHeld_IsRefused()
    {
        RollupDb.Reset();
        using (RollupLock.TryAcquire(ConnectionString))
        {
            Assert.Throws<InvalidOperationException>(() => Seed(days: 5));
        }

        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchDaily"));
    }

    [DatabaseFact]
    public void Clear_RemovesExactlyTheRange_IncludingFoldedOtherRows_AndItemRows()
    {
        RollupDb.Reset();
        var (from, to, rows, itemRows) = Seed(days: 10);
        var other = DailyRollupStore.ToInfo(Kpi(from.AddDays(2), Index));
        other.SetValue(nameof(SearchDailyInfo.QueryText), null);
        InsertDaily(other);
        InsertDaily(Kpi(to.AddDays(1), Index), Kpi(from, "other-index"));
        KenticoServices.Value.GetRequiredService<IInfoProvider<ItemDailyInfo>>().BulkInsert(
            [DailyRollupStore.ToInfo(new ItemKpi(to.AddDays(1), Index, Guid.NewGuid(), 1, 1, 0)), DailyRollupStore.ToInfo(new ItemKpi(from, "other-index", Guid.NewGuid(), 1, 1, 0))]);

        var (deleted, items) = SearchSeeder.Clear(ConnectionString, Index, from, to);

        Assert.Equal(rows + 1, deleted.Values.Sum());
        Assert.Equal(10, deleted.Count);
        Assert.True(itemRows > 0);
        Assert.Equal(itemRows, items);
        Assert.Equal(2, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchDaily"));
        Assert.Equal(2, Scalar("SELECT COUNT(*) FROM LuceneSuite_ItemDaily"));
    }

    private static (DateOnly From, DateOnly To) Range(int days = 60)
    {
        using var connection = Open();
        return SearchSeeder.Range(connection, null, Index, days, Today);
    }

    private static (DateOnly From, DateOnly To, int Rows, int ItemRows) Seed(int days) =>
        SearchSeeder.Seed(ConnectionString, Index, days, Today, (from, to) => SearchSeeder.Roll(SearchSeeder.Generate(SeedVocabulary.BuiltIn(), from, to, 60, 7, Options), Index, Options));

    private static void SetWatermark(string day) =>
        Execute($"UPDATE CMS_SettingsKey SET KeyValue = '{day}' WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}'");

    private static DailyKpi Kpi(DateOnly day, string index) => new(day, index, "fixture", 5, 0, 0, 0, 0, 0, 0, 0, 1, new int[SearchDailyInfo.LatencyBucketCount]);

    private static void InsertDaily(params DailyKpi[] kpis) => InsertDaily(kpis.Select(DailyRollupStore.ToInfo).ToArray());

    private static void InsertDaily(params SearchDailyInfo[] rows) =>
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(rows);
}
