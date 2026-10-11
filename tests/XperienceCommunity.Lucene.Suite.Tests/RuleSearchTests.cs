using System.Diagnostics;
using System.Text.Json;

using Kentico.Xperience.Lucene.Core;

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Facet;
using Lucene.Net.Facet.Taxonomy.Directory;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit.Abstractions;

using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RuleSearchTests(ITestOutputHelper output)
{
    private const string Index = "rules-test";
    private const string Category = "Category";
    private static readonly Analyzer Analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void HiddenItem_IsAbsentFromHitsTotalsAndFacetCounts()
    {
        var index = FacetedIndex(out var guids);
        var hidden = guids[0];

        var baseline = Search(index, Request("drink"), []);
        var ruled = Search(index, Request("drink"), [Rule(1, hides: [hidden])]);

        Assert.Equal(5, baseline.TotalHits);
        Assert.Equal(4, ruled.TotalHits);
        Assert.DoesNotContain(hidden, ruled.Hits.Select(hit => hit.ItemGuid));
        Assert.Equal(2, FacetCount(ruled, "Coffee"));
        Assert.Equal([1], ruled.FiredRuleIds);
    }

    [Fact]
    public void HiddenItem_IsAbsentOnThePlainPath_AndInABrowse()
    {
        var (searcher, a, b) = PlainIndex();

        var text = SearchPlain(searcher, Request("espresso"), [Rule(1, hides: [a])]);
        var browse = SearchPlain(searcher, Request(null), [Rule(1, hides: [a])]);

        Assert.Equal([b], text.Hits.Select(hit => hit.ItemGuid!.Value));
        Assert.Equal(1, text.TotalHits);
        Assert.DoesNotContain(a, browse.Hits.Select(hit => hit.ItemGuid));
    }

    [Theory]
    [InlineData(100, 10.0)]
    [InlineData(-100, 0.1)]
    [InlineData(50, 3.16227766)]
    public void Boost_MultipliesTheScoreByTheFactor(int strength, double factor)
    {
        var (searcher, a, _) = PlainIndex();

        float before = ScoreOf(SearchPlain(searcher, Request("espresso"), []), a);
        float after = ScoreOf(SearchPlain(searcher, Request("espresso"), [Rule(1, boosts: [new("Type", "Machine", strength)])]), a);

        Assert.Equal(before * factor, after, before * factor * 1e-4);
    }

    [Fact]
    public void TwoMatchingBoosts_BothApply_EvenWithTheSameStrength()
    {
        var (searcher, a, _) = PlainIndex();

        float before = ScoreOf(SearchPlain(searcher, Request("espresso"), []), a);
        float after = ScoreOf(SearchPlain(searcher, Request("espresso"), [Rule(1, boosts: [new("Type", "Machine", 50), new("Title", "Espresso Machines", 50)])]), a);

        Assert.Equal(before * 10, after, before * 10 * 1e-4);
    }

    [Fact]
    public void Boost_MatchesExactStringValues_AndAnalyzedTextValues()
    {
        var (searcher, a, b) = PlainIndex();

        var exact = SearchPlain(searcher, Request("espresso"), [Rule(1, boosts: [new("Type", "Grinder", 100)])]);
        var analyzed = SearchPlain(searcher, Request("espresso"), [Rule(1, boosts: [new("Title", "Espresso Grinders", 100)])]);

        Assert.Equal(b, exact.Hits[0].ItemGuid);
        Assert.Equal(b, analyzed.Hits[0].ItemGuid);
        Assert.NotEqual(a, exact.Hits[0].ItemGuid);
    }

    [Fact]
    public void Bury_And_Browse_Reorder()
    {
        var (searcher, a, b) = PlainIndex();

        var browse = SearchPlain(searcher, Request(null), [Rule(1, boosts: [new("Type", "Grinder", 100)])]);
        var buried = SearchPlain(searcher, Request("espresso"), [Rule(1, boosts: [new("Type", "Machine", -100)])]);

        Assert.Equal(b, browse.Hits[0].ItemGuid);
        Assert.Equal(b, buried.Hits[0].ItemGuid);
        Assert.Equal(a, buried.Hits[1].ItemGuid);
    }

    [Fact]
    public void NoRules_RulesOff_AndNoFiredRule_GiveTheUnruledResults()
    {
        var (searcher, _, _) = PlainIndex();
        var rules = new[] { Rule(1, conditions: [new RuleCondition(RuleAnchoring.Is, "tea", [], [])], boosts: [new("Type", "Grinder", 100)]) };

        var baseline = SearchPlain(searcher, Request("espresso"), []);
        var off = SearchPlain(searcher, Request("espresso") with { ApplyRules = false }, [Rule(1, boosts: [new("Type", "Grinder", 100)])]);
        var notFired = SearchPlain(searcher, Request("espresso"), rules);

        foreach (var response in new[] { off, notFired })
        {
            Assert.Equal(Describe(baseline), Describe(response));
            Assert.Empty(response.FiredRuleIds);
            Assert.False(response.RulesFailed);
        }
    }

    [Fact]
    public void FiredRules_AndUserData_AreReturnedInRunOrder_AndContextsAreMatched()
    {
        var (searcher, _, _) = PlainIndex();
        var vipOnly = new RuleCondition(null, null, [], ["vip"]);
        RuleDefinition[] rules =
        [
            Rule(5, priority: 2, userData: """{"n":5}"""),
            Rule(3, priority: 1, userData: """{"n":3}"""),
            Rule(9, priority: 1, conditions: [vipOnly], userData: """{"n":9}"""),
        ];

        var guest = SearchPlain(searcher, Request("espresso"), rules);
        var vip = SearchPlain(searcher, Request("espresso") with { Contexts = ["vip"] }, rules);

        Assert.Equal([3, 5], guest.FiredRuleIds);
        Assert.Equal([3, 9, 5], vip.FiredRuleIds);
        Assert.Equal([3, 9, 5], vip.UserData.Select(data => data.RuleId));
        Assert.Equal(9, vip.UserData[1].Data.GetProperty("n").GetInt32());
    }

    [Fact]
    public void RuleLoadFailure_ReturnsUnruledResults_AndWarnsOncePerMinute()
    {
        var (searcher, _, _) = PlainIndex();
        var logger = new RecordingLogger();
        var service = Service(new FailingRules(), logger);

        var first = service.SearchWithRules(Request("espresso"), executor => executor.Search(Request("espresso"), Analyzer, searcher));
        var second = service.SearchWithRules(Request("espresso"), executor => executor.Search(Request("espresso"), Analyzer, searcher));

        Assert.Equal(2, first.TotalHits);
        Assert.True(first.RulesFailed);
        Assert.True(second.RulesFailed);
        Assert.Single(logger.Warnings);
    }

    [Fact]
    public void RuleApplicationFailure_ReturnsUnruledResults()
    {
        var (searcher, a, _) = PlainIndex();
        var logger = new RecordingLogger();
        var service = Service(new FixedRules([Rule(1, hides: [a], boosts: [new("Type", "Machine", 100)])]), logger);
        int runs = 0;

        // The first (ruled) run fails as Lucene would on an oversized query; the fallback run succeeds.
        var response = service.SearchWithRules(Request("espresso"), executor => runs++ == 0
            ? throw new BooleanQuery.TooManyClausesException()
            : executor.Search(Request("espresso"), Analyzer, searcher));

        Assert.Equal(2, runs);
        Assert.True(response.RulesFailed);
        Assert.Equal(2, response.TotalHits);
        Assert.Empty(response.FiredRuleIds);
        Assert.Single(logger.Warnings);
    }

    [Fact]
    public void InvalidRequest_StillThrows_WithRules()
    {
        var (searcher, a, _) = PlainIndex();
        var service = Service(new FixedRules([Rule(1, hides: [a])]), new RecordingLogger());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.SearchWithRules(Request("espresso"), executor => executor.Search(Request("espresso") with { PageSize = 0 }, Analyzer, searcher)));
    }

    [Fact]
    public void ManyFiredRules_WithManyHidesAndBoosts_StaySearchable()
    {
        var (searcher, a, b) = PlainIndex();
        var rules = Enumerable.Range(1, 500)
            .Select(id => Rule(
                id,
                hides: [.. Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()), .. id == 1 ? new[] { a } : []],
                boosts: [.. Enumerable.Range(0, 50).Select(i => new RuleBoost("Type", $"t{id}-{i}", (id + i) % 200 - 100 is 0 ? 1 : (id + i) % 200 - 100))]))
            .ToArray();
        var logger = new RecordingLogger();

        var response = Service(new FixedRules(rules), logger)
            .SearchWithRules(Request("espresso"), executor => executor.Search(Request("espresso"), Analyzer, searcher));

        Assert.False(response.RulesFailed);
        Assert.Equal([b], response.Hits.Select(hit => hit.ItemGuid!.Value));
        Assert.Single(logger.Warnings);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void RulesOverhead_StaysUnderTenMillisecondsAtP95()
    {
        var guids = Enumerable.Range(0, 5000).Select(_ => Guid.NewGuid()).ToArray();
        var searcher = Build(guids.Select((guid, i) => Document(guid, $"coffee item {i}", i % 3 == 0 ? "Machine" : "Grinder")).ToArray());
        var rules = Enumerable.Range(1, 500)
            .Select(id => Rule(
                id,
                conditions: [new RuleCondition(RuleAnchoring.Contains, id <= 50 ? "coffee" : $"term{id}", [], [])],
                hides: [guids[id]],
                boosts: [new RuleBoost("Type", id % 2 == 0 ? "Machine" : "Grinder", id % 2 == 0 ? 30 : -30)]))
            .ToArray();
        var ruled = Service(new FixedRules(rules), NullLogger<SuiteSearchService>.Instance);
        var unruled = Service(new FixedRules([]), NullLogger<SuiteSearchService>.Instance);
        var request = Request("coffee");

        double P95(SuiteSearchService service)
        {
            for (int i = 0; i < 20; i++)
            {
                service.SearchWithRules(request, executor => executor.Search(request, Analyzer, searcher));
            }

            var timings = new double[200];
            for (int i = 0; i < timings.Length; i++)
            {
                long start = Stopwatch.GetTimestamp();
                service.SearchWithRules(request, executor => executor.Search(request, Analyzer, searcher));
                timings[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            Array.Sort(timings);
            return timings[(int)(timings.Length * 0.95)];
        }

        double overhead = P95(ruled) - P95(unruled);
        output.WriteLine($"p95 rules overhead: {overhead:F2} ms");
        if (!Debugger.IsAttached)
        {
            Assert.True(overhead < 10, $"p95 overhead {overhead:F2} ms");
        }
    }

    private static SuiteSearchService Service(RuleProvider rules, ILogger<SuiteSearchService> logger)
    {
        var options = new SuiteSearchOptions();
        options.SearchFields[Index] = new() { ["Title"] = 3f, ["Content"] = 1f };
        var timeProvider = new FixedTime(Now);
        return new SuiteSearchService(null!, null!, null!, new DefaultSearchQueryFactory(Options.Create(options)), Options.Create(options), rules, timeProvider, logger);
    }

    private static SuiteSearchResponse SearchPlain(IndexSearcher searcher, SuiteSearchRequest request, RuleDefinition[] rules) =>
        Service(new FixedRules(rules), NullLogger<SuiteSearchService>.Instance)
            .SearchWithRules(request, executor => executor.Search(request, Analyzer, searcher));

    private static SuiteSearchResponse Search((IndexSearcher Searcher, DrillSideways DrillSideways, FacetsConfig Config) index, SuiteSearchRequest request, RuleDefinition[] rules) =>
        Service(new FixedRules(rules), NullLogger<SuiteSearchService>.Instance)
            .SearchWithRules(request, executor => executor.Search(request, Analyzer, index.Searcher, index.DrillSideways, index.Config));

    private static SuiteSearchRequest Request(string? query) => new() { IndexName = Index, Query = query, TrackAnalytics = false };

    private static RuleDefinition Rule(
        int id,
        int priority = 1,
        RuleCondition[]? conditions = null,
        Guid[]? hides = null,
        RuleBoost[]? boosts = null,
        string? userData = null) =>
        new(id, Index, priority, true, null, null, conditions ?? [],
            new RuleActions([], hides ?? [], boosts ?? [], userData is null ? null : JsonDocument.Parse(userData).RootElement.Clone()));

    private static (IndexSearcher Searcher, Guid Machine, Guid Grinder) PlainIndex()
    {
        var machine = Guid.NewGuid();
        var grinder = Guid.NewGuid();
        // The machine wins on relevance (two title hits), so boosts and buries must visibly reorder.
        var searcher = Build(
            Document(machine, "Espresso Machines espresso", "Machine"),
            Document(grinder, "Espresso Grinders", "Grinder"));
        return (searcher, machine, grinder);
    }

    private static (IndexSearcher Searcher, DrillSideways DrillSideways, FacetsConfig Config) FacetedIndex(out Guid[] guids)
    {
        var config = new FacetsConfig();
        var directory = new RAMDirectory();
        var taxonomyDirectory = new RAMDirectory();
        guids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, Analyzer)))
        using (var taxonomyWriter = new DirectoryTaxonomyWriter(taxonomyDirectory))
        {
            string[] categories = ["Coffee", "Coffee", "Coffee", "Tea", "Tea"];
            for (int i = 0; i < categories.Length; i++)
            {
                var document = Document(guids[i], "drink", "Drink");
                document.Add(new FacetField(Category, categories[i]));
                writer.AddDocument(config.Build(taxonomyWriter, document));
            }
        }

        var searcher = new IndexSearcher(DirectoryReader.Open(directory));
        return (searcher, new DrillSideways(searcher, config, new DirectoryTaxonomyReader(taxonomyDirectory)), config);
    }

    private static Document Document(Guid guid, string title, string type) =>
    [
        new StringField(BaseDocumentProperties.ITEM_GUID, guid.ToString(), Field.Store.YES),
        new TextField("Title", title, Field.Store.YES),
        new TextField("Content", "plain", Field.Store.YES),
        new StringField("Type", type, Field.Store.YES),
    ];

    private static IndexSearcher Build(params Document[] documents)
    {
        var directory = new RAMDirectory();
        using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, Analyzer)))
        {
            foreach (var document in documents)
            {
                writer.AddDocument(document);
            }
        }

        return new IndexSearcher(DirectoryReader.Open(directory));
    }

    private static float ScoreOf(SuiteSearchResponse response, Guid guid) => response.Hits.Single(hit => hit.ItemGuid == guid).Score;

    private static int FacetCount(SuiteSearchResponse response, string label) =>
        response.Facets.Single(facet => facet.Dimension == Category).Values.Single(value => value.Label == label).Count;

    private static string Describe(SuiteSearchResponse response) =>
        $"{response.TotalHits}|" + string.Join(",", response.Hits.Select(hit => $"{hit.ItemGuid}:{hit.Score:R}"));

    private sealed class FixedRules(IReadOnlyList<RuleDefinition> rules)
        : RuleProvider(null!, null!, Options.Create(new SuiteSearchOptions()), TimeProvider.System, NullLogger<RuleProvider>.Instance)
    {
        public override IReadOnlyList<RuleDefinition> GetRules(string indexName) => rules;
    }

    private sealed class FailingRules()
        : RuleProvider(null!, null!, Options.Create(new SuiteSearchOptions()), TimeProvider.System, NullLogger<RuleProvider>.Instance)
    {
        public override IReadOnlyList<RuleDefinition> GetRules(string indexName) => throw new InvalidOperationException("database down");
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class RecordingLogger : ILogger<SuiteSearchService>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
