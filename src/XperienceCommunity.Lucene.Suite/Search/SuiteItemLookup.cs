using Kentico.Xperience.Lucene.Core;
using Kentico.Xperience.Lucene.Core.Indexing;
using Kentico.Xperience.Lucene.Core.Search;

using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Queries;
using Lucene.Net.Search;

using Microsoft.Extensions.Options;

namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>An indexed item as the admin shows it.</summary>
/// <param name="ItemGuid">The ItemGuid stored in the index (WebPageItemGUID or ContentItemGUID).</param>
/// <param name="Title">Stored value of the index's first search field, or the ItemGuid.</param>
/// <param name="ContentType">Stored content type name, when the index stores one.</param>
internal sealed record SuiteItem(Guid ItemGuid, string Title, string? ContentType);

/// <summary>Resolves ItemGuids to display titles for the rule editor's pins and hides.</summary>
internal sealed class SuiteItemLookup(ILuceneIndexManager indexManager, ILuceneSearchService searchService, IOptions<SuiteSearchOptions> options)
{
    /// <summary>Most items resolved at once (300 pins + 50 hides).</summary>
    internal const int MaxItems = 350;

    internal const string ContentTypeField = "ContentTypeName";

    public IReadOnlyList<SuiteItem> Describe(string indexName, IReadOnlyCollection<Guid> itemGuids)
    {
        var distinct = itemGuids.Distinct().ToList();
        if (distinct.Count > MaxItems)
        {
            throw new ArgumentOutOfRangeException(nameof(itemGuids), distinct.Count, $"At most {MaxItems} items can be resolved at once.");
        }

        if (distinct.Count == 0)
        {
            return [];
        }

        var index = indexManager.GetIndex(indexName) ?? throw new ArgumentException($"Lucene index '{indexName}' does not exist.", nameof(indexName));
        string? titleField = TitleField(indexName);
        return searchService.UseSearcher(index, searcher => Describe(searcher, distinct, titleField));
    }

    /// <summary>The field shown as an item's title: the index's first configured search field.</summary>
    public string? TitleField(string indexName) =>
        options.Value.SearchFields.TryGetValue(indexName, out var fields) ? fields.Keys.FirstOrDefault() : null;

    /// <summary>One item per ItemGuid, in input order; in multilingual indexes the document with the lowest id wins.</summary>
    internal static IReadOnlyList<SuiteItem> Describe(IndexSearcher searcher, IReadOnlyList<Guid> distinct, string? titleField)
    {
        var query = new ConstantScoreQuery(new TermsFilter(distinct.Select(guid => new Term(BaseDocumentProperties.ITEM_GUID, guid.ToString())).ToList()));
        var top = searcher.Search(query, Math.Max(distinct.Count, 1));
        if (top.TotalHits > top.ScoreDocs.Length)
        {
            top = searcher.Search(query, top.TotalHits);
        }

        var byGuid = new Dictionary<Guid, SuiteItem>();
        foreach (var scoreDoc in top.ScoreDocs.OrderBy(scoreDoc => scoreDoc.Doc))
        {
            var document = searcher.Doc(scoreDoc.Doc);
            if (Guid.TryParse(document.Get(BaseDocumentProperties.ITEM_GUID), out var guid) && !byGuid.ContainsKey(guid))
            {
                byGuid[guid] = ToItem(guid, Stored(document, titleField), document.Get(ContentTypeField));
            }
        }

        return [.. distinct.Where(byGuid.ContainsKey).Select(guid => byGuid[guid])];
    }

    /// <summary>Search hits as picker items, once per ItemGuid (the first hit wins); hits without an ItemGuid are skipped.</summary>
    internal static IReadOnlyList<SuiteItem> FromHits(IEnumerable<SuiteSearchHit> hits, string? titleField)
    {
        var seen = new HashSet<Guid>();
        var items = new List<SuiteItem>();
        foreach (var hit in hits)
        {
            if (hit.ItemGuid is { } guid && seen.Add(guid))
            {
                items.Add(ToItem(guid, First(hit, titleField), First(hit, ContentTypeField)));
            }
        }

        return items;
    }

    private static SuiteItem ToItem(Guid guid, string? title, string? contentType) =>
        new(guid, string.IsNullOrWhiteSpace(title) ? guid.ToString() : title, string.IsNullOrWhiteSpace(contentType) ? null : contentType);

    private static string? Stored(Document document, string? field) => field is null ? null : document.Get(field);

    private static string? First(SuiteSearchHit hit, string? field) =>
        field is not null && hit.Fields.TryGetValue(field, out var values) && values.Count > 0 ? values[0] : null;
}
