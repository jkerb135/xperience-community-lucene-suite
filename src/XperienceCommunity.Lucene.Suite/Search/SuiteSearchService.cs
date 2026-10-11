using System.Diagnostics;

using Kentico.Xperience.Lucene.Core.Indexing;
using Kentico.Xperience.Lucene.Core.Search;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Search;

internal sealed class SuiteSearchService(
    ILuceneIndexManager indexManager,
    ILuceneSearchService searchService,
    IServiceProvider serviceProvider,
    ISearchQueryFactory queryFactory,
    IOptions<SuiteSearchOptions> options,
    RuleProvider ruleProvider,
    SynonymProvider synonymProvider,
    TimeProvider timeProvider,
    ILogger<SuiteSearchService> logger) : ISuiteSearchService
{
    private readonly LogThrottle logThrottle = new(timeProvider, TimeSpan.FromMinutes(1));
    private readonly LogThrottle synonymLogThrottle = new(timeProvider, TimeSpan.FromMinutes(1));

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

    /// <summary>Expands synonyms, evaluates rules and runs <paramref name="run"/> with both; a failure of either falls back to a plain run.</summary>
    /// <remarks>Request errors (<see cref="ArgumentException"/>) are not rule or synonym failures and still reach the caller.</remarks>
    internal SuiteSearchResponse SearchWithRules(SuiteSearchRequest request, Func<SearchExecutor, SuiteSearchResponse> run)
    {
        bool synonymsFailed = false;
        var expansion = SynonymExpansion.None;
        if (request.ApplySynonyms && !string.IsNullOrWhiteSpace(request.Query))
        {
            try
            {
                expansion = SynonymEngine.Expand(synonymProvider.GetSynonyms(request.IndexName), request.Query, options.Value.Synonyms.MaxExpansions);
            }
            catch (Exception exception)
            {
                WarnSynonyms(exception, "Lucene Suite could not load or expand synonyms for index {Index}; searching the original text only (further failures suppressed for a minute).", request.IndexName);
                synonymsFailed = true;
            }
        }

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

        var executor = new SearchExecutor(queryFactory, options.Value, outcome, expansion);
        SuiteSearchResponse response;
        try
        {
            response = run(executor);
        }
        catch (Exception exception) when ((outcome is not null || expansion.Variants.Count > 0) && exception is not ArgumentException)
        {
            Warn(exception, "Lucene Suite could not apply rules or synonyms on index {Index}; searching without them (further failures suppressed for a minute).", request.IndexName);
            rulesFailed |= outcome is { FiredRuleIds.Count: > 0 };
            synonymsFailed |= expansion.Variants.Count > 0;
            outcome = null;
            expansion = SynonymExpansion.None;
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
            AppliedSynonyms = [.. expansion.Applied.Select(applied => new SuiteAppliedSynonym(applied.SynonymId, applied.Term, applied.Replacement))],
            SynonymsFailed = synonymsFailed,
        };
    }

    private void WarnSynonyms(Exception exception, string message, string index)
    {
        if (synonymLogThrottle.ShouldLog())
        {
            logger.LogWarning(exception, message, index);
        }
    }

    private void Warn(Exception? exception, string message, string index)
    {
        if (logThrottle.ShouldLog())
        {
            logger.LogWarning(exception, message, index);
        }
    }
}
