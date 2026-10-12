using System.Globalization;
using System.Runtime.CompilerServices;

using CMS.DataEngine;
using CMS.Helpers;

using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Admin.Suggestions;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Suggestions;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class BlockedSuggestionAdminDatabaseTests
{
    private const string Index = "block-admin-test";
    private const string OtherIndex = "block-admin-other";

    [DatabaseFact]
    public void Block_StoresOneExactEntry_AndASecondCallReportsAlreadyBlocked()
    {
        Cleanup();
        try
        {
            var first = SearchReportsPage.Block(Provider, Index, "  Cofee ");
            var second = SearchReportsPage.Block(Provider, Index, "COFEE!");

            Assert.True(first.Blocked);
            Assert.Null(first.Error);
            Assert.Contains("\"Cofee\"", first.Message, StringComparison.Ordinal);
            Assert.Contains("Blocked suggestions", first.Message, StringComparison.Ordinal);
            Assert.Equal((false, true, null), (second.Blocked, second.AlreadyBlocked, second.Error));

            var stored = Assert.Single(Entries(Index));
            Assert.Equal(("Cofee", "cofee", BlockedSuggestionInfo.MatchModeExact), (stored.Text, stored.MatchKey, stored.MatchMode));
            Assert.Empty(Entries(OtherIndex));

            // The same text in another index, or as a word entry, is a different entry.
            Assert.True(SearchReportsPage.Block(Provider, OtherIndex, "cofee").Blocked);
            Assert.False(BlockedSuggestionValidation.Exists(Provider, Index, "cofee", BlockedSuggestionInfo.MatchModeWord));
            Assert.True(BlockedSuggestionValidation.Exists(Provider, Index, "cofee", BlockedSuggestionInfo.MatchModeExact));
        }
        finally
        {
            Cleanup();
        }
    }

    [DatabaseFact]
    public void Block_RefusesInvalidTextAndAMissingIndex_AndStoresNothing()
    {
        Cleanup();
        try
        {
            foreach (string? query in new[] { null, "", "  !? ", "coffee {email}", "{number}", new string('a', 101) })
            {
                var result = SearchReportsPage.Block(Provider, Index, query);
                Assert.False(result.Blocked);
                Assert.NotNull(result.Error);
            }

            Assert.Equal(BlockedSuggestionValidation.IndexMissingMessage, SearchReportsPage.Block(Provider, null, "coffee").Error);
            Assert.Equal(BlockedSuggestionValidation.IndexMissingMessage, SearchReportsPage.Block(Provider, string.Empty, "coffee").Error);
            Assert.Empty(Entries(Index));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM LuceneSuite_BlockedSuggestion WHERE IndexName = ''"));
        }
        finally
        {
            Cleanup();
        }
    }

    [DatabaseFact]
    public async Task Block_RemovesTheSuggestionAtOnce_AndDeletingTheEntryRestoresIt()
    {
        // A fresh index name, so no earlier test's cached candidates are reused.
        string index = "suggest-block-" + Guid.NewGuid().ToString("N");
        RollupDb.Reset();
        KenticoServices.Value.GetRequiredService<IInfoProvider<SearchDailyInfo>>().BulkInsert(
            [Row("2030-02-01", "cofee", index), Row("2030-02-02", "cofee", index), Row("2030-02-01", "coffee", index), Row("2030-02-02", "coffee", index)]);
        var source = Source();
        try
        {
            Assert.Equal(["cofee", "coffee"], (await source.SuggestAsync(index, "cof")).Order().ToList());

            Assert.True(SearchReportsPage.Block(Provider, index, "cofee").Blocked);
            Assert.Equal(["coffee"], await source.SuggestAsync(index, "cof"));

            Provider.Delete(Assert.Single(Entries(index)));
            Assert.Contains("cofee", await source.SuggestAsync(index, "cof"));
        }
        finally
        {
            Cleanup(index);
        }
    }

    private static IInfoProvider<BlockedSuggestionInfo> Provider => KenticoServices.Value.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>();

    private static List<BlockedSuggestionInfo> Entries(string index) =>
        Provider.Get().WhereEquals(nameof(BlockedSuggestionInfo.IndexName), index).ToList();

    private static void Cleanup(params string[] extra)
    {
        // TouchKey needs Kentico initialized, which the first test of a run may not have done yet.
        _ = KenticoServices.Value;
        string[] indexes = [Index, OtherIndex, .. extra];
        Execute($"DELETE FROM LuceneSuite_BlockedSuggestion WHERE IndexName IN ({string.Join(", ", indexes.Select(index => $"'{index}'"))})");
        CacheHelper.TouchKey($"{BlockedSuggestionInfo.OBJECT_TYPE}|all");
    }

    private static SuggestionSource Source()
    {
        var services = KenticoServices.Value;
        var options = new SuiteSearchOptions();
        options.Suggestions.CacheMinutes = 60;
        return new SuggestionSource(
            services.GetRequiredService<IInfoProvider<SearchDailyInfo>>(),
            services.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>(),
            new AnyIndex(),
            services.GetRequiredService<IProgressiveCache>(),
            Microsoft.Extensions.Options.Options.Create(options),
            new ManualClock(DateTimeOffset.Parse("2030-04-01T12:00:00Z", CultureInfo.InvariantCulture)));
    }

    /// <summary>Surfaced (5+ searches on 2+ days) inside the default window of 2030-04-01.</summary>
    private static SearchDailyInfo Row(string day, string queryText, string index)
    {
        var buckets = new int[SearchDailyInfo.LatencyBucketCount];
        buckets[0] = 4;
        return DailyRollupStore.ToInfo(new DailyKpi(
            DateOnly.Parse(day, CultureInfo.InvariantCulture), index, queryText, 4, 0, 4, 0, 0, 0, 0, 0, 1, buckets));
    }

    /// <summary>Every index exists; existence is all the source reads.</summary>
    private sealed class AnyIndex : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(string indexName) => (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex));

        public LuceneIndex? GetIndex(int identifier) => throw new NotSupportedException();

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }
}
