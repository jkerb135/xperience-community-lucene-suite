using System.Diagnostics;
using System.Globalization;

using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit.Abstractions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Reports.Insights;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SearchInsightDatabaseTests(ITestOutputHelper output)
{
    internal const string Index = "insight-test";
    internal static readonly DateOnly From = new(2030, 1, 21);
    internal static readonly DateOnly To = new(2030, 1, 30);

    [DatabaseFact]
    public void Aggregate_SplitsCurrentAndPrevious_MergesCollationEqualTexts_AndKeepsOnlySurfacedTypedQueries()
    {
        Seed();

        var (rows, truncated) = Service().Aggregate(new ReportRange(Index, From, To));

        Assert.False(truncated);
        // gamma (3 current searches) is below MinSearches; nonsurf, browse, (other) and the other index never appear.
        Assert.Equal(["alpha", "delta", "beta"], rows.Select(row => row.QueryText.ToLowerInvariant()));
        var alpha = rows[0];
        Assert.Equal((20L, 10L, 0L, 20L, 0L, 10L), (alpha.Searches, alpha.ZeroResultSearches, alpha.ClickedSearches, alpha.PreviousSearches, alpha.PreviousZeroResultSearches, alpha.PreviousClickedSearches));
        Assert.Equal(new LatencyPercentile(50, false), alpha.LatencyP95);
        Assert.Equal((0L, 0L), (rows[2].PreviousSearches, rows[2].PreviousClickedSearches));
    }

    [DatabaseFact]
    public void Aggregate_KeepsTheTopMaxQueries_AndFlagsTruncation()
    {
        Seed();
        var range = new ReportRange(Index, From, To);

        var (rows, truncated) = Service(o => o.Analytics.Insights.MaxQueries = 2).Aggregate(range);
        Assert.True(truncated);
        Assert.Equal(["alpha", "delta"], rows.Select(row => row.QueryText.ToLowerInvariant()));

        (rows, truncated) = Service(o => o.Analytics.Insights.MaxQueries = 3).Aggregate(range);
        Assert.False(truncated);
        Assert.Equal(3, rows.Count);

        (rows, _) = Service(o => o.Analytics.Insights.MinSearches = 12).Aggregate(range);
        Assert.Equal(["alpha", "delta"], rows.Select(row => row.QueryText.ToLowerInvariant()));
    }

    [DatabaseFact]
    public void Aggregate_MatchesTheReportQueryRows()
    {
        Seed();

        var (rows, _) = Service().Aggregate(new ReportRange(Index, From, To));
        var report = Reports(Options.Create(new SuiteSearchOptions())).Queries(Index, From, To, QuerySort.Searches, 0, 200).Rows
            .ToDictionary(row => row.QueryText!, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(3, rows.Count);
        foreach (var row in rows)
        {
            var expected = report[row.QueryText];
            Assert.Equal(
                (expected.Searches, expected.ZeroResultSearches, expected.ClickedSearches, expected.Clicks, expected.Ctr, expected.NoResultRate, expected.AvgClickPosition, expected.LatencyP95),
                (row.Searches, row.ZeroResultSearches, row.ClickedSearches, row.Clicks, row.Ctr, row.NoResultRate, row.AvgClickPosition, row.LatencyP95));
        }
    }

    [DatabaseFact]
    public void AggregateQuery_HasNoClientTextInItsSql_AndTakesTheRangeAsParameters()
    {
        const string hostile = "x' OR 1=1; DROP TABLE LuceneSuite_SearchDaily; --";
        var query = Service().AggregateQuery(new ReportRange(hostile, From, To));

        string sql = query.ToString(false);
        output.WriteLine(sql);
        Assert.DoesNotContain("DROP", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("2030", sql, StringComparison.Ordinal);
        Assert.Contains("[Day] >= @InsightFrom", sql, StringComparison.Ordinal);
        Assert.Equal(RollupScheduler.Midnight(From), query.Parameters["@InsightFrom"].Value);
    }

    [DatabaseFact]
    public void Insights_RankTheFindings_WithRangeSemanticsOfTheReports()
    {
        Seed();

        var report = Service().Insights(Index, From, To);

        Assert.Equal(
            ["BuriedAnswer delta 11", "ContentGap alpha 10", "SlowQuery beta 10", "Regression alpha 10", "RelevanceMiss beta 9"],
            report.Findings.Select(f => $"{f.Kind} {f.QueryText.ToLowerInvariant()} {f.Affected}"));
        Assert.Equal((0, 3, false, false, false), (report.Omitted, report.Considered, report.Truncated, report.PreviousIsPartial, report.AnnotationsFailed));
        Assert.Equal(new DateOnly(2030, 1, 25), report.CompleteThrough);
        var regression = report.Findings.Single(f => f.Kind == InsightKind.Regression);
        Assert.Equal((0.5, 0.0, 0.0, 0.5), (regression.NoResultRate, regression.Ctr, regression.PreviousNoResultRate, regression.PreviousCtr));

        var capped = Service(o => o.Analytics.Insights.MaxFindings = 2).Insights(Index, From, To);
        Assert.Equal((2, 3), (capped.Findings.Count, capped.Omitted));
    }

    [DatabaseFact]
    public void Insights_WhenThePreviousPeriodIsPartial_ReportNoRegression()
    {
        Seed();

        // 30 days → previous 2029-12-02..2029-12-31, before the first data day (2030-01-05).
        var report = Service().Insights(Index, new DateOnly(2030, 1, 1), To);

        Assert.True(report.PreviousIsPartial);
        Assert.Contains(report.Findings, f => f.Kind == InsightKind.ContentGap);
        Assert.DoesNotContain(report.Findings, f => f.Kind == InsightKind.Regression);
    }

    [DatabaseFact]
    public void Insights_ListTheSynonymsAndRulesThatAlreadyMatch_ButNotFacetOrContextRules()
    {
        Seed();
        var services = KenticoServices.Value;
        var rules = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        var synonyms = services.GetRequiredService<IInfoProvider<SynonymInfo>>();
        CleanupFixes();
        try
        {
            var synonym = new SynonymInfo { IndexName = Index, Kind = SynonymSerializer.TwoWay, Input = string.Empty, Terms = """["alpha","alfa"]""", Enabled = true };
            synonyms.Set(synonym);
            var fires = Rule("Alpha fix", """[{"anchoring":"contains","pattern":"alpha"}]""");
            var betaRule = Rule("Beta fix", """[{"anchoring":"is","pattern":"beta"}]""");
            rules.Set(fires);
            rules.Set(betaRule);
            rules.Set(Rule("Facet only", """[{"anchoring":"is","pattern":"alpha","facets":[{"dimension":"d","value":"v"}]}]"""));
            rules.Set(Rule("Context only", """[{"anchoring":"is","pattern":"alpha","contexts":["vip"]}]"""));
            var disabled = Rule("Off", """[{"anchoring":"is","pattern":"alpha"}]""");
            disabled.Enabled = false;
            rules.Set(disabled);

            var report = Service().Insights(Index, From, To);

            Assert.False(report.AnnotationsFailed);
            foreach (var finding in report.Findings)
            {
                var (synonymIds, fired) = finding.QueryText.ToLowerInvariant() switch
                {
                    "alpha" => (new[] { synonym.SynonymID }, new[] { new InsightRule(fires.RuleID, "Alpha fix") }),
                    "beta" => ([], [new InsightRule(betaRule.RuleID, "Beta fix")]),
                    _ => (Array.Empty<int>(), Array.Empty<InsightRule>()),
                };
                Assert.Equal(synonymIds, finding.AppliedSynonymIds);
                Assert.Equal(fired, finding.FiredRules);
            }
        }
        finally
        {
            CleanupFixes();
        }
    }

    [DatabaseFact]
    public void Insights_WhenRulesFailToLoad_KeepTheFindingsWithoutAnnotations()
    {
        Seed();
        var logger = new RecordingLogger<SearchInsightService>();

        var report = Service(ruleProvider: new FailingRuleProvider(), logger: logger).Insights(Index, From, To);

        Assert.True(report.AnnotationsFailed);
        Assert.Equal(5, report.Findings.Count);
        Assert.All(report.Findings, f => Assert.Empty(f.FiredRules));
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [DatabaseFact]
    public void Insights_WriteNothing()
    {
        Seed();
        const string counts =
            """
            SELECT (SELECT COUNT(*) FROM LuceneSuite_SearchDaily) + (SELECT COUNT(*) FROM LuceneSuite_SearchEvent) + (SELECT COUNT(*) FROM LuceneSuite_SearchInteraction)
                + (SELECT COUNT(*) FROM LuceneSuite_Rule) + (SELECT COUNT(*) FROM LuceneSuite_Synonym)
            """;
        int before = Scalar(counts);
        string checksum = Checksum();

        Service().Insights(Index, From, To);

        Assert.Equal(before, Scalar(counts));
        Assert.Equal(checksum, Checksum());
    }

    [VolumeFact(database: true)]
    public void Insights_Over400DaysOf500Queries_With500RulesAnd200Synonyms_TakeAtMost200Ms()
    {
        RollupDb.Reset();
        CleanupFixes();
        var last = new DateOnly(2030, 1, 30);
        var random = new Random(7);
        var services = KenticoServices.Value;
        var provider = services.GetRequiredService<IInfoProvider<SearchDailyInfo>>();
        var insert = Stopwatch.StartNew();
        for (int day = 0; day < 400; day++)
        {
            provider.BulkInsert(Enumerable.Range(0, 500).Select(query =>
                Row(last.AddDays(-day), $"query {query:D3}", random.Next(1, 20), zero: random.Next(0, 8), clicked: 1, clicks: 1, positions: random.Next(1, 10), bucket: random.Next(0, 10))));
        }

        var rules = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        for (int i = 0; i < 500; i++)
        {
            rules.Set(Rule($"Rule {i}", $$"""[{"anchoring":"contains","pattern":"query {{i:D3}}"}]"""));
        }

        var synonyms = services.GetRequiredService<IInfoProvider<SynonymInfo>>();
        for (int i = 0; i < 200; i++)
        {
            synonyms.Set(new SynonymInfo { IndexName = Index, Kind = SynonymSerializer.TwoWay, Input = string.Empty, Terms = $"""["{i:D3}","n{i:D3}"]""", Enabled = true });
        }

        output.WriteLine($"setup: 200,000 rows, 500 rules, 200 synonyms in {insert.Elapsed.TotalSeconds:N1} s");
        try
        {
            var service = Service(o => o.Analytics.Insights.MinSearches = 20);
            var from = last.AddDays(-199);
            var cold = Stopwatch.StartNew();
            var report = service.Insights(Index, from, last);
            output.WriteLine($"first call (loads rules and synonyms): {cold.Elapsed.TotalMilliseconds:N0} ms");
            var watch = Stopwatch.StartNew();
            report = service.Insights(Index, from, last);
            watch.Stop();
            output.WriteLine($"insights: {watch.Elapsed.TotalMilliseconds:N0} ms; {report.Considered} queries, {report.Findings.Count} findings (+{report.Omitted} omitted), {report.Findings.Sum(f => f.FiredRules.Count)} fired-rule and {report.Findings.Sum(f => f.AppliedSynonymIds.Count)} synonym annotations");
            Assert.False(report.AnnotationsFailed);
            Assert.Contains(report.Findings, f => f.FiredRules.Count > 0 && f.AppliedSynonymIds.Count > 0);
            Assert.True(watch.Elapsed <= TimeSpan.FromMilliseconds(200), $"Insights took {watch.Elapsed}.");
        }
        finally
        {
            RollupDb.Reset();
            CleanupFixes();
        }
    }

    internal static string Checksum()
    {
        using var connection = Open();
        using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM LuceneSuite_SearchDaily", connection);
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;
    }

    internal static RuleInfo Rule(string name, string conditions) => new()
    {
        IndexName = Index,
        DisplayName = name,
        Enabled = true,
        Priority = 1,
        Conditions = conditions,
        Actions = $$"""{"hides":["{{Guid.NewGuid()}}"]}""",
    };

    internal static void CleanupFixes()
    {
        Execute($"DELETE FROM LuceneSuite_Rule WHERE IndexName = '{Index}'; DELETE FROM LuceneSuite_Synonym WHERE IndexName = '{Index}'");
        CacheHelper.TouchKey($"{RuleInfo.OBJECT_TYPE}|all");
        CacheHelper.TouchKey($"{SynonymInfo.OBJECT_TYPE}|all");
    }

    private static SearchReportService Reports(IOptions<SuiteSearchOptions> options) => new(
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
        RollupDb.Store(),
        options,
        Clock());

    private static ManualClock Clock() => new(DateTimeOffset.Parse("2030-01-31T12:00:00Z", CultureInfo.InvariantCulture));

    internal static SearchInsightService Service(
        Action<SuiteSearchOptions>? configure = null, RuleProvider? ruleProvider = null, ILogger<SearchInsightService>? logger = null)
    {
        var settings = new SuiteSearchOptions();
        settings.Analytics.Insights.MinSearches = 5;
        settings.Analytics.Insights.MinAffected = 1;
        configure?.Invoke(settings);
        var options = Options.Create(settings);
        var services = KenticoServices.Value;
        var cache = services.GetRequiredService<IProgressiveCache>();
        return new SearchInsightService(
            services.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            services.GetRequiredService<IInfoProvider<RuleInfo>>(),
            Reports(options),
            RollupDb.Store(),
            ruleProvider ?? new RuleProvider(services.GetRequiredService<IInfoProvider<RuleInfo>>(), cache, options, TimeProvider.System, NullLogger<RuleProvider>.Instance),
            new SynonymProvider(services.GetRequiredService<IInfoProvider<SynonymInfo>>(), cache, options, TimeProvider.System, NullLogger<SynonymProvider>.Instance),
            options,
            Clock(),
            logger ?? NullLogger<SearchInsightService>.Instance);
    }

    internal static void Seed()
    {
        RollupDb.Reset();
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(
        [
            // Before both periods: the first data day, so the previous period is complete; never summed.
            Row("2030-01-05", "alpha", 50, zero: 50),
            // Content gap that regressed: case variants on different days are one query under the collation.
            Row("2030-01-21", "alpha", 10, zero: 5, bucket: 2),
            Row("2030-01-22", "ALPHA", 10, zero: 5, bucket: 2),
            Row("2030-01-15", "alpha", 10, clicked: 5, clicks: 5, positions: 5, bucket: 2),
            Row("2030-01-16", "Alpha", 10, clicked: 5, clicks: 5, positions: 5, bucket: 2),
            // Relevance miss and slow (overflow p95).
            Row("2030-01-23", "beta", 8, bucket: 9),
            Row("2030-01-24", "beta", 2, clicked: 1, clicks: 1, positions: 7, bucket: 9),
            // Buried answer: average position 66 / 12 = 5.5.
            Row("2030-01-25", "delta", 6, clicked: 6, clicks: 6, positions: 36, bucket: 3),
            Row("2030-01-26", "delta", 6, clicked: 5, clicks: 6, positions: 30, bucket: 4),
            // Surfaced through the previous period, but only 3 searches in the range.
            Row("2030-01-27", "gamma", 3, zero: 3),
            Row("2030-01-12", "gamma", 5),
            Row("2030-01-13", "gamma", 5),
            // One day only: not surfaced.
            Row("2030-01-21", "nonsurf", 9, zero: 9),
            Row("2030-01-21", "", 10, zero: 10),
            Row("2030-01-22", "", 10, zero: 10),
            Row("2030-01-21", null, 10, zero: 10),
            Row("2030-01-21", "alpha", 100, zero: 100, index: "insight-other"),
            Row("2030-01-22", "alpha", 100, zero: 100, index: "insight-other"),
        ]);
        Execute($"UPDATE CMS_SettingsKey SET KeyValue = '2030-01-25' WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}'");
    }

    private static SearchDailyInfo Row(
        string day, string? queryText, int searches, int zero = 0, int clicked = 0, int clicks = 0, long positions = 0, int bucket = 0, string index = Index) =>
        Row(DateOnly.Parse(day, CultureInfo.InvariantCulture), queryText, searches, zero, clicked, clicks, positions, bucket, index);

    private static SearchDailyInfo Row(
        DateOnly day, string? queryText, int searches, int zero = 0, int clicked = 0, int clicks = 0, long positions = 0, int bucket = 0, string index = Index)
    {
        var buckets = new int[SearchDailyInfo.LatencyBucketCount];
        buckets[bucket] = searches;
        var info = DailyRollupStore.ToInfo(new DailyKpi(day, index, queryText ?? string.Empty, searches, zero, searches, clicked, clicks, positions, 0, 0, 1, buckets));
        if (queryText is null)
        {
            info.SetValue(nameof(SearchDailyInfo.QueryText), null);
        }

        return info;
    }

    private sealed class FailingRuleProvider() : RuleProvider(null!, null!, Options.Create(new SuiteSearchOptions()), TimeProvider.System, NullLogger<RuleProvider>.Instance)
    {
        public override IReadOnlyList<RuleDefinition> GetRules(string indexName) => throw new InvalidOperationException("rule store down");
    }
}
