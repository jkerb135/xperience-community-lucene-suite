using System.Globalization;

using CMS.DataEngine;
using CMS.Helpers;

using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Suggestions;

/// <summary>Loads suggestion candidates from <see cref="SearchDailyInfo"/> and the blocklist through Kentico's cache, then matches with <see cref="SuggestionEngine"/>.</summary>
internal sealed class SuggestionSource(
    IInfoProvider<SearchDailyInfo> daily,
    IInfoProvider<BlockedSuggestionInfo> blocklist,
    ILuceneIndexManager indexManager,
    IProgressiveCache cache,
    IOptions<SuiteSearchOptions> options,
    TimeProvider clock) : ISuiteSuggestions
{
    public async Task<IReadOnlyList<string>> SuggestAsync(string indexName, string? input, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        var settings = options.Value.Suggestions;
        int max = maxResults ?? settings.MaxResults;
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1, nameof(maxResults));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(max, ISuiteSuggestions.MaxResultsLimit, nameof(maxResults));

        // Checked before anything is cached, so arbitrary names from public callers never create cache entries.
        if (input is null || input.Length > ISuiteSuggestions.MaxInputLength || string.IsNullOrWhiteSpace(indexName) || indexManager.GetIndex(indexName) is null)
        {
            return [];
        }

        string cacheKey = indexName.ToLowerInvariant();
        var candidates = await cache.LoadAsync(
            (_, token) => LoadCandidates(indexName, settings, token),
            new CacheSettings(settings.CacheMinutes, "lucenesuite", "suggestions", cacheKey),
            cancellationToken);
        var blocks = await cache.LoadAsync(
            (cacheSettings, token) =>
            {
                cacheSettings.CacheDependency = CacheHelper.GetCacheDependency($"{BlockedSuggestionInfo.OBJECT_TYPE}|all");
                return LoadBlocks(indexName, token);
            },
            new CacheSettings(settings.CacheMinutes, "lucenesuite", "blockedsuggestions", cacheKey),
            cancellationToken);
        return SuggestionEngine.Match(candidates, blocks, input, settings.MinInputLength, max);
    }

    /// <summary>The last <see cref="SuiteSuggestionOptions.WindowDays"/> complete UTC days, ending yesterday.</summary>
    internal (DateOnly From, DateOnly To) Window(SuiteSuggestionOptions settings)
    {
        var to = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-1);
        return (to.AddDays(1 - settings.WindowDays), to);
    }

    private async Task<IReadOnlyList<SuggestionCandidate>> LoadCandidates(string indexName, SuiteSuggestionOptions settings, CancellationToken cancellationToken)
    {
        var (from, to) = Window(settings);
        const string searches = nameof(SearchDailyInfo.Searches);
        const string zero = nameof(SearchDailyInfo.ZeroResultSearches);
        // The engine's filters also run in SQL, so the TopN cap counts only eligible texts. Values are validated numbers.
        var query = daily.Get()
            .TopN(settings.MaxCandidates)
            .Columns(
                new QueryColumn(nameof(SearchDailyInfo.QueryText)),
                new AggregatedColumn(AggregationType.Sum, searches).As(searches),
                new AggregatedColumn(AggregationType.Sum, zero).As(zero))
            .WhereEquals(nameof(SearchDailyInfo.IndexName), indexName)
            .WhereGreaterOrEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(from))
            .WhereLessOrEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(to))
            .WhereNotEmpty(nameof(SearchDailyInfo.QueryText))
            .WhereNotContains(nameof(SearchDailyInfo.QueryText), QueryScrubber.EmailPlaceholder)
            .WhereNotContains(nameof(SearchDailyInfo.QueryText), QueryScrubber.NumberPlaceholder)
            .WhereIn(nameof(SearchDailyInfo.QueryText), Surfacing.Query(daily, indexName, options.Value.Analytics.Surfacing))
            .GroupBy(nameof(SearchDailyInfo.QueryText))
            .Having(string.Create(CultureInfo.InvariantCulture, $"SUM([{searches}]) >= {settings.MinSearches} AND SUM([{zero}]) < {settings.MaxNoResultRate:R} * SUM([{searches}])"))
            .OrderByDescending(searches)
            .OrderByAscending(nameof(SearchDailyInfo.QueryText));
        var rows = await query.GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
        return SuggestionEngine.Candidates(
            rows.Select(row => new SuggestionRow(
                row.QueryText,
                ValidationHelper.GetLong(row.GetValue(searches), 0, CultureInfo.InvariantCulture),
                ValidationHelper.GetLong(row.GetValue(zero), 0, CultureInfo.InvariantCulture))),
            settings);
    }

    private async Task<IReadOnlyCollection<SuggestionBlock>> LoadBlocks(string indexName, CancellationToken cancellationToken)
    {
        var rows = await blocklist.Get()
            .WhereEquals(nameof(BlockedSuggestionInfo.IndexName), indexName)
            .Columns(nameof(BlockedSuggestionInfo.Text), nameof(BlockedSuggestionInfo.MatchMode))
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
        return rows
            .Select(row => new SuggestionBlock(
                SynonymEngine.MatchKey(row.Text),
                string.Equals(row.MatchMode, BlockedSuggestionInfo.MatchModeWord, StringComparison.OrdinalIgnoreCase)))
            .Where(block => block.Key.Length > 0)
            .ToList();
    }
}
