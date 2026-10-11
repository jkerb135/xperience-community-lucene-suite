using CMS.DataEngine;
using CMS.Helpers;
using CMS.Modules;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SynonymProviderDatabaseTests
{
    private const string Index = "synonym-test";
    private const string OtherIndex = "synonym-other";

    [DatabaseFact]
    public void Synonyms_AreCachedPerIndex_SkipDisabledAndInvalidRows_AndEveryChangeClearsTheCache()
    {
        var services = KenticoServices.Value;
        new AnalyticsModuleInstaller(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            NullLogger<AnalyticsModuleInstaller>.Instance).Install();
        var infos = services.GetRequiredService<IInfoProvider<SynonymInfo>>();
        var logger = new RecordingLogger();
        var provider = new SynonymProvider(infos, services.GetRequiredService<IProgressiveCache>(), Options.Create(new SuiteSearchOptions()), TimeProvider.System, logger);
        Cleanup();
        try
        {
            var grinder = Synonym(Index, SynonymSerializer.TwoWay, null, """["grinder","mill"]""");
            infos.Set(grinder);
            infos.Set(Synonym(Index, SynonymSerializer.OneWay, "tee", """["t-shirt"]""", enabled: false));
            infos.Set(Synonym(OtherIndex, SynonymSerializer.TwoWay, null, """["coffee","java"]"""));

            Assert.Equal(["mill"], Alternatives(provider.GetSynonyms(Index), "grinder"));
            Assert.Equal(["mill"], Alternatives(provider.GetSynonyms("SYNONYM-TEST"), "grinder"));
            Assert.Empty(Alternatives(provider.GetSynonyms(Index), "tee"));
            Assert.Empty(Alternatives(provider.GetSynonyms(Index), "coffee"));

            grinder.Terms = """["grinder","mill","burr"]""";
            infos.Set(grinder);
            Assert.Equal(["mill", "burr"], Alternatives(provider.GetSynonyms(Index), "grinder"));

            // A raw insert bypasses Kentico, so the cached set stays until the dependency is touched.
            Execute(
                $"""
                INSERT INTO LuceneSuite_Synonym (SynonymGuid, IndexName, Kind, Input, Terms, Enabled, LastModified)
                VALUES (NEWID(), '{Index}', 'replace', '', '["a","b"]', 1, SYSUTCDATETIME())
                """);
            int invalidId = Scalar($"SELECT MAX(SynonymID) FROM LuceneSuite_Synonym WHERE IndexName = '{Index}'");
            Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);

            CacheHelper.TouchKey($"{SynonymInfo.OBJECT_TYPE}|all");
            Assert.Equal(["mill", "burr"], Alternatives(provider.GetSynonyms(Index), "grinder"));
            var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
            Assert.Contains($"{invalidId} (kind)", warning.Message);

            infos.Delete(grinder);
            Assert.Empty(Alternatives(provider.GetSynonyms(Index), "grinder"));
        }
        finally
        {
            Cleanup();
        }
    }

    private static SynonymInfo Synonym(string index, string kind, string? input, string terms, bool enabled = true) => new()
    {
        IndexName = index,
        Kind = kind,
        Input = input ?? string.Empty,
        Terms = terms,
        Enabled = enabled,
    };

    private static string[] Alternatives(SynonymSet set, string term) =>
        set.TryGet(SynonymEngine.MatchKey(term), out var found) ? found.Select(entry => entry.Alternative).ToArray() : [];

    private static void Cleanup()
    {
        Execute($"DELETE FROM LuceneSuite_Synonym WHERE IndexName IN ('{Index}', '{OtherIndex}')");
        CacheHelper.TouchKey($"{SynonymInfo.OBJECT_TYPE}|all");
    }

    private sealed class RecordingLogger : ILogger<SynonymProvider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
