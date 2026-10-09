using System.Globalization;

using CMS.DataEngine;
using CMS.Modules;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class RollupJobDatabaseTests
{
    private static readonly DateOnly March1 = new(2030, 3, 1);

    [DatabaseFact]
    public void CmsTransactionScope_RollsBackBulkDeleteAndBulkInsert()
    {
        RollupDb.Reset();
        var daily = KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>();
        daily.BulkInsert([DailyRollupStore.ToInfo(Kpi(March1, "kept"))]);

        using (new CMSTransactionScope())
        {
            daily.BulkDelete(new WhereCondition().WhereEquals(nameof(SearchDailyInfo.IndexName), RollupDb.Index));
            daily.BulkInsert([DailyRollupStore.ToInfo(Kpi(March1, "rolled back"))]);
            // Read on the scope's own connection; another connection would wait for the transaction's locks.
            Assert.Equal(["rolled back"], daily.Get().Column(nameof(SearchDailyInfo.QueryText)).GetListResult<string>());

            // Disposed without Commit.
        }

        Assert.Equal(["kept"], RollupDb.Daily().Select(row => row.Split('|')[2]));
    }

    [DatabaseFact]
    public void RollingADayTwice_YieldsIdenticalRows_MatchingTheEngine()
    {
        RollupDb.Reset();
        var traffic = SyntheticTraffic.Generate(7, [March1], RollupDb.Index);
        RollupDb.Insert(traffic);
        var store = RollupDb.Store();
        var settings = RollupSettings.From(new SuiteAnalyticsOptions());

        store.Replace(March1, store.Compute(March1, settings));
        var first = RollupDb.Daily();
        store.Replace(March1, store.Compute(March1, settings));

        Assert.NotEmpty(first);
        Assert.Equal(first, RollupDb.Daily());
        Assert.Equal(RollupDb.Format(traffic.Expected(March1, settings)), first);
        Assert.Equal(March1, store.ReadWatermark());
    }

    [DatabaseFact]
    public void FailureBeforeCommit_LeavesThePreviousRowsAndTheWatermarkUnchanged()
    {
        RollupDb.Reset();
        RollupDb.Insert(SyntheticTraffic.Generate(8, [March1], RollupDb.Index));
        var store = RollupDb.Store();
        var kpis = store.Compute(March1, RollupSettings.From(new SuiteAnalyticsOptions()));
        store.Replace(March1, kpis);
        var before = RollupDb.Daily();
        store.WriteWatermark(March1.AddDays(-1));

        // A duplicate (Day, Index, Query) makes BulkInsert fail after the delete and the watermark update ran.
        Assert.ThrowsAny<Exception>(() => store.Replace(March1, [.. kpis, kpis[0]]));

        Assert.Equal(before, RollupDb.Daily());
        Assert.Equal(March1.AddDays(-1), store.ReadWatermark());
    }

    [DatabaseFact]
    public void HeldLock_MakesATickSkipWithoutWork()
    {
        RollupDb.Reset();
        RollupDb.Insert(SyntheticTraffic.Generate(9, [March1], RollupDb.Index));
        var job = RollupDb.Job(new ManualClock(At("2030-03-02T03:00:00")));

        using (RollupLock.TryAcquire(ConnectionHelper.ConnectionString) ?? throw new InvalidOperationException("Lock not free."))
        {
            Assert.Null(RollupLock.TryAcquire(ConnectionHelper.ConnectionString));
            var skipped = job.RunOnce(CancellationToken.None);

            Assert.False(skipped.LockAcquired);
            Assert.Empty(RollupDb.Daily());
        }

        var rolled = job.RunOnce(CancellationToken.None);
        Assert.Equal([March1], rolled.Rolled);
        Assert.NotEmpty(RollupDb.Daily());
    }

    [DatabaseFact]
    public void ConcurrentTicks_RollEachDayOnce()
    {
        RollupDb.Reset();
        var days = new[] { March1, March1.AddDays(1), March1.AddDays(2) };
        var traffic = SyntheticTraffic.Generate(10, days, RollupDb.Index);
        RollupDb.Insert(traffic);
        var options = RollupDb.Options();
        // Long enough that 03-01 and 03-02 are still above the completeness floor on 03-04.
        options.Analytics.Retention.RawRetention = options.Analytics.Retention.RawHardLimit = TimeSpan.FromDays(30);
        var clock = new ManualClock(At("2030-03-04T03:00:00"));
        var jobs = Enumerable.Range(0, 4).Select(_ => RollupDb.Job(clock, options)).ToArray();
        using var start = new Barrier(jobs.Length);

        var runs = Task.WhenAll(jobs.Select(job => Task.Run(() =>
        {
            start.SignalAndWait();
            return job.RunOnce(CancellationToken.None);
        }))).GetAwaiter().GetResult();

        Assert.Equal(days, runs.SelectMany(run => run.Rolled).Order());
        Assert.Equal(RollupDb.Format(days.SelectMany(day => traffic.Expected(day, RollupSettings.From(options.Analytics)))), RollupDb.Daily());
    }

    [DatabaseFact]
    public void ResetWatermark_SkipsDaysPastRawRetention_KeepingTheirKpis_AndLogsAnError()
    {
        RollupDb.Reset();
        var traffic = SyntheticTraffic.Generate(11, [March1], RollupDb.Index);
        RollupDb.Insert(traffic);
        var store = RollupDb.Store();
        store.Replace(March1, store.Compute(March1, RollupSettings.From(new SuiteAnalyticsOptions())));
        var before = RollupDb.Daily();
        // Watermark lost (e.g. reset by hand) after the raw rows of 03-01 were partly purged.
        Execute($"UPDATE CMS_SettingsKey SET KeyValue = '' WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}'");
        Execute($"DELETE FROM LuceneSuite_SearchEvent WHERE ReceivedUtc < '2030-03-01T12:00:00'");
        var logger = new RecordingLogger<AnalyticsRollupJob>();

        // 2030-03-04 00:00 − 48 h + 1 h = 03-02 01:00: 03-03 is the first complete day, so 03-01 and 03-02 are skipped.
        var run = RollupDb.Job(new ManualClock(At("2030-03-04T00:00:00")), logger: logger).RunOnce(CancellationToken.None);

        Assert.Equal(March1.AddDays(1), run.SkippedThrough);
        Assert.Equal(before, RollupDb.Daily().Where(row => row.StartsWith("2030-03-01", StringComparison.Ordinal)));
        Assert.Equal(March1.AddDays(1), store.ReadWatermark());
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("2030-03-02", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public void BeforeTheInstallerRecordedTheCurrentVersion_TheJobDoesNothing()
    {
        RollupDb.Reset();
        RollupDb.Insert(SyntheticTraffic.Generate(12, [March1], RollupDb.Index));
        Execute($"UPDATE CMS_SettingsKey SET KeyValue = '2' WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}'");
        try
        {
            var run = RollupDb.Job(new ManualClock(At("2030-03-02T03:00:00"))).RunOnce(CancellationToken.None);

            Assert.Equal((true, false), (run.LockAcquired, run.Installed));
            Assert.Empty(RollupDb.Daily());
            Assert.Null(RollupDb.Store().ReadWatermark());
        }
        finally
        {
            Execute($"UPDATE CMS_SettingsKey SET KeyValue = '{AnalyticsModuleInstaller.Version}' WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}'");
        }
    }

    [DatabaseFact]
    public void QueriesEqualUnderTheDatabaseCollation_AreMergedInsteadOfFailingTheDay()
    {
        RollupDb.Reset();
        var start = RollupScheduler.Midnight(March1).AddHours(9);
        var rows = new[] { "strasse", "straße", "straße", "tea" }
            .Select((text, i) => new RawSearchRow(i + 1, Guid.NewGuid(), 1, null, start.AddMinutes(i), RollupDb.Index, text, 1, 10, 3, 20, true))
            .ToList();
        RollupDb.Insert(new SyntheticTraffic(rows, []));
        var store = RollupDb.Store();

        store.Replace(March1, store.Compute(March1, RollupSettings.From(new SuiteAnalyticsOptions())));

        Assert.Equal(["straße|3", "tea|1"], RollupDb.Daily().Select(row => row.Split('|')).Select(parts => parts[2] + "|" + parts[3].Split(',')[0]));
    }

    [DatabaseFact]
    public void EndToEnd_TwoNodesThreeDays_JobTicksDayByDay()
    {
        RollupDb.Reset();
        var days = new[] { March1, March1.AddDays(1), March1.AddDays(2) };
        var traffic = SyntheticTraffic.Generate(42, days, RollupDb.Index);
        RollupDb.Insert(traffic);
        var options = RollupDb.Options();
        options.Analytics.Retention.SubThresholdPruneAfter = TimeSpan.FromDays(1);
        var settings = RollupSettings.From(options.Analytics);
        var clock = new ManualClock(At("2030-03-01T12:00:00"));
        var job = RollupDb.Job(clock, options);
        var store = RollupDb.Store();

        Assert.Empty(job.RunOnce(CancellationToken.None).Rolled);
        Assert.Null(store.ReadWatermark());

        foreach (var (now, rolled) in new[] { ("2030-03-02T02:05:02", March1), ("2030-03-03T03:00:00", March1.AddDays(1)), ("2030-03-04T03:00:00", March1.AddDays(2)) })
        {
            clock.Now = At(now);
            Assert.Equal([rolled], job.RunOnce(CancellationToken.None).Rolled);
            Assert.Equal(rolled, store.ReadWatermark());
        }

        // A repeated tick at the same time finds nothing new.
        Assert.Empty(job.RunOnce(CancellationToken.None).Rolled);

        var expected = days.SelectMany(day => traffic.Expected(day, settings)).ToList();
        var actual = RollupDb.Daily();
        var expectedRows = RollupDb.Format(expected);

        // Unfolded rows are exactly the engine's; folded ones moved into "(other)".
        Assert.All(actual.Where(row => !row.Contains("|<other>|", StringComparison.Ordinal)), row => Assert.Contains(row, expectedRows));
        Assert.Contains(actual, row => row.Contains("|<other>|", StringComparison.Ordinal));
        Assert.Equal(RollupDb.DayTotals(expectedRows), RollupDb.DayTotals(actual));
        Assert.Equal(
            expectedRows.Where(row => row.Contains("||", StringComparison.Ordinal)),
            actual.Where(row => row.Contains("||", StringComparison.Ordinal)));

        // Retention at 03-04 03:00 with watermark 03-03: searches before min(now − 48 h, 03-04 − 1 h), interactions before min(now − 48 h, 03-04 − 2 h).
        var cut = At("2030-03-02T03:00:00").UtcDateTime;
        Assert.Equal(traffic.Searches.Count(row => row.ReceivedUtc >= cut), Scalar($"SELECT COUNT(*) FROM LuceneSuite_SearchEvent WHERE IndexName = '{RollupDb.Index}'"));
        Assert.Equal(0, Scalar($"SELECT COUNT(*) FROM LuceneSuite_SearchEvent WHERE ReceivedUtc < '2030-03-02T03:00:00'"));
        Assert.Equal(traffic.Interactions.Count(row => row.ReceivedUtc >= cut), Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchInteraction"));
        Assert.True(traffic.Searches.Any(row => row.ReceivedUtc < cut) && traffic.Searches.Any(row => row.ReceivedUtc >= cut));
    }

    private static DailyKpi Kpi(DateOnly day, string queryText, int searches = 1) =>
        new(day, RollupDb.Index, queryText, searches, 0, 0, 0, 0, 0, 0, 0, 0, [searches, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

    private static DateTimeOffset At(string utc) => DateTimeOffset.Parse(utc + "Z", CultureInfo.InvariantCulture);
}

/// <summary>Disposable-database helpers shared by the rollup and retention tests.</summary>
internal static class RollupDb
{
    public const string Index = "rollup-test";

    private const string CountColumns =
        "Searches, ZeroResultSearches, SubmittedSearches, ClickedSearches, Clicks, ClickPositionSum, ConvertedSearches, Conversions, SessionCount, " +
        "LatencyBucket0, LatencyBucket1, LatencyBucket2, LatencyBucket3, LatencyBucket4, LatencyBucket5, LatencyBucket6, LatencyBucket7, LatencyBucket8, LatencyBucket9";

    /// <summary>Installs (v3 + watermark key) and empties every Suite analytics table and the watermark.</summary>
    public static void Reset()
    {
        var services = KenticoServices.Value;
        new AnalyticsModuleInstaller(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            NullLogger<AnalyticsModuleInstaller>.Instance).Install();
        Execute(
            $"""
            DELETE FROM LuceneSuite_SearchEvent; DELETE FROM LuceneSuite_SearchInteraction; DELETE FROM LuceneSuite_SearchDaily;
            UPDATE CMS_SettingsKey SET KeyValue = '' WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}';
            """);
    }

    public static DailyRollupStore Store()
    {
        var services = KenticoServices.Value;
        return new DailyRollupStore(
            services.GetRequiredService<IInfoProvider<SearchEventInfo>>(),
            services.GetRequiredService<IInfoProvider<SearchInteractionInfo>>(),
            services.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>());
    }

    public static AnalyticsRetention Retention(ILogger<AnalyticsRetention>? logger = null)
    {
        var services = KenticoServices.Value;
        return new AnalyticsRetention(
            services.GetRequiredService<IInfoProvider<SearchEventInfo>>(),
            services.GetRequiredService<IInfoProvider<SearchInteractionInfo>>(),
            services.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            logger ?? NullLogger<AnalyticsRetention>.Instance);
    }

    public static SuiteSearchOptions Options() => new();

    public static AnalyticsRollupJob Job(TimeProvider clock, SuiteSearchOptions? options = null, ILogger<AnalyticsRollupJob>? logger = null)
    {
        var provider = new ServiceCollection().AddSingleton(Store()).AddSingleton(Retention()).BuildServiceProvider();
        return new AnalyticsRollupJob(provider, Microsoft.Extensions.Options.Options.Create(options ?? Options()), clock, logger ?? NullLogger<AnalyticsRollupJob>.Instance);
    }

    public static void Insert(SyntheticTraffic traffic)
    {
        var services = KenticoServices.Value;
        services.GetRequiredService<IInfoProvider<SearchEventInfo>>().BulkInsert(traffic.Searches.Select(ToInfo));
        if (traffic.Interactions.Count > 0)
        {
            services.GetRequiredService<IInfoProvider<SearchInteractionInfo>>().BulkInsert(traffic.Interactions.Select(ToInfo));
        }
    }

    /// <summary>All SearchDaily rows as "day|index|query|counts…" ("&lt;other&gt;" for NULL), ordered.</summary>
    public static List<string> Daily()
    {
        using var connection = Open();
        using var command = new SqlCommand($"SELECT Day, IndexName, QueryText, {CountColumns} FROM LuceneSuite_SearchDaily", connection);
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var values = Enumerable.Range(3, reader.FieldCount - 3).Select(i => Convert.ToInt64(reader.GetValue(i), CultureInfo.InvariantCulture));
            rows.Add(Row(DateOnly.FromDateTime(reader.GetDateTime(0)), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), values));
        }

        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    public static List<string> Format(IEnumerable<DailyKpi> kpis)
    {
        var rows = kpis.Select(kpi => Row(
            kpi.Day,
            kpi.IndexName,
            kpi.QueryText,
            new long[] { kpi.Searches, kpi.ZeroResultSearches, kpi.SubmittedSearches, kpi.ClickedSearches, kpi.Clicks, kpi.ClickPositionSum, kpi.ConvertedSearches, kpi.Conversions, kpi.SessionCount }
                .Concat(kpi.LatencyBuckets.Select(bucket => (long)bucket)))).ToList();
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    /// <summary>Per (day, index) sums of every count column.</summary>
    public static List<string> DayTotals(IEnumerable<string> rows) =>
        rows.Select(row => row.Split('|'))
            .GroupBy(parts => parts[0] + "|" + parts[1], StringComparer.Ordinal)
            .Select(group => group.Key + "|" + string.Join(',', Enumerable.Range(0, 19).Select(i => group.Sum(parts => long.Parse(parts[3].Split(',')[i], CultureInfo.InvariantCulture)))))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string Row(DateOnly day, string index, string? query, IEnumerable<long> values) =>
        $"{day:yyyy-MM-dd}|{index}|{query ?? "<other>"}|{string.Join(',', values)}";

    private static SearchEventInfo ToInfo(RawSearchRow row) => new()
    {
        SearchId = row.SearchId,
        Sequence = row.Sequence,
        SessionId = row.SessionId,
        ReceivedUtc = row.ReceivedUtc,
        IndexName = row.IndexName,
        QueryText = row.QueryText,
        Page = row.Page,
        PageSize = row.PageSize,
        FacetFilterCount = 0,
        ResultCount = row.ResultCount,
        LatencyMs = row.LatencyMs,
        IsSubmit = row.IsSubmit,
        Origin = SearchEventOrigin.Client,
    };

    private static SearchInteractionInfo ToInfo(RawInteractionRow row) => new()
    {
        SearchId = row.SearchId,
        Sequence = row.Sequence,
        ItemGuid = row.ItemGuid,
        Position = row.Position,
        Type = row.Type,
        EventId = row.EventId,
        ReceivedUtc = row.ReceivedUtc,
    };
}

/// <summary>Seeded raw traffic from two simulated nodes: typing rows, paging, retried duplicates, a cross-midnight search, clicks inside and beyond the window, conversions.</summary>
internal sealed record SyntheticTraffic(List<RawSearchRow> Searches, List<RawInteractionRow> Interactions)
{
    private static readonly string[] Texts = ["coffee", "coffee grinder", "espresso", "tea", "mug", "decaf", "french press", ""];

    public static SyntheticTraffic Generate(int seed, IEnumerable<DateOnly> days, string index, int searchesPerDay = 40)
    {
        var random = new Random(seed);
        Guid NewGuid()
        {
            var bytes = new byte[16];
            random.NextBytes(bytes);
            return new Guid(bytes);
        }

        var sessions = Enumerable.Range(0, 8).Select(_ => NewGuid()).ToArray();
        var searches = new List<RawSearchRow>();
        var interactions = new List<RawInteractionRow>();
        foreach (var day in days)
        {
            var dayStart = RollupScheduler.Midnight(day);
            for (int i = 0; i < searchesPerDay; i++)
            {
                // The last search of each day starts 10 s before midnight and continues after it.
                var start = i == searchesPerDay - 1 ? dayStart.AddSeconds(86_390) : dayStart.AddSeconds(random.Next(86_000));
                string text = random.Next(6) == 0 ? $"rare {day:MMdd} {i}" : Texts[random.Next(Texts.Length)];
                Guid searchId = NewGuid();
                Guid? session = random.Next(2) == 0 ? sessions[random.Next(sessions.Length)] : null;
                int results = random.Next(4) == 0 ? 0 : random.Next(1, 30);
                var rows = new List<RawSearchRow>();
                if (text.Length > 3)
                {
                    rows.Add(new(0, searchId, 1, session, start, index, text[..3], 1, 10, results, random.Next(1, 3000), false));
                }

                rows.Add(new(0, searchId, rows.Count + 1, session, start.AddSeconds(rows.Count * 20), index, text, 1, 10, results, random.Next(1, 3000), random.Next(2) == 0));
                if (random.Next(5) == 0)
                {
                    rows.Add(rows[^1] with { Sequence = rows.Count + 1, ReceivedUtc = rows[^1].ReceivedUtc.AddSeconds(25), Page = 2 });
                }

                // The other node retried one row.
                if (random.Next(8) == 0)
                {
                    rows.Add(rows[0] with { ReceivedUtc = rows[0].ReceivedUtc.AddSeconds(1) });
                }

                searches.AddRange(rows);
                var last = rows.MaxBy(row => row.Sequence)!;
                if (random.Next(3) == 0)
                {
                    // 1 in 5 clicks arrives after the attribution window.
                    var clickAt = last.ReceivedUtc.AddSeconds(random.Next(5) == 0 ? 7_300 : 15);
                    var click = new RawInteractionRow(0, searchId, last.Sequence, NewGuid(), ((last.Page - 1) * 10) + random.Next(1, 11), SearchInteractionType.Click, NewGuid(), clickAt);
                    interactions.Add(click);
                    if (random.Next(5) == 0)
                    {
                        interactions.Add(click with { ReceivedUtc = clickAt.AddSeconds(2) });
                    }
                }

                if (random.Next(10) == 0)
                {
                    interactions.Add(new(0, searchId, last.Sequence, NewGuid(), null, SearchInteractionType.Conversion, NewGuid(), last.ReceivedUtc.AddMinutes(5)));
                }
            }
        }

        // Interleave the nodes' rows; ids follow insertion order, as the database assigns them.
        var shuffledSearches = searches.OrderBy(_ => random.Next()).Select((row, i) => row with { Id = i + 1 }).ToList();
        var shuffledInteractions = interactions.OrderBy(_ => random.Next()).Select((row, i) => row with { Id = i + 1 }).ToList();
        return new SyntheticTraffic(shuffledSearches, shuffledInteractions);
    }

    /// <summary>The engine's KPIs for <paramref name="day"/> over these rows (the generated texts never collide under the collation).</summary>
    public IReadOnlyList<DailyKpi> Expected(DateOnly day, RollupSettings settings) =>
        RollupEngine.Roll(day, Searches, Interactions, settings);
}

/// <summary>Settable clock.</summary>
internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Logger that keeps every entry.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
