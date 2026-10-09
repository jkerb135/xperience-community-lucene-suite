using Lucene.Net.Analysis;
using Lucene.Net.Search;
using Lucene.Net.Util;

using Microsoft.Extensions.Options;

namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>OR's an analyzed term query per configured field of the index, each weighted by its boost.</summary>
public sealed class DefaultSearchQueryFactory(IOptions<SuiteSearchOptions> options) : ISearchQueryFactory
{
    /// <inheritdoc />
    public Query Create(string indexName, string queryText, Analyzer analyzer)
    {
        if (!options.Value.SearchFields.TryGetValue(indexName, out var fields) || fields.Count == 0)
        {
            throw new InvalidOperationException(
                $"No search fields are configured for index '{indexName}'. Set {nameof(SuiteSearchOptions)}.{nameof(SuiteSearchOptions.SearchFields)}[\"{indexName}\"].");
        }

        var builder = new QueryBuilder(analyzer);
        var query = new BooleanQuery();
        foreach (var (field, boost) in fields)
        {
            // Null when analysis leaves no tokens (stop words, punctuation); an empty BooleanQuery matches nothing.
            if (builder.CreateBooleanQuery(field, queryText) is { } fieldQuery)
            {
                fieldQuery.Boost = boost;
                query.Add(fieldQuery, Occur.SHOULD);
            }
        }

        return query;
    }
}
