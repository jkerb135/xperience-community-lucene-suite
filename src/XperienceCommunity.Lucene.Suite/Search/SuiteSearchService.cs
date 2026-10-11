using System.Diagnostics;

using Kentico.Xperience.Lucene.Core.Indexing;
using Kentico.Xperience.Lucene.Core.Search;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Search;

internal sealed class SuiteSearchService(
    ILuceneIndexManager indexManager,
    ILuceneSearchService searchService,
    IServiceProvider serviceProvider,
    ISearchQueryFactory queryFactory,
    IOptions<SuiteSearchOptions> options,
    RuleProvider ruleProvider,
    TimeProvider timeProvider,
    ILogger<SuiteSearchService> logger) : ISuiteSearchService
{
    private readonly LogThrottle logThrottle = new(timeProvider, TimeSpan.FromMinutes(1));

    public SuiteSearchResponse Search(SuiteSearchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        SearchExecutor.Validate(request);

        var index = indexManager.GetIndex(request.IndexName)
            ?? throw new ArgumentException($"Lucene index '{request.IndexName}' does not exist.", nameof(request));
        var strategy = (ILuceneIndexingStrategy)serviceProvider.GetRequiredService(index.LuceneIndexingStrategyType);
        var facetsConfig = strategy.FacetsConfigFactory();

        var response = SearchWithRules(request, executor => facetsConfig is not null
            ? searchService.UseSearcherWithDrillSideways(index, (searcher, drillSideways) =>
                executor.Search(request, index.LuceneAnalyzer, searcher, drillSideways, facetsConfig))
            : searchService.UseSearcher(index, searcher =>
                executor.Search(request, index.LuceneAnalyzer, searcher)));

        return response with { Elapsed = stopwatch.Elapsed };
    }

    /// <summary>Evaluates rules and runs <paramref name="run"/> with them; any rule failure falls back to an unruled run.</summary>
    /// <remarks>Request errors (<see cref="ArgumentException"/>) are not rule failures and still reach the caller.</remarks>
    internal SuiteSearchResponse SearchWithRules(SuiteSearchRequest request, Func<SearchExecutor, SuiteSearchResponse> run)
    {
        bool rulesFailed = false;
        RuleOutcome? outcome = null;
        if (request.ApplyRules)
        {
            try
            {
                var rules = ruleProvider.GetRules(request.IndexName);
                outcome = RuleEngine.Evaluate(rules, new RuleInput(
                    request.IndexName,
                    request.Query,
                    request.Facets,
                    new HashSet<string>(request.Contexts, StringComparer.Ordinal),
                    timeProvider.GetUtcNow().UtcDateTime));
            }
            catch (Exception exception)
            {
                Warn(exception, "Lucene Suite could not load or evaluate rules for index {Index}; searching without rules (further failures suppressed for a minute).", request.IndexName);
                rulesFailed = true;
            }
        }

        var executor = new SearchExecutor(queryFactory, options.Value, outcome);
        SuiteSearchResponse response;
        try
        {
            response = run(executor);
        }
        catch (Exception exception) when (outcome is not null && exception is not ArgumentException)
        {
            Warn(exception, "Lucene Suite could not apply rules on index {Index}; searching without rules (further failures suppressed for a minute).", request.IndexName);
            rulesFailed = true;
            outcome = null;
            executor = new SearchExecutor(queryFactory, options.Value);
            response = run(executor);
        }

        if (executor.BoostsTruncated)
        {
            Warn(null, "Lucene Suite applied only the strongest " + SearchExecutor.MaxBoosts + " boosts on index {Index}; weaker fired boosts were left out.", request.IndexName);
        }

        return response with
        {
            FiredRuleIds = outcome?.FiredRuleIds ?? [],
            UserData = outcome?.UserData.Select(data => new SuiteRuleUserData(data.RuleId, data.Data)).ToArray() ?? [],
            RulesFailed = rulesFailed,
        };
    }

    private void Warn(Exception? exception, string message, string index)
    {
        if (logThrottle.ShouldLog())
        {
            logger.LogWarning(exception, message, index);
        }
    }
}
