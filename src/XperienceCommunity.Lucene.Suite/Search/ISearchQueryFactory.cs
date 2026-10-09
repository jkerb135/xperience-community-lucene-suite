using Lucene.Net.Analysis;
using Lucene.Net.Search;

namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Builds the Lucene query for non-empty query text; register your own implementation to replace the default.</summary>
public interface ISearchQueryFactory
{
    /// <summary>Creates the query for <paramref name="queryText"/> against index <paramref name="indexName"/>.</summary>
    Query Create(string indexName, string queryText, Analyzer analyzer);
}
