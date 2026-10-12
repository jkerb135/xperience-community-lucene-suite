using CMS.DataEngine;
using CMS.Helpers;
using CMS.Modules;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class RuleProviderDatabaseTests
{
    private const string Index = "provider-test";
    private const string OtherIndex = "provider-other";

    [DatabaseFact]
    public void Rules_AreCachedPerIndex_AndEveryChangeClearsTheCache()
    {
        var services = KenticoServices.Value;
        new AnalyticsModuleInstaller(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            services.GetRequiredService<IInfoProvider<CMS.Activities.ActivityTypeInfo>>(),
            NullLogger<AnalyticsModuleInstaller>.Instance).Install();
        var infos = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        var logger = new RecordingLogger();
        var provider = new RuleProvider(infos, services.GetRequiredService<IProgressiveCache>(), Options.Create(new SuiteSearchOptions()), TimeProvider.System, logger);
        Cleanup();
        try
        {
            var first = Rule(Index, priority: 1);
            var second = Rule(Index, priority: 2);
            infos.Set(first);
            infos.Set(second);
            infos.Set(Rule(OtherIndex, priority: 1));

            Assert.Equal([first.RuleID, second.RuleID], Ids(provider.GetRules(Index)));
            Assert.Equal([first.RuleID, second.RuleID], Ids(provider.GetRules("PROVIDER-TEST")));

            first.Priority = 5;
            infos.Set(first);
            Assert.Equal(5, provider.GetRules(Index).Single(rule => rule.Id == first.RuleID).Priority);

            infos.Delete(second);
            Assert.Equal([first.RuleID], Ids(provider.GetRules(Index)));

            // A raw insert bypasses Kentico, so the cached set stays until the dependency is touched.
            Execute(
                $$"""
                INSERT INTO LuceneSuite_Rule (RuleGuid, IndexName, DisplayName, Enabled, Priority, Conditions, Actions, LastModified)
                VALUES (NEWID(), '{{Index}}', 'Hand-edited', 1, 1, '[]', '{"pins":[{"itemGuid":', SYSUTCDATETIME())
                """);
            int invalidId = Scalar($"SELECT MAX(RuleID) FROM LuceneSuite_Rule WHERE IndexName = '{Index}'");
            Assert.Equal([first.RuleID], Ids(provider.GetRules(Index)));
            Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);

            CacheHelper.TouchKey($"{RuleInfo.OBJECT_TYPE}|all");
            Assert.Equal([first.RuleID], Ids(provider.GetRules(Index)));
            var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
            Assert.Contains($"{invalidId} (actions)", warning.Message);
        }
        finally
        {
            Cleanup();
        }
    }

    [DatabaseFact]
    public void Ownership_FindsARuleOnlyInItsOwnIndex()
    {
        var services = KenticoServices.Value;
        new AnalyticsModuleInstaller(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            services.GetRequiredService<IInfoProvider<CMS.Activities.ActivityTypeInfo>>(),
            NullLogger<AnalyticsModuleInstaller>.Instance).Install();
        var infos = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        Cleanup();
        try
        {
            var rule = Rule(Index, priority: 1);
            infos.Set(rule);

            Assert.NotNull(Admin.UIPages.RuleOwnership.Find(infos, rule.RuleID, Index));
            Assert.NotNull(Admin.UIPages.RuleOwnership.Find(infos, rule.RuleID, Index.ToUpperInvariant()));
            Assert.Null(Admin.UIPages.RuleOwnership.Find(infos, rule.RuleID, OtherIndex));
            Assert.Null(Admin.UIPages.RuleOwnership.Find(infos, rule.RuleID, string.Empty));
            Assert.Null(Admin.UIPages.RuleOwnership.Find(infos, int.MaxValue, Index));
        }
        finally
        {
            Cleanup();
        }
    }

    private static RuleInfo Rule(string index, int priority) => new()
    {
        IndexName = index,
        DisplayName = $"{index} {priority}",
        Enabled = true,
        Priority = priority,
        Conditions = "[]",
        Actions = $$"""{"hides":["{{Guid.NewGuid()}}"]}""",
    };

    private static int[] Ids(IReadOnlyList<RuleDefinition> rules) => rules.Select(rule => rule.Id).Order().ToArray();

    private static void Cleanup()
    {
        Execute($"DELETE FROM LuceneSuite_Rule WHERE IndexName IN ('{Index}', '{OtherIndex}')");
        CacheHelper.TouchKey($"{RuleInfo.OBJECT_TYPE}|all");
    }

    private sealed class RecordingLogger : ILogger<RuleProvider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
