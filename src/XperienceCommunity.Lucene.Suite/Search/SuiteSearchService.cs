using System.Diagnostics;

using Kentico.Xperience.Lucene.Core.Indexing;
using Kentico.Xperience.Lucene.Core.Search;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace XperienceCommunity.Lucene.Suite.Search;

internal sealed class SuiteSearchService(
    ILuceneIndexManager indexManager,
    ILuceneSearchService searchService,
    IServiceProvider serviceProvider,
    ISearchQueryFactory queryFactory,
    IOptions<SuiteSearchOptions> options) : ISuiteSearchService
{
    public SuiteSearchResponse Search(SuiteSearchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        SearchExecutor.Validate(request);

        var index = indexManager.GetIndex(request.IndexName)
            ?? throw new ArgumentException($"Lucene index '{request.IndexName}' does not exist.", nameof(request));
        var strategy = (ILuceneIndexingStrategy)serviceProvider.GetRequiredService(index.LuceneIndexingStrategyType);
        var executor = new SearchExecutor(queryFactory, options.Value);

        var response = strategy.FacetsConfigFactory() is { } facetsConfig
            ? searchService.UseSearcherWithDrillSideways(index, (searcher, drillSideways) =>
                executor.Search(request, index.LuceneAnalyzer, searcher, drillSideways, facetsConfig))
            : searchService.UseSearcher(index, searcher =>
                executor.Search(request, index.LuceneAnalyzer, searcher));

        return response with { Elapsed = stopwatch.Elapsed };
    }
}
