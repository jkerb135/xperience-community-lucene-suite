using System.Globalization;
using System.Runtime.CompilerServices;

using CMS.DataEngine;
using CMS.Helpers;

using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Suggestions;
using XperienceCommunity.Lucene.Suite.Synonyms;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SuggestionSourceDatabaseTests
{
    private const string Index = "suggest-test";
    private const string OtherIndex = "suggest-other";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-04-01T12:00:00Z", CultureInfo.InvariantCulture);

    [DatabaseFact]
    public async Task Candidates_AreSurfacedTextsOfTheWindow_WithoutDeadEndsPlaceholdersBrowseOrOther()
    {
        Seed(Index);
        var source = Source(cacheMinutes: 0);

        Assert.Equal((new DateOnly(2030, 1, 1), new DateOnly(2030, 3, 31)), source.Window(new SuiteSuggestionOptions()));
        Assert.Equal(["coffee mostly", "coffee acme", "coffee inside", "best coffee"], await source.SuggestAsync(Index, "Coffee"));
        Assert.Equal(["coffee mostly", "coffee acme"], await source.SuggestAsync(Index.ToUpperInvariant(), "coffee", 2));
        Assert.Empty(await source.SuggestAsync(Index, "email"));
        Assert.Empty(await source.SuggestAsync(Index, "number"));

        // The cap is applied in SQL to the most searched eligible texts.
        Assert.Equal(["coffee mostly", "coffee acme"], await Source(cacheMinutes: 0, maxCandidates: 2).SuggestAsync(Index, "coffee"));
    }

    [DatabaseFact]
    public async Task EverySuggestion_IsANamedQueryOfTheReports_ForTheSameDays()
    {
        Seed(Index);
        var source = Source(cacheMinutes: 0);
        var (from, to) = source.Window(new SuiteSuggestionOptions());
        var reports = new SearchReportService(
            KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            RollupDb.Store(),
            Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions()),
            new ManualClock(Now));

        var named = reports.Queries(Index, from, to, QuerySort.Searches, 0, 200).Rows.Select(row => row.QueryText).ToList();
        var suggested = await source.SuggestAsync(Index, "coffee", 20);

        Assert.NotEmpty(suggested);
        Assert.All(suggested, text => Assert.Contains(text, named));
        // Surfaced over all kept days, so the report names it, but too few searches inside the window for public suggestions.
        Assert.Contains("coffee edge", named);
        Assert.DoesNotContain("coffee edge", suggested);
    }

    [DatabaseFact]
    public async Task ExactAndWordBlocks_RemoveTheirCandidates()
    {
        Seed(Index);
        var blocks = KenticoServices.Value.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>();
        blocks.Set(Block(Index, "Coffee  Mostly!", BlockedSuggestionInfo.MatchModeExact));
        blocks.Set(Block(Index, "ACME", BlockedSuggestionInfo.MatchModeWord));
        blocks.Set(Block(OtherIndex, "inside", BlockedSuggestionInfo.MatchModeWord));
        try
        {
            Assert.Equal(["coffee inside", "best coffee"], await Source(cacheMinutes: 0).SuggestAsync(Index, "coffee"));
        }
        finally
        {
            CleanupBlocks(Index, OtherIndex);
        }
    }

    [DatabaseFact]
    public async Task Candidates_AreCachedPerIndex_ButABlocklistChangeAppliesAtOnce()
    {
        // A fresh index name, so no earlier test's cache entry is reused.
        string index = "suggest-cache-" + Guid.NewGuid().ToString("N");
        Seed(index);
        var source = Source(cacheMinutes: 60);
        var blocks = KenticoServices.Value.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>();
        try
        {
            Assert.Contains("coffee acme", await source.SuggestAsync(index, "coffee"));

            // SearchDaily is written in bulk without touching any dependency: the cached candidates stay until they expire.
            KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(
                [Row("2030-03-01", "coffee late", 50, index: index), Row("2030-03-02", "coffee late", 50, index: index)]);
            Assert.DoesNotContain("coffee late", await source.SuggestAsync(index, "coffee"));

            var block = Block(index, "acme", BlockedSuggestionInfo.MatchModeWord);
            blocks.Set(block);
            Assert.DoesNotContain("coffee acme", await source.SuggestAsync(index, "coffee"));

            blocks.Delete(block);
            Assert.Contains("coffee acme", await source.SuggestAsync(index, "coffee"));
        }
        finally
        {
            CleanupBlocks(index);
        }
    }

    [DatabaseFact]
    public async Task UnknownIndex_NullOrLongInput_AreEmpty_AndNothingIsRecorded()
    {
        Seed(Index);
        var source = Source(cacheMinutes: 0);

        Assert.Empty(await source.SuggestAsync("unknown-index", "coffee"));
        Assert.Empty(await source.SuggestAsync(string.Empty, "coffee"));
        Assert.Empty(await source.SuggestAsync(Index, null));
        Assert.Empty(await source.SuggestAsync(Index, "c"));
        Assert.Empty(await source.SuggestAsync(Index, "coffee" + new string(' ', ISuiteSuggestions.MaxInputLength)));
        Assert.NotEmpty(await source.SuggestAsync(Index, "coffee" + new string(' ', ISuiteSuggestions.MaxInputLength - 6)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => source.SuggestAsync(Index, "coffee", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => source.SuggestAsync(Index, "coffee", 21));

        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent"));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchInteraction"));
    }

    private static SuggestionSource Source(int cacheMinutes, int maxCandidates = 2_000)
    {
        var services = KenticoServices.Value;
        var options = new SuiteSearchOptions();
        options.Suggestions.CacheMinutes = cacheMinutes;
        options.Suggestions.MaxCandidates = maxCandidates;
        return new SuggestionSource(
            services.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            services.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>(),
            new SuggestIndexes(),
            services.GetRequiredService<IProgressiveCache>(),
            Microsoft.Extensions.Options.Options.Create(options),
            new ManualClock(Now));
    }

    /// <summary>Window 2030-01-01..2030-03-31 (today 2030-04-01); the surfacing threshold is 5 searches on 2 days over all rows.</summary>
    private static void Seed(string index)
    {
        RollupDb.Reset();
        CleanupBlocks(index);
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(
        [
            // Exactly 5 searches, on the first and last window day.
            Row("2030-01-01", "coffee inside", 3, index: index),
            Row("2030-03-31", "coffee inside", 2, index: index),
            // 15 searches, but only 4 inside the window: the days just outside it would push it over MinSearches.
            Row("2029-12-31", "coffee edge", 6, index: index),
            Row("2030-01-01", "coffee edge", 4, index: index),
            Row("2030-04-01", "coffee edge", 5, index: index),
            // No-result rate 0.4 is kept, 0.5 is dropped.
            Row("2030-02-01", "coffee mostly", 5, zero: 2, index: index),
            Row("2030-02-02", "coffee mostly", 5, zero: 2, index: index),
            Row("2030-02-01", "coffee empty", 5, zero: 3, index: index),
            Row("2030-02-02", "coffee empty", 5, zero: 2, index: index),
            Row("2030-02-01", "coffee acme", 4, index: index),
            Row("2030-02-02", "coffee acme", 4, index: index),
            Row("2030-02-01", "best coffee", 4, index: index),
            Row("2030-02-02", "best coffee", 3, index: index),
            Row("2030-02-01", "coffee {number}", 10, index: index),
            Row("2030-02-02", "coffee {number}", 10, index: index),
            Row("2030-02-01", "{email} coffee", 10, index: index),
            Row("2030-02-02", "{email} coffee", 10, index: index),
            // Not surfaced: one day only.
            Row("2030-02-03", "coffee oneday", 9, index: index),
            Row("2030-02-01", "", 10, index: index),
            Row("2030-02-02", "", 10, index: index),
            Row("2030-02-03", null, 30, index: index),
            Row("2030-02-01", "coffee other", 50, index: OtherIndex),
            Row("2030-02-02", "coffee other", 50, index: OtherIndex),
        ]);
    }

    private static BlockedSuggestionInfo Block(string index, string text, string mode) => new()
    {
        IndexName = index,
        Text = text,
        MatchKey = SynonymEngine.MatchKey(text),
        MatchMode = mode,
    };

    private static void CleanupBlocks(params string[] indexes)
    {
        Execute($"DELETE FROM LuceneSuite_BlockedSuggestion WHERE IndexName IN ({string.Join(", ", indexes.Select(index => $"'{index}'"))})");
        CacheHelper.TouchKey($"{BlockedSuggestionInfo.OBJECT_TYPE}|all");
    }

    private static SearchDailyInfo Row(string day, string? queryText, int searches, int zero = 0, string index = Index)
    {
        var buckets = new int[SearchDailyInfo.LatencyBucketCount];
        buckets[0] = searches;
        var info = DailyRollupStore.ToInfo(new DailyKpi(
            DateOnly.Parse(day, CultureInfo.InvariantCulture), index, queryText ?? string.Empty, searches, zero, searches, 0, 0, 0, 0, 0, 1, buckets));
        if (queryText is null)
        {
            info.SetValue(nameof(SearchDailyInfo.QueryText), null);
        }

        return info;
    }

    /// <summary>Every index whose name starts with "suggest-" exists.</summary>
    private sealed class SuggestIndexes : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(string indexName) =>
            // LuceneIndex has only an internal constructor; existence is all the source reads.
            indexName.StartsWith("suggest-", StringComparison.OrdinalIgnoreCase) ? (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex)) : null;

        public LuceneIndex? GetIndex(int identifier) => throw new NotSupportedException();

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }
}
