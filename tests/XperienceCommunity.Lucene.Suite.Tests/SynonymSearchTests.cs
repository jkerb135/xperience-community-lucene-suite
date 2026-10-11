using System.Diagnostics;

using Kentico.Xperience.Lucene.Core;

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit.Abstractions;

using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SynonymSearchTests(ITestOutputHelper output)
{
    private const string Index = "synonyms-test";
    private static readonly Analyzer Analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
    private static readonly Guid Grinder = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Mill = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly SynonymDefinition GrinderMill = new(7, Index, SynonymKind.TwoWay, null, ["grinder", "mill"]);

    [Fact]
    public void SynonymOnlyDocument_IsFound_AndRanksBelowAnIdenticalExactMatch()
    {
        var response = Search("grinder", [GrinderMill]);

        Assert.Equal([Grinder, Mill], response.Hits.Select(hit => hit.ItemGuid!.Value));
        Assert.InRange(response.Hits[1].Score / response.Hits[0].Score, 0.79f, 0.81f);
        Assert.Equal(new SuiteAppliedSynonym(7, "grinder", "mill"), Assert.Single(response.AppliedSynonyms));
        Assert.False(response.SynonymsFailed);
    }

    [Fact]
    public void WithoutSynonyms_TheOriginalQueryIsUnchanged()
    {
        var plain = Search("grinder", []);
        var unrelated = Search("grinder", [new SynonymDefinition(8, Index, SynonymKind.TwoWay, null, ["tea", "chai"])]);

        Assert.Equal([Grinder], plain.Hits.Select(hit => hit.ItemGuid!.Value));
        Assert.Equal(plain.Hits.Single().Score, unrelated.Hits.Single().Score);
        Assert.Empty(unrelated.AppliedSynonyms);
    }

    [Fact]
    public void Variants_GoThroughTheFactory_SoAllTermsFactoriesStillMatch()
    {
        var response = Search("coffee grinder", [GrinderMill], factory: new AllTermsFactory());

        Assert.Equal([Grinder, Mill], response.Hits.Select(hit => hit.ItemGuid!.Value));
    }

    [Fact]
    public void VariantWeight_MultipliesTheFactoryBoost()
    {
        var factory = new AllTermsFactory(boost: 2f);

        Search("grinder", [GrinderMill], factory: factory);

        Assert.Equal([2f, 1.6f], factory.Created.Select(query => query.Boost));
    }

    [Fact]
    public void RuleHides_StillApplyOverTheExpandedQuery_AndRulesSeeTheOriginalText()
    {
        RuleDefinition[] rules =
        [
            Rule(1, "grinder", hide: Mill),
            Rule(2, "mill", hide: Grinder),
        ];
        var request = Request("grinder");

        var response = Service(new FixedSynonyms([GrinderMill]), new FixedRules(rules), new DefaultSearchQueryFactory(Options.Create(SearchOptions())))
            .SearchWithRules(request, executor => executor.Search(request, Analyzer, BuildIndex()));

        Assert.Equal([1], response.FiredRuleIds);
        Assert.Equal([Grinder], response.Hits.Select(hit => hit.ItemGuid!.Value));
        Assert.Equal("grinder", request.Query);
    }

    [Fact]
    public void ProviderFailure_SearchesTheOriginalTextOnly()
    {
        var request = Request("grinder");

        var response = Service(new FailingSynonyms(), new FixedRules([]), new DefaultSearchQueryFactory(Options.Create(SearchOptions())))
            .SearchWithRules(request, executor => executor.Search(request, Analyzer, BuildIndex()));

        Assert.True(response.SynonymsFailed);
        Assert.Empty(response.AppliedSynonyms);
        Assert.Equal([Grinder], response.Hits.Select(hit => hit.ItemGuid!.Value));
    }

    [Fact]
    public void FactoryFailingOnAVariant_RerunsWithoutSynonyms_ButRequestErrorsPropagate()
    {
        var response = Search("grinder", [GrinderMill], factory: new AllTermsFactory(failOn: "mill", failure: new InvalidOperationException("bad variant")));

        Assert.True(response.SynonymsFailed);
        Assert.False(response.RulesFailed);
        Assert.Empty(response.AppliedSynonyms);
        Assert.Equal([Grinder], response.Hits.Select(hit => hit.ItemGuid!.Value));

        Assert.Throws<ArgumentException>(() => Search("grinder", [GrinderMill], factory: new AllTermsFactory(failOn: "mill", failure: new ArgumentException("bad"))));
    }

    [Fact]
    public void EmptyQuery_IsNeverExpanded()
    {
        var response = Search(string.Empty, [GrinderMill]);

        Assert.Equal(2, response.TotalHits);
        Assert.Empty(response.AppliedSynonyms);
    }

    [VolumeFact]
    public void Volume_ExpandOver2000Synonyms_StaysWellUnderAMillisecond()
    {
        var synonyms = Enumerable.Range(1, 2000)
            .Select(id => id % 2 == 0
                ? new SynonymDefinition(id, Index, SynonymKind.TwoWay, null, [$"term{id}", $"alt{id}", $"other {id}"])
                : new SynonymDefinition(id, Index, SynonymKind.OneWay, $"input {id}", [$"out{id}"]))
            .ToList();
        var set = new SynonymSet(synonyms);
        string query = "best coffee grinder term42 for input 7 espresso with burr";

        for (int i = 0; i < 1000; i++)
        {
            SynonymEngine.Expand(set, query, 20);
        }

        var times = new List<double>();
        for (int i = 0; i < 2000; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            SynonymEngine.Expand(set, query, 20);
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        double p95 = P95(times);
        output.WriteLine($"Expand p95 {p95:F4} ms over {synonyms.Count} synonyms");
        Assert.True(p95 < 1, $"p95 {p95} ms");

        // Whole search with 2 000 synonyms loaded and a matching query, against the same search without synonyms.
        var searcher = BuildIndex();
        var request = Request("coffee grinder");
        var withSynonyms = Service(new FixedSynonyms(synonyms.Append(GrinderMill)), new FixedRules([]), new DefaultSearchQueryFactory(Options.Create(SearchOptions())));
        var without = Service(FixedSynonyms.None, new FixedRules([]), new DefaultSearchQueryFactory(Options.Create(SearchOptions())));
        double searchP95 = P95(Measure(() => withSynonyms.SearchWithRules(request, executor => executor.Search(request, Analyzer, searcher))));
        double plainP95 = P95(Measure(() => without.SearchWithRules(request, executor => executor.Search(request, Analyzer, searcher))));
        output.WriteLine($"Search p95 {searchP95:F4} ms with synonyms, {plainP95:F4} ms without");
        Assert.True(searchP95 - plainP95 < 10, $"overhead {searchP95 - plainP95} ms");
    }

    private static List<double> Measure(Action search)
    {
        for (int i = 0; i < 500; i++)
        {
            search();
        }

        var times = new List<double>();
        for (int i = 0; i < 2000; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            search();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return times;
    }

    private static double P95(List<double> times)
    {
        times.Sort();
        return times[(int)(times.Count * 0.95)];
    }

    private static SuiteSearchResponse Search(string query, SynonymDefinition[] synonyms, ISearchQueryFactory? factory = null)
    {
        var request = Request(query);
        return Service(new FixedSynonyms(synonyms), new FixedRules([]), factory ?? new DefaultSearchQueryFactory(Options.Create(SearchOptions())))
            .SearchWithRules(request, executor => executor.Search(request, Analyzer, BuildIndex()));
    }

    private static SuiteSearchOptions SearchOptions()
    {
        var options = new SuiteSearchOptions();
        options.SearchFields[Index] = new() { ["Title"] = 1f };
        return options;
    }

    private static SuiteSearchService Service(SynonymProvider synonyms, RuleProvider rules, ISearchQueryFactory factory) =>
        new(null!, null!, null!, factory, Options.Create(SearchOptions()), rules, synonyms, TimeProvider.System, NullLogger<SuiteSearchService>.Instance);

    private static SuiteSearchRequest Request(string query) => new() { IndexName = Index, Query = query, TrackAnalytics = false };

    private static RuleDefinition Rule(int id, string pattern, Guid hide) =>
        new(id, Index, id, true, null, null, [new RuleCondition(RuleAnchoring.Contains, pattern, [], [])], new RuleActions([], [hide], [], null));

    private static IndexSearcher BuildIndex()
    {
        var directory = new RAMDirectory();
        using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, Analyzer)))
        {
            writer.AddDocument(Document(Grinder, "coffee grinder"));
            writer.AddDocument(Document(Mill, "coffee mill"));
        }

        return new IndexSearcher(DirectoryReader.Open(directory));
    }

    private static Document Document(Guid guid, string title) =>
    [
        new StringField(BaseDocumentProperties.ITEM_GUID, guid.ToString(), Field.Store.YES),
        new TextField("Title", title, Field.Store.YES),
    ];

    /// <summary>Requires every analyzed term of the text in Title; records each query it builds.</summary>
    private sealed class AllTermsFactory(float boost = 1f, string? failOn = null, Exception? failure = null) : ISearchQueryFactory
    {
        public List<Query> Created { get; } = [];

        public Query Create(string indexName, string queryText, Analyzer analyzer)
        {
            if (failOn is not null && queryText.Contains(failOn, StringComparison.Ordinal))
            {
                throw failure!;
            }

            var query = new BooleanQuery { Boost = boost };
            foreach (string word in queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                query.Add(new TermQuery(new Term("Title", word.ToLowerInvariant())), Occur.MUST);
            }

            Created.Add(query);
            return query;
        }
    }
}

internal sealed class FixedSynonyms(IEnumerable<SynonymDefinition> synonyms)
    : SynonymProvider(null!, null!, Options.Create(new SuiteSearchOptions()), TimeProvider.System, NullLogger<SynonymProvider>.Instance)
{
    private readonly SynonymSet set = new(synonyms);

    public static FixedSynonyms None { get; } = new([]);

    public override SynonymSet GetSynonyms(string indexName) => set;
}

internal sealed class FailingSynonyms()
    : SynonymProvider(null!, null!, Options.Create(new SuiteSearchOptions()), TimeProvider.System, NullLogger<SynonymProvider>.Instance)
{
    public override SynonymSet GetSynonyms(string indexName) => throw new InvalidOperationException("database down");
}

internal sealed class FixedRules(IReadOnlyList<RuleDefinition> rules)
    : RuleProvider(null!, null!, Options.Create(new SuiteSearchOptions()), TimeProvider.System, NullLogger<RuleProvider>.Instance)
{
    public override IReadOnlyList<RuleDefinition> GetRules(string indexName) => rules;
}
