using System.Globalization;

using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;
using XperienceCommunity.Lucene.Suite.Tests.Seeding;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SearchReportsPageDatabaseTests
{
    private const string Index = "reports-page-test";
    private static readonly DateOnly Today = new(2030, 3, 1);

    [DatabaseFact]
    public void Report_MapsEverySectionOfTheService()
    {
        var (from, to) = Seed(days: 30);
        var service = Service();

        var result = SearchReportsPage.Build(service, Index, Arguments(from, to));

        var overview = service.Overview(Index, from, to);
        Assert.Null(result.Error);
        Assert.Equal(("2030-01-30", "2030-02-28", "2029-12-31", "2030-01-29"), (result.From, result.To, result.PreviousFrom, result.PreviousTo));
        Assert.Equal(overview.Current.Searches, result.Current!.Searches);
        Assert.Equal(overview.Current.Ctr, result.Current.Ctr);
        Assert.Equal(overview.Current.SessionDays, result.Current.SessionDays);
        Assert.Equal(0, result.Previous!.Searches);
        Assert.True(result.PreviousIsPartial);
        Assert.Equal("2030-01-30", result.FirstDataDay);
        Assert.Null(result.CompleteThrough);
        Assert.Equal(30, result.Days.Count);
        Assert.Equal(overview.Current.Searches, result.Days.Sum(day => day.Searches));
        Assert.Equal(
            (overview.Current.ConvertedSearches, overview.Current.Clicks, overview.Current.ClickPositionSum, overview.Current.SessionDays),
            (result.Days.Sum(day => day.ConvertedSearches), result.Days.Sum(day => day.Clicks), result.Days.Sum(day => day.ClickPositionSum), result.Days.Sum(day => day.SessionDays)));
        Assert.DoesNotContain(result.Days, day => day.IsComplete);
        Assert.Equal(overview.Current.Searches, result.Latency!.Buckets.Sum());
        Assert.Equal(9, result.Latency.BucketUpperBoundsMs.Count);
        Assert.NotNull(result.Latency.P50);
    }

    [DatabaseFact]
    public void Report_InvalidRanges_ReturnAnErrorInsteadOfThrowing()
    {
        var service = Service();

        Assert.NotNull(SearchReportsPage.Build(service, Index, new SearchReportsArguments { From = "2030-02-01", To = "2030-01-01" }).Error);
        Assert.NotNull(SearchReportsPage.Build(service, Index, new SearchReportsArguments { From = "2030-02-01", To = "2030-03-02" }).Error);
        Assert.NotNull(SearchReportsPage.Build(service, Index, new SearchReportsArguments { From = "2030-02-01", To = null }).Error);
        Assert.NotNull(SearchReportsPage.Build(service, Index, new SearchReportsArguments { From = "1/2/2030", To = "2030-02-01" }).Error);
        Assert.Null(SearchReportsPage.Build(service, Index, new SearchReportsArguments { From = "2030-02-01", To = "2030-02-01" }).Error);
    }

    [DatabaseFact]
    public void Queries_MapEverySortAndPage_OfTheService()
    {
        var (from, to) = Seed(days: 30);
        var service = Service();

        foreach (var sort in Enum.GetValues<QuerySort>())
        {
            var expected = service.Queries(Index, from, to, sort, 0, SearchReportsPage.QueryPageSize);
            var result = SearchReportsPage.BuildQueries(service, Index, Queries(from, to, sort.ToString().ToLowerInvariant(), 1));

            Assert.Null(result.Error);
            Assert.Equal(expected.Rows.Select(row => (row.QueryText, row.Searches)), result.Rows.Select(row => (row.QueryText, row.Searches)));
            Assert.Equal(expected.TotalCount, result.TotalCount);
            Assert.Equal(expected.Other?.Searches, result.Other?.Searches);
            Assert.Equal(SearchReportsPage.QueryPageSize, result.PageSize);
        }

        var second = SearchReportsPage.BuildQueries(service, Index, Queries(from, to, "Searches", 2));
        var expectedSecond = service.Queries(Index, from, to, QuerySort.Searches, SearchReportsPage.QueryPageSize, SearchReportsPage.QueryPageSize);
        Assert.Equal(expectedSecond.Rows.Select(row => row.QueryText), second.Rows.Select(row => row.QueryText));

        var beyond = SearchReportsPage.BuildQueries(service, Index, Queries(from, to, "Searches", 1000));
        Assert.Null(beyond.Error);
        Assert.Empty(beyond.Rows);
        Assert.Equal(expectedSecond.TotalCount, beyond.TotalCount);
    }

    [DatabaseFact]
    public void Queries_InvalidInput_ReturnsAnError_AndPlaceholderTextsAreFlagged()
    {
        var (from, to) = Seed(days: 30);
        var service = Service();

        Assert.NotNull(SearchReportsPage.BuildQueries(service, Index, Queries(from, to, "popular", 1)).Error);
        Assert.NotNull(SearchReportsPage.BuildQueries(service, Index, Queries(from, to, "0", 1)).Error);
        Assert.NotNull(SearchReportsPage.BuildQueries(service, Index, Queries(from, to, "Searches", 0)).Error);
        Assert.NotNull(SearchReportsPage.BuildQueries(service, Index, Queries(to, from, "Searches", 1)).Error);

        // Surfaced (2 days, top of the Searches sort) and containing the scrubber's number placeholder.
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(
            new[] { from, from.AddDays(1) }.Select(day => DailyRollupStore.ToInfo(new DailyKpi(day, Index, "order {number}", 50, 0, 0, 0, 0, 0, 0, 0, 1, new int[SearchDailyInfo.LatencyBucketCount]))));

        var rows = SearchReportsPage.BuildQueries(service, Index, Queries(from, to, "Searches", 1)).Rows;
        Assert.True(Assert.Single(rows, row => row.QueryText == "order {number}").HasPlaceholder);
        Assert.DoesNotContain(rows, row => row.QueryText != "order {number}" && row.HasPlaceholder);
    }

    [DatabaseFact]
    public void Insights_MapTheFindings_WithEditUrlsOfTheFixesThatApply_AndWriteNothing()
    {
        SearchInsightDatabaseTests.Seed();
        var services = KenticoServices.Value;
        SearchInsightDatabaseTests.CleanupFixes();
        try
        {
            var synonym = new SynonymInfo { IndexName = SearchInsightDatabaseTests.Index, Kind = SynonymSerializer.TwoWay, Input = string.Empty, Terms = """["alpha","alfa"]""", Enabled = true };
            services.GetRequiredService<IInfoProvider<SynonymInfo>>().Set(synonym);
            var rule = SearchInsightDatabaseTests.Rule("Alpha fix", """[{"anchoring":"contains","pattern":"alpha"}]""");
            services.GetRequiredService<IInfoProvider<RuleInfo>>().Set(rule);
            const string counts =
                """
                SELECT (SELECT COUNT(*) FROM LuceneSuite_SearchDaily) + (SELECT COUNT(*) FROM LuceneSuite_SearchEvent) + (SELECT COUNT(*) FROM LuceneSuite_SearchInteraction)
                    + (SELECT COUNT(*) FROM LuceneSuite_Rule) + (SELECT COUNT(*) FROM LuceneSuite_Synonym)
                """;
            int before = Scalar(counts);
            string checksum = SearchInsightDatabaseTests.Checksum();

            var result = SearchReportsPage.BuildInsights(
                SearchInsightDatabaseTests.Service(), id => $"rule/{id}", id => $"synonym/{id}", SearchInsightDatabaseTests.Index, Arguments(SearchInsightDatabaseTests.From, SearchInsightDatabaseTests.To));

            Assert.Equal((before, checksum), (Scalar(counts), SearchInsightDatabaseTests.Checksum()));
            Assert.Null(result.Error);
            Assert.Equal(
                ["BuriedAnswer delta 11", "ContentGap alpha 10", "SlowQuery beta 10", "Regression alpha 10", "RelevanceMiss beta 9"],
                result.Findings.Select(f => $"{f.Kind} {f.QueryText.ToLowerInvariant()} {f.Affected}"));
            Assert.Equal((0, 3, false, false, false), (result.Omitted, result.Considered, result.Truncated, result.PreviousIsPartial, result.AnnotationsFailed));
            Assert.True(result.Findings.Single(f => f.Kind == "SlowQuery").LatencyP95!.IsOverflow);
            foreach (var finding in result.Findings)
            {
                bool alpha = finding.QueryText.Equals("alpha", StringComparison.OrdinalIgnoreCase);
                Assert.Equal(alpha ? [(synonym.SynonymID, $"synonym/{synonym.SynonymID}")] : [], finding.AppliedSynonyms.Select(s => (s.Id, s.EditUrl)));
                Assert.Equal(alpha ? [(rule.RuleID, "Alpha fix", $"rule/{rule.RuleID}")] : [], finding.FiredRules.Select(r => (r.Id, r.Name, r.EditUrl)));
                Assert.False(finding.HasPlaceholder);
            }
        }
        finally
        {
            SearchInsightDatabaseTests.CleanupFixes();
        }
    }

    [DatabaseFact]
    public void Insights_InvalidRanges_ReturnAnErrorInsteadOfThrowing()
    {
        var service = SearchInsightDatabaseTests.Service();
        string Error(string? from, string? to) =>
            SearchReportsPage.BuildInsights(service, id => "", id => "", SearchInsightDatabaseTests.Index, new SearchReportsArguments { From = from, To = to }).Error!;

        Assert.Equal("From and To must be dates in the yyyy-MM-dd format.", Error("1/2/2030", "2030-01-30"));
        Assert.Equal("From and To must be dates in the yyyy-MM-dd format.", Error("2030-01-01", null));
        Assert.NotNull(Error("2030-01-30", "2030-01-01"));
        Assert.NotNull(Error("2020-01-01", "2030-01-30"));
    }

    private static SearchReportsQueriesArguments Queries(DateOnly from, DateOnly to, string sort, int page) => new()
    {
        From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Sort = sort,
        Page = page,
    };

    private static (DateOnly From, DateOnly To) Seed(int days)
    {
        RollupDb.Reset();
        var options = new SuiteAnalyticsOptions();
        var (from, to, _) = SearchSeeder.Seed(
            Environment.GetEnvironmentVariable(DatabaseFactAttribute.Variable)!,
            Index,
            days,
            Today,
            (first, last) => SearchSeeder.Roll(SearchSeeder.Generate(SeedVocabulary.BuiltIn(), first, last, 40, 3, options), Index, options));
        return (from, to);
    }

    private static SearchReportsArguments Arguments(DateOnly from, DateOnly to) => new()
    {
        From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static SearchReportService Service() => new(
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
        RollupDb.Store(),
        Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions()),
        new ManualClock(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));
}
