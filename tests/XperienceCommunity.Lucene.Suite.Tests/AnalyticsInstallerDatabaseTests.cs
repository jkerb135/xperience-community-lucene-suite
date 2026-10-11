using System.Globalization;

using CMS.Core;
using CMS.DataEngine;
using CMS.Modules;

using Kentico.Xperience.Lucene.Core;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Synonyms;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Runs only when LUCENESUITE_TEST_CONNECTIONSTRING points at a disposable Xperience database whose name contains "test".</summary>
/// <remarks>These tests delete and recreate the Suite objects, so they refuse any other database.</remarks>
public sealed class DatabaseFactAttribute : FactAttribute
{
    internal const string Variable = "LUCENESUITE_TEST_CONNECTIONSTRING";

    public DatabaseFactAttribute()
    {
        string? connectionString = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = $"Set {Variable} to a disposable Xperience database connection string to run.";
        }
        else if (!new SqlConnectionStringBuilder(connectionString).InitialCatalog.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"{Variable} must name a disposable database (catalog name containing 'test'); these tests drop Suite objects.";
        }
    }
}

/// <summary>Shared Kentico initialization and raw SQL helpers for the database tests.</summary>
internal static class DatabaseTestHost
{
    public static readonly Lazy<IServiceProvider> KenticoServices = new(InitializeKentico);

    public static int Scalar(string sql)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        return (int)command.ExecuteScalar();
    }

    public static void Execute(string sql)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static SqlConnection Open()
    {
        var connection = new SqlConnection(Environment.GetEnvironmentVariable(DatabaseFactAttribute.Variable));
        connection.Open();
        return connection;
    }

    // Kentico's documented external-application initialization; without AddLuceneSuite the module installs nothing itself.
    private static IServiceProvider InitializeKentico()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CMSConnectionString"] = Environment.GetEnvironmentVariable(DatabaseFactAttribute.Variable),
        });
        // Kentico Lucene's module, discovered from the referenced packages, needs its own services.
        builder.Services.AddKenticoLucene();
        CMSApplication.PreInit(false);
        Service.MergeDescriptors(builder.Services);
        var host = builder.Build();
        Service.SetProvider(host.Services);
        CMSApplication.Init();
        return host.Services;
    }
}

/// <summary>Database tests must not run in parallel: some drop and recreate the Suite tables.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection
{
    internal const string Name = "Database";
}

[Collection(DatabaseCollection.Name)]
public sealed class AnalyticsInstallerDatabaseTests
{
    [DatabaseFact]
    public void FreshDatabase_ConcurrentInstalls_CreateEachObjectOnce_AndRerunIsIdempotent()
    {
        var services = KenticoServices.Value;
        RemoveSuiteObjects(keep: []);
        var installers = Enumerable.Range(0, 4).Select(_ => CreateInstaller(services)).ToArray();
        using var start = new Barrier(installers.Length);

        Task.WaitAll(installers.Select(installer => Task.Run(() =>
        {
            start.SignalAndWait();
            installer.Install();
        })).ToArray());
        AssertInstalledOnce();

        installers[0].Install();
        AssertInstalledOnce();
    }

    [DatabaseFact]
    public void Version1Database_UpgradesToCurrent_LeavingVersion1ObjectsUntouched()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        RemoveSuiteObjects(keep: [SearchEventInfo.CLASS_NAME]);
        SetInstalledVersion(1);
        Execute(
            """
            INSERT INTO LuceneSuite_SearchEvent (SearchId, Sequence, ReceivedUtc, IndexName, QueryText, Page, PageSize, FacetFilterCount, ResultCount, LatencyMs, IsSubmit, Origin)
            VALUES (NEWID(), 1, SYSUTCDATETIME(), 'upgrade-test', 'kept', 1, 10, 0, 0, 0, 0, 1)
            """);
        string searchEventDefinition = ClassDefinition(SearchEventInfo.CLASS_NAME);
        int searchEventRows = Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent");

        CreateInstaller(services).Install();

        AssertInstalledOnce();
        Assert.Equal(searchEventDefinition, ClassDefinition(SearchEventInfo.CLASS_NAME));
        Assert.Equal(searchEventRows, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent"));
    }

    [DatabaseFact]
    public void Version2Database_UpgradesToVersion3_LeavingVersion2ObjectsUntouched()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        RemoveSuiteObjects(keep: [SearchEventInfo.CLASS_NAME, SearchInteractionInfo.CLASS_NAME]);
        SetInstalledVersion(2);
        Execute(
            """
            INSERT INTO LuceneSuite_SearchEvent (SearchId, Sequence, ReceivedUtc, IndexName, QueryText, Page, PageSize, FacetFilterCount, ResultCount, LatencyMs, IsSubmit, Origin)
            VALUES (NEWID(), 1, SYSUTCDATETIME(), 'upgrade-test', 'kept', 1, 10, 0, 0, 0, 0, 1);
            INSERT INTO LuceneSuite_SearchInteraction (SearchId, Sequence, ItemGuid, Position, Type, EventId, ReceivedUtc)
            VALUES (NEWID(), 1, NEWID(), 1, 1, NEWID(), SYSUTCDATETIME());
            """);
        string[] definitions = [ClassDefinition(SearchEventInfo.CLASS_NAME), ClassDefinition(SearchInteractionInfo.CLASS_NAME)];
        int[] rows = [Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent"), Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchInteraction")];

        CreateInstaller(services).Install();

        AssertInstalledOnce();
        Assert.Equal(definitions, new[] { ClassDefinition(SearchEventInfo.CLASS_NAME), ClassDefinition(SearchInteractionInfo.CLASS_NAME) });
        Assert.Equal(rows, new[] { Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent"), Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchInteraction") });
    }

    [DatabaseFact]
    public void Version3Database_UpgradesToVersion4_LeavingVersion3ObjectsUntouched()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        string[] kept = [SearchEventInfo.CLASS_NAME, SearchInteractionInfo.CLASS_NAME, SearchDailyInfo.CLASS_NAME];
        RemoveSuiteObjects(keep: kept);
        SetInstalledVersion(3);
        string[] definitions = kept.Select(ClassDefinition).ToArray();

        CreateInstaller(services).Install();

        AssertInstalledOnce();
        Assert.Equal(definitions, kept.Select(ClassDefinition).ToArray());
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName = '{RuleInfo.CLASS_NAME}' AND ClassDisplayName = 'Lucene Suite search rule'"));
    }

    [DatabaseFact]
    public void Version4Database_UpgradesToVersion5_LeavingVersion4ObjectsUntouched()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        string[] kept = [SearchEventInfo.CLASS_NAME, SearchInteractionInfo.CLASS_NAME, SearchDailyInfo.CLASS_NAME, RuleInfo.CLASS_NAME];
        RemoveSuiteObjects(keep: kept);
        SetInstalledVersion(4);
        string[] definitions = kept.Select(ClassDefinition).ToArray();

        CreateInstaller(services).Install();
        CreateInstaller(services).Install();

        AssertInstalledOnce();
        Assert.Equal(definitions, kept.Select(ClassDefinition).ToArray());
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName = '{SynonymInfo.CLASS_NAME}' AND ClassDisplayName = 'Lucene Suite synonym'"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM sys.indexes WHERE name = '{AnalyticsModuleInstaller.SynonymIndexName}'"));
    }

    [DatabaseFact]
    public void RuleInfo_RoundTripsThroughToDefinition()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        var rules = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        Execute("DELETE FROM LuceneSuite_Rule WHERE IndexName = 'rule-test'");
        var pinned = Guid.NewGuid();
        var rule = new RuleInfo
        {
            IndexName = "rule-test",
            DisplayName = "Pin the grinder guide",
            Enabled = true,
            Priority = 2,
            ValidFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Conditions = """[{"anchoring":"contains","pattern":"Coffee Grinder"}]""",
            Actions = $$"""{"pins":[{"itemGuid":"{{pinned}}","position":1}]}""",
        };

        rules.Set(rule);
        var stored = rules.Get().WhereEquals(nameof(RuleInfo.RuleID), rule.RuleID).First();
        var result = stored.ToDefinition();

        Assert.NotEqual(Guid.Empty, stored.RuleGuid);
        Assert.Null(stored.Description);
        Assert.Null(stored.ValidUntilUtc);
        Assert.Empty(result.Errors);
        var definition = Assert.IsType<RuleDefinition>(result.Definition);
        Assert.Equal((rule.RuleID, "rule-test", 2, true), (definition.Id, definition.IndexName, definition.Priority, definition.Enabled));
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), definition.ValidFromUtc);
        Assert.Equal(DateTimeKind.Utc, definition.ValidFromUtc!.Value.Kind);
        Assert.Equal("coffee grinder", Assert.Single(definition.Conditions).Pattern);
        Assert.Equal(new RulePin(pinned, 1), Assert.Single(definition.Actions.Pins));
        Execute("DELETE FROM LuceneSuite_Rule WHERE IndexName = 'rule-test'");
    }

    [DatabaseFact]
    public void StoredRuleWithInvalidJson_YieldsErrors_NotAnException()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        var rules = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        Execute("DELETE FROM LuceneSuite_Rule WHERE IndexName = 'rule-test'");
        Execute(
            """
            INSERT INTO LuceneSuite_Rule (RuleGuid, IndexName, DisplayName, Enabled, Priority, Conditions, Actions, LastModified)
            VALUES (NEWID(), 'rule-test', 'Hand-edited', 1, 1, '[]', '{"pins":[{"itemGuid":', SYSUTCDATETIME())
            """);

        var stored = rules.Get().WhereEquals(nameof(RuleInfo.IndexName), "rule-test").First();
        var result = stored.ToDefinition();

        Assert.Null(result.Definition);
        Assert.Equal("actions", Assert.Single(result.Errors).Path);
        Execute("DELETE FROM LuceneSuite_Rule WHERE IndexName = 'rule-test'");
    }

    [DatabaseFact]
    public void SearchDaily_RejectsADuplicateDayIndexAndQuery()
    {
        CreateInstaller(KenticoServices.Value).Install();
        Execute("DELETE FROM LuceneSuite_SearchDaily WHERE IndexName = 'unique-test'");
        const string insert =
            """
            INSERT INTO LuceneSuite_SearchDaily (Day, IndexName, QueryText, Searches, ZeroResultSearches, SubmittedSearches, ClickedSearches, Clicks, ClickPositionSum, ConvertedSearches, Conversions, SessionCount,
                LatencyBucket0, LatencyBucket1, LatencyBucket2, LatencyBucket3, LatencyBucket4, LatencyBucket5, LatencyBucket6, LatencyBucket7, LatencyBucket8, LatencyBucket9)
            VALUES ('2026-10-09', 'unique-test', 'coffee', 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            """;
        Execute(insert);

        Assert.Throws<SqlException>(() => Execute(insert));
        Execute("DELETE FROM LuceneSuite_SearchDaily WHERE IndexName = 'unique-test'");
    }

    [DatabaseFact]
    public void NewerInstalledVersion_LogsWarning_AndChangesNothing()
    {
        var services = KenticoServices.Value;
        CreateInstaller(services).Install();
        RemoveSuiteObjects(keep: [SearchEventInfo.CLASS_NAME]);
        SetInstalledVersion(AnalyticsModuleInstaller.Version + 1);
        var logger = new RecordingLogger();

        CreateInstaller(services, logger).Install();

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal(0, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName IN ('{SearchInteractionInfo.CLASS_NAME}', '{SearchDailyInfo.CLASS_NAME}', '{RuleInfo.CLASS_NAME}')"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}' AND KeyValue = '{AnalyticsModuleInstaller.Version + 1}'"));

        // Leave the database fully installed for the other tests.
        SetInstalledVersion(1);
        CreateInstaller(services).Install();
    }

    private static AnalyticsModuleInstaller CreateInstaller(IServiceProvider services, ILogger<AnalyticsModuleInstaller>? logger = null) =>
        new(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            logger ?? NullLogger<AnalyticsModuleInstaller>.Instance);

    // Raw SQL because Kentico refuses to delete a class whose Info type is compiled in; the caches are cleared afterwards.
    private static void RemoveSuiteObjects(string[] keep)
    {
        string[] all = [SynonymInfo.CLASS_NAME, RuleInfo.CLASS_NAME, SearchDailyInfo.CLASS_NAME, SearchInteractionInfo.CLASS_NAME, SearchEventInfo.CLASS_NAME];
        foreach (string className in all.Except(keep))
        {
            Execute($"DROP TABLE IF EXISTS [{className.Replace('.', '_')}]; DELETE FROM CMS_Class WHERE ClassName = '{className}';");
            ClassStructureInfo.Remove(className, false);
        }

        if (keep.Length == 0)
        {
            Execute(
                $"""
                DELETE FROM CMS_SettingsKey WHERE KeyName IN ('{AnalyticsModuleInstaller.VersionKeyName}', '{AnalyticsModuleInstaller.WatermarkKeyName}');
                DELETE FROM CMS_Resource WHERE ResourceName = '{AnalyticsModuleInstaller.ResourceName}';
                """);
        }

        foreach (string objectType in new[] { DataClassInfo.OBJECT_TYPE, ResourceInfo.OBJECT_TYPE, SettingsKeyInfo.OBJECT_TYPE })
        {
            ProviderHelper.ClearHashtables(objectType, false);
        }

        Assert.Equal(keep.Length, Scalar("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'LuceneSuite[_]%'"));
    }

    private static void SetInstalledVersion(int version)
    {
        var keys = KenticoServices.Value.GetRequiredService<IInfoProvider<SettingsKeyInfo>>();
        var key = keys.Get().WhereEquals(nameof(SettingsKeyInfo.KeyName), AnalyticsModuleInstaller.VersionKeyName).First();
        key.KeyValue = version.ToString(CultureInfo.InvariantCulture);
        keys.Set(key);
    }

    private static string ClassDefinition(string className)
    {
        using var connection = Open();
        using var command = new SqlCommand($"SELECT ClassFormDefinition FROM CMS_Class WHERE ClassName = '{className}'", connection);
        return (string)command.ExecuteScalar();
    }

    private static void AssertInstalledOnce()
    {
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Resource WHERE ResourceName = '{AnalyticsModuleInstaller.ResourceName}'"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}' AND KeyValue = '{AnalyticsModuleInstaller.Version}'"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.WatermarkKeyName}'"));
        foreach (var (className, indexName, columns, unique) in new[]
        {
            (SearchEventInfo.CLASS_NAME, AnalyticsModuleInstaller.ReceivedIndexName, 2, 0),
            (SearchInteractionInfo.CLASS_NAME, AnalyticsModuleInstaller.InteractionReceivedIndexName, 2, 0),
            (SearchDailyInfo.CLASS_NAME, AnalyticsModuleInstaller.DailyKeyIndexName, 3, 1),
            (RuleInfo.CLASS_NAME, AnalyticsModuleInstaller.RuleIndexName, 1, 0),
            (SynonymInfo.CLASS_NAME, AnalyticsModuleInstaller.SynonymIndexName, 1, 0),
        })
        {
            Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName = '{className}'"));
            Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM sys.tables WHERE name = '{className.Replace('.', '_')}'"));
            Assert.Equal(columns, Scalar(
                $"""
                SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.name = '{indexName}' AND ic.is_descending_key = 0 AND i.is_unique = {unique}
                """));
        }
    }

    private sealed class RecordingLogger : ILogger<AnalyticsModuleInstaller>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
