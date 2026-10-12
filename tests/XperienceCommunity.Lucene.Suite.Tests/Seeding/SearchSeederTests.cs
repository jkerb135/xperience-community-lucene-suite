using Kentico.Xperience.Lucene.Core;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests.Seeding;

public sealed class SearchSeederTests
{
    private static readonly DateOnly From = new(2030, 3, 4);
    private static readonly DateOnly To = From.AddDays(59);
    private static readonly SuiteAnalyticsOptions Options = new();

    [Fact]
    public void SameSeed_GivesIdenticalKpis()
    {
        var first = Kpis(seed: 11);
        var second = Kpis(seed: 11);

        Assert.Equal(first.Select(Format), second.Select(Format));
        Assert.NotEqual(first.Select(Format), Kpis(seed: 12).Select(Format));
    }

    [Fact]
    public void RolledSearchesPerDay_EqualTheGeneratedLogicalSearches()
    {
        var traffic = Generate(seed: 3);
        var kpis = SearchSeeder.Roll(traffic, "seed", Options);

        foreach (var (day, count) in traffic.LogicalSearches)
        {
            Assert.Equal(count, kpis.Where(kpi => kpi.Day == day).Sum(kpi => kpi.Searches));
        }
    }

    [Fact]
    public void Traffic_LooksLikeASite()
    {
        var traffic = Generate(seed: 5);
        var kpis = SearchSeeder.Roll(traffic, "seed", Options);
        double searches = kpis.Sum(kpi => kpi.Searches);

        double noResults = kpis.Sum(kpi => kpi.ZeroResultSearches) / searches;
        Assert.InRange(noResults, 0.05, 0.25);
        Assert.InRange(kpis.Sum(kpi => kpi.ClickedSearches) / searches, 0.15, 0.45);
        Assert.True(kpis.Sum(kpi => kpi.Conversions) > 0);
        Assert.True(kpis.Sum(kpi => kpi.SessionCount) > 0);
        // 2030-03-04 is a Monday.
        Assert.True(traffic.LogicalSearches[From] > traffic.LogicalSearches[From.AddDays(5)]);
    }

    [Fact]
    public void Clicks_StayOnTheReturnedPage_AndNeverFollowZeroResults()
    {
        var traffic = Generate(seed: 9);
        var rows = traffic.Searches.ToDictionary(row => (row.SearchId, row.Sequence));

        Assert.NotEmpty(traffic.Interactions);
        foreach (var click in traffic.Interactions.Where(i => i.Type == SearchInteractionType.Click))
        {
            var row = rows[(click.SearchId, click.Sequence)];
            Assert.InRange(click.Position!.Value, ((row.Page - 1) * row.PageSize) + 1, Math.Min(row.Page * row.PageSize, row.ResultCount));
        }
    }

    [Fact]
    public void LongTail_SomeQueriesSurface_OthersStayBelowTheThreshold()
    {
        var byText = SearchSeeder.Roll(Generate(seed: 1), "seed", Options)
            .Where(kpi => kpi.QueryText.Length > 0)
            .GroupBy(kpi => kpi.QueryText)
            .Select(group => (Searches: group.Sum(kpi => kpi.Searches), Days: group.Count()))
            .ToList();
        var surfacing = Options.Surfacing;

        Assert.Contains(byText, q => q.Searches >= surfacing.MinSearches && q.Days >= surfacing.MinDistinctDays);
        Assert.Contains(byText, q => q.Searches < surfacing.MinSearches || q.Days < surfacing.MinDistinctDays);
    }

    [Fact]
    public void BuiltInVocabulary_MatchesCaseInsensitively_AndNormalizesTexts()
    {
        var vocabulary = SeedVocabulary.BuiltIn();

        Assert.Equal(vocabulary.Match("espresso"), vocabulary.Match("  ESPRESSO "));
        Assert.Equal(2, vocabulary.Match("espresso").Count);
        Assert.Empty(vocabulary.Match("pizza delivery"));
        Assert.All(vocabulary.Texts, text => Assert.Equal(SeedVocabulary.Normalize(text), text));
        Assert.Equal("a b", SeedVocabulary.Normalize(" A \t B  "));
    }

    [Fact]
    public void FromIndex_ReadsTheNewestGeneration_SkippingTaxonomyFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), $"seed-index-{Guid.NewGuid():N}");
        var plunger = Guid.NewGuid();
        try
        {
            WriteIndex(Path.Combine(root, "i-g0000001-p_True"), (Guid.NewGuid(), "Old generation"));
            WriteIndex(Path.Combine(root, "i-g0000002-p_True"), (plunger, "Coffee Plunger"), (plunger, "Cafetera de émbolo"), (Guid.NewGuid(), "Burr Grinder"));
            System.IO.Directory.CreateDirectory(Path.Combine(root, "i-g0000003-p_True_taxonomy"));

            var vocabulary = SeedVocabulary.FromIndex(root, "Title");

            Assert.Equal(3, vocabulary.DocumentCount);
            Assert.Equal([plunger], vocabulary.Match("PLUNGER"));
            Assert.Contains("coffee plunger", vocabulary.Texts);
            Assert.Contains("grinder", vocabulary.Texts);
            Assert.DoesNotContain("old generation", vocabulary.Texts);
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Confirm_RequiresTheTargetDatabaseName()
    {
        const string connection = "Data Source=localhost;Initial Catalog=demo_db;Integrated Security=True";

        Assert.Throws<InvalidOperationException>(() => SearchSeeder.Confirm(connection, null));
        Assert.Throws<InvalidOperationException>(() => SearchSeeder.Confirm(connection, "DEMO_DB"));
        SearchSeeder.Confirm(connection, "demo_db");
    }

    private static void WriteIndex(string folder, params (Guid ItemGuid, string Title)[] documents)
    {
        using var directory = FSDirectory.Open(folder);
        using var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, new StandardAnalyzer(LuceneVersion.LUCENE_48)));
        foreach (var (itemGuid, title) in documents)
        {
            writer.AddDocument(new Document
            {
                new StringField(BaseDocumentProperties.ITEM_GUID, itemGuid.ToString(), Field.Store.YES),
                new TextField("Title", title, Field.Store.YES),
            });
        }

        writer.Commit();
    }

    private static SeedTraffic Generate(int seed) => SearchSeeder.Generate(SeedVocabulary.BuiltIn(), From, To, 120, seed, Options);

    private static List<Analytics.Rollup.DailyKpi> Kpis(int seed) => SearchSeeder.Roll(Generate(seed), "seed", Options);

    private static string Format(Analytics.Rollup.DailyKpi kpi) =>
        $"{kpi.Day}|{kpi.QueryText}|{kpi.Searches}|{kpi.ZeroResultSearches}|{kpi.ClickedSearches}|{kpi.Clicks}|{kpi.ClickPositionSum}|{kpi.Conversions}|{kpi.SessionCount}|{string.Join(',', kpi.LatencyBuckets)}";
}
