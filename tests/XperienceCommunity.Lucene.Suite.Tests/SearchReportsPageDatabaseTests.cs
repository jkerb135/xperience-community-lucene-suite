using System.Globalization;

using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Search;
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
