using CMS.Core;

using Kentico.Xperience.Lucene.Core;
using Kentico.Xperience.Lucene.Core.Indexing;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SuiteItemLookupTests
{
    private static readonly Guid Shirt = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Plunger = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Untitled = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public void Describe_ReturnsOneEntryPerItem_InInputOrder_WithTheFirstDocumentsTitle()
    {
        var searcher = BuildIndex(
            Document(Shirt, "T-shirt Black", "Product"),
            Document(Plunger, "Coffee Plunger", null),
            Document(Shirt, "Camiseta negra", "Product"),
            Document(Untitled, null, null));

        var items = SuiteItemLookup.Describe(searcher, [Untitled, Guid.NewGuid(), Plunger, Shirt], "Title");

        Assert.Equal(
            [new SuiteItem(Untitled, Untitled.ToString(), null), new SuiteItem(Plunger, "Coffee Plunger", null), new SuiteItem(Shirt, "T-shirt Black", "Product")],
            items);
    }

    [Fact]
    public void Describe_WithoutATitleField_ShowsTheItemGuid()
    {
        var items = SuiteItemLookup.Describe(BuildIndex(Document(Shirt, "T-shirt Black", null)), [Shirt], titleField: null);

        Assert.Equal(Shirt.ToString(), Assert.Single(items).Title);
    }

    [Fact]
    public void Describe_RejectsMoreThanTheCap()
    {
        var tooMany = Enumerable.Range(0, SuiteItemLookup.MaxItems + 1).Select(_ => Guid.NewGuid()).ToList();

        Assert.Throws<ArgumentOutOfRangeException>(() => new SuiteItemLookup(null!, null!, null!).Describe("index", tooMany));
    }

    [Fact]
    public void FromHits_ListsEachItemOnce_SkippingHitsWithoutAGuid()
    {
        SuiteSearchHit[] hits =
        [
            Hit(Shirt, "T-shirt Black", "Product"),
            Hit(null, "orphan", null),
            Hit(Shirt, "Camiseta negra", "Product"),
            Hit(Plunger, null, null),
        ];

        Assert.Equal(
            [new SuiteItem(Shirt, "T-shirt Black", "Product"), new SuiteItem(Plunger, Plunger.ToString(), null)],
            SuiteItemLookup.FromHits(hits, "Title"));
    }

    [Fact]
    public async Task ItemCommands_FailClosed_WhenTheIndexIsUnresolved_OrTheRequestIsTooLarge()
    {
        var component = new RulePinsFormComponent(new NoLocalization(), new NoIndexes(), null!, null!) { IndexIdentifier = 4 };

        var search = await component.Search(new RuleItemSearchArguments { Query = "coffee" });
        var describe = await component.Describe(new RuleItemDescribeArguments { Items = [Shirt] });
        var tooMany = await component.Describe(new RuleItemDescribeArguments { Items = [.. Enumerable.Range(0, SuiteItemLookup.MaxItems + 1).Select(_ => Guid.NewGuid())] });
        var duplicates = await component.Describe(new RuleItemDescribeArguments { Items = [.. Enumerable.Repeat(Shirt, SuiteItemLookup.MaxItems + 1)] });

        Assert.Equal(RuleItemsFormComponent.IndexMissingMessage, search.Result.Error);
        Assert.Equal(RuleItemsFormComponent.IndexMissingMessage, describe.Result.Error);
        Assert.Equal($"At most {SuiteItemLookup.MaxItems} items can be shown.", tooMany.Result.Error);
        Assert.Equal(RuleItemsFormComponent.IndexMissingMessage, duplicates.Result.Error); // de-duplicated under the cap
        Assert.Empty(search.Result.Items);
    }

    private static Document Document(Guid itemGuid, string? title, string? contentType)
    {
        var document = new Document { new StringField(BaseDocumentProperties.ITEM_GUID, itemGuid.ToString(), Field.Store.YES) };
        if (title is not null)
        {
            document.Add(new TextField("Title", title, Field.Store.YES));
        }

        if (contentType is not null)
        {
            document.Add(new StringField(SuiteItemLookup.ContentTypeField, contentType, Field.Store.YES));
        }

        return document;
    }

    private static SuiteSearchHit Hit(Guid? itemGuid, string? title, string? contentType)
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>();
        if (title is not null)
        {
            fields["Title"] = [title];
        }

        if (contentType is not null)
        {
            fields[SuiteItemLookup.ContentTypeField] = [contentType];
        }

        return new SuiteSearchHit(itemGuid, 1, fields);
    }

    private static IndexSearcher BuildIndex(params Document[] documents)
    {
        var directory = new RAMDirectory();
        using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, new StandardAnalyzer(LuceneVersion.LUCENE_48))))
        {
            foreach (var document in documents)
            {
                writer.AddDocument(document);
            }
        }

        return new IndexSearcher(DirectoryReader.Open(directory));
    }

    private sealed class NoIndexes : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(int identifier) => null;

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex? GetIndex(string indexName) => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }

    private sealed class NoLocalization : ILocalizationService
    {
        public string GetString(string resourceKey, string? culture = null, bool useDefaultCulture = true) => resourceKey;

        public string LocalizeExpression(string expression, string? culture = null, bool encode = false, Func<string, string, bool, string>? getStringMethod = null, bool useDefaultCulture = true) => expression;

        public string LocalizeString(string inputText, string? culture = null, bool encode = false, bool useDefaultCulture = true) => inputText;
    }
}
