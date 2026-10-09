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

using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SearchExecutorTests
{
    private const string IndexName = "test";
    private const string Category = "Category";

    private static readonly Analyzer Analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);

    [Fact]
    public void TextSearch_RanksTitleBoostAboveContent_AndReturnsStoredFields()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var docB = CreateDocument(b.ToString(), title: "espresso", content: "plain");
        docB.Add(new StoredField("Tag", "hot"));
        docB.Add(new StoredField("Tag", "strong"));
        var searcher = BuildIndex(CreateDocument(a.ToString(), title: "plain", content: "espresso"), docB);

        var response = CreateExecutor().Search(Request("espresso"), Analyzer, searcher);

        Assert.Equal(2, response.TotalHits);
        Assert.Equal([b, a], response.Hits.Select(hit => hit.ItemGuid!.Value));
        Assert.True(response.Hits[0].Score > response.Hits[1].Score);
        Assert.Equal(["espresso"], response.Hits[0].Fields["Title"]);
        Assert.Equal(["hot", "strong"], response.Hits[0].Fields["Tag"]);
    }

    [Fact]
    public void Paging_ReturnsRequestedSlice_AndFullTotal()
    {
        var searcher = BuildIndex(Enumerable.Range(0, 25)
            .Select(i => CreateDocument(Guid.NewGuid().ToString(), title: $"item {i}", content: "coffee"))
            .ToArray());

        var response = CreateExecutor().Search(Request("coffee") with { Page = 3, PageSize = 10 }, Analyzer, searcher);

        Assert.Equal(5, response.Hits.Count);
        Assert.Equal(25, response.TotalHits);
    }

    [Theory]
    [InlineData("pageSize0")]
    [InlineData("pageSize101")]
    [InlineData("page0")]
    [InlineData("windowOver1000")]
    [InlineData("windowOverflow")]
    [InlineData("query257")]
    [InlineData("dimensions21")]
    [InlineData("values51")]
    [InlineData("value257")]
    public void OutOfRangeRequests_AreRejected(string scenario)
    {
        var index = BuildFacetedIndex();

        Assert.Throws<ArgumentOutOfRangeException>(() => Search(index, InvalidRequest(scenario)));
    }

    [Theory]
    [InlineData("indexNull")]
    [InlineData("indexEmpty")]
    [InlineData("facetsNull")]
    [InlineData("dimensionEmpty")]
    [InlineData("valuesNull")]
    [InlineData("valuesEmpty")]
    [InlineData("valueNull")]
    [InlineData("valueEmpty")]
    public void MalformedRequests_AreRejected(string scenario)
    {
        var index = BuildFacetedIndex();

        Assert.Throws<ArgumentException>(() => Search(index, InvalidRequest(scenario)));
    }

    [Theory]
    [InlineData("query257", "Query must not exceed 256 characters.")]
    [InlineData("indexEmpty", "IndexName is required.")]
    public void QueryTesterValidationMessage_DropsRuntimeSuffixes(string scenario, string expected)
    {
        var index = BuildFacetedIndex();
        var exception = Assert.ThrowsAny<ArgumentException>(() => Search(index, InvalidRequest(scenario)));

        Assert.Equal(expected, Admin.UIPages.QueryTesterPage.ValidationMessage(exception));
    }

    [Theory]
    [InlineData("the")]
    [InlineData("!!!")]
    public void QueryWithNoTokensAfterAnalysis_ReturnsNoHits(string query)
    {
        var searcher = BuildIndex(CreateDocument(Guid.NewGuid().ToString(), title: "the coffee", content: "!!! the tea"));

        var response = CreateExecutor().Search(Request(query), Analyzer, searcher);

        Assert.Equal(0, response.TotalHits);
        Assert.Empty(response.Hits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQuery_BrowsesWholeIndex(string? query)
    {
        var response = Search(BuildFacetedIndex(), Request(query));

        Assert.Equal(5, response.TotalHits);
        Assert.Equal(3, FacetCount(response, "Coffee"));
        Assert.Equal(2, FacetCount(response, "Tea"));
    }

    [Fact]
    public void Facets_AreCountedWithoutFilter()
    {
        var response = Search(BuildFacetedIndex(), Request("drink"));

        Assert.Equal(5, response.TotalHits);
        Assert.Equal(3, FacetCount(response, "Coffee"));
        Assert.Equal(2, FacetCount(response, "Tea"));
    }

    [Fact]
    public void FacetValuesWithinOneDimension_AreOred()
    {
        var response = Search(BuildFacetedIndex(), Request("drink", "Coffee", "Tea"));

        Assert.Equal(5, response.TotalHits);
    }

    [Fact]
    public void FacetFilter_KeepsSidewaysCounts()
    {
        var response = Search(BuildFacetedIndex(), Request("drink", "Coffee"));

        Assert.Equal(3, response.TotalHits);
        Assert.Equal(3, FacetCount(response, "Coffee"));
        Assert.Equal(2, FacetCount(response, "Tea"));
    }

    [Fact]
    public void NonFacetedIndex_ReturnsEmptyFacets()
    {
        var searcher = BuildIndex(CreateDocument(Guid.NewGuid().ToString(), title: "coffee", content: "beans"));

        var response = CreateExecutor().Search(Request("coffee"), Analyzer, searcher);

        Assert.Equal(1, response.TotalHits);
        Assert.Empty(response.Facets);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public void MissingOrInvalidItemGuid_YieldsNullItemGuid(string? itemGuid)
    {
        var searcher = BuildIndex(CreateDocument(itemGuid, title: "coffee", content: "beans"));

        var hit = Assert.Single(CreateExecutor().Search(Request("coffee"), Analyzer, searcher).Hits);

        Assert.Null(hit.ItemGuid);
    }

    [Fact]
    public void FacetFiltersOnNonFacetedIndex_AreRejected()
    {
        var searcher = BuildIndex(CreateDocument(Guid.NewGuid().ToString(), title: "coffee", content: "beans"));

        Assert.Throws<ArgumentException>(() => CreateExecutor().Search(Request("coffee", "Coffee"), Analyzer, searcher));
    }

    [Fact]
    public void QueryFactory_WithoutConfiguredFields_Throws()
    {
        var factory = new DefaultSearchQueryFactory(Options.Create(new SuiteSearchOptions()));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create(IndexName, "coffee", Analyzer));

        Assert.Contains(IndexName, exception.Message);
    }

    private sealed record FacetedIndex(IndexSearcher Searcher, DrillSideways DrillSideways, FacetsConfig Config);

    private static SearchExecutor CreateExecutor()
    {
        var options = new SuiteSearchOptions();
        options.SearchFields[IndexName] = new() { ["Title"] = 3f, ["Content"] = 1f };
        return new SearchExecutor(new DefaultSearchQueryFactory(Options.Create(options)), options);
    }

    private static SuiteSearchResponse Search(FacetedIndex index, SuiteSearchRequest request) =>
        CreateExecutor().Search(request, Analyzer, index.Searcher, index.DrillSideways, index.Config);

    private static SuiteSearchRequest Request(string? query, params string[] categories) => new()
    {
        IndexName = IndexName,
        Query = query,
        Facets = categories.Length == 0
            ? new Dictionary<string, IReadOnlyList<string>>()
            : new Dictionary<string, IReadOnlyList<string>> { [Category] = categories },
    };

    private static SuiteSearchRequest InvalidRequest(string scenario)
    {
        var request = Request("drink");
        string tooLong = new('x', 257);
        return scenario switch
        {
            "pageSize0" => request with { PageSize = 0 },
            "pageSize101" => request with { PageSize = 101 },
            "page0" => request with { Page = 0 },
            "windowOver1000" => request with { Page = 11, PageSize = 100 },
            "windowOverflow" => request with { Page = int.MaxValue, PageSize = 2 },
            "query257" => request with { Query = tooLong },
            "dimensions21" => request with { Facets = Enumerable.Range(0, 21).ToDictionary(i => $"dim{i}", _ => (IReadOnlyList<string>)["a"]) },
            "values51" => Request("drink", Enumerable.Range(0, 51).Select(i => $"v{i}").ToArray()),
            "value257" => Request("drink", tooLong),
            "indexNull" => request with { IndexName = null! },
            "indexEmpty" => request with { IndexName = "" },
            "facetsNull" => request with { Facets = null! },
            "dimensionEmpty" => request with { Facets = new Dictionary<string, IReadOnlyList<string>> { [""] = ["Coffee"] } },
            "valuesNull" => request with { Facets = new Dictionary<string, IReadOnlyList<string>> { [Category] = null! } },
            "valuesEmpty" => request with { Facets = new Dictionary<string, IReadOnlyList<string>> { [Category] = [] } },
            "valueNull" => Request("drink", [null!]),
            "valueEmpty" => Request("drink", ""),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private static int FacetCount(SuiteSearchResponse response, string label) =>
        response.Facets.Single(facet => facet.Dimension == Category).Values.Single(value => value.Label == label).Count;

    private static Document CreateDocument(string? itemGuid, string title, string content)
    {
        var document = new Document
        {
            new TextField("Title", title, Field.Store.YES),
            new TextField("Content", content, Field.Store.YES),
        };
        if (itemGuid is not null)
        {
            document.Add(new StringField(BaseDocumentProperties.ITEM_GUID, itemGuid, Field.Store.YES));
        }

        return document;
    }

    private static IndexSearcher BuildIndex(params Document[] documents)
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

    private static FacetedIndex BuildFacetedIndex()
    {
        var config = new FacetsConfig();
        var directory = new RAMDirectory();
        var taxonomyDirectory = new RAMDirectory();
        using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, Analyzer)))
        using (var taxonomyWriter = new DirectoryTaxonomyWriter(taxonomyDirectory))
        {
            foreach (string category in new[] { "Coffee", "Coffee", "Coffee", "Tea", "Tea" })
            {
                var document = CreateDocument(Guid.NewGuid().ToString(), title: "drink", content: $"a {category} drink");
                document.Add(new FacetField(Category, category));
                writer.AddDocument(config.Build(taxonomyWriter, document));
            }
        }

        var searcher = new IndexSearcher(DirectoryReader.Open(directory));
        var drillSideways = new DrillSideways(searcher, config, new DirectoryTaxonomyReader(taxonomyDirectory));
        return new FacetedIndex(searcher, drillSideways, config);
    }
}
