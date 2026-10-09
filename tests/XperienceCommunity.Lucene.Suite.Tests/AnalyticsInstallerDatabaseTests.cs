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

public sealed class AnalyticsInstallerDatabaseTests
{
    private static readonly Lazy<IServiceProvider> Kentico = new(InitializeKentico);

    [DatabaseFact]
    public void FreshDatabase_ConcurrentInstalls_CreateEachObjectOnce_AndRerunIsIdempotent()
    {
        var services = Kentico.Value;
        RemoveSuiteObjects(keepSearchEvent: false);
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
    public void Version1Database_UpgradesToVersion2_LeavingVersion1ObjectsUntouched()
    {
        var services = Kentico.Value;
        CreateInstaller(services).Install();
        RemoveSuiteObjects(keepSearchEvent: true);
        SetInstalledVersion(1);
        Execute(
            """
            INSERT INTO LuceneSuite_SearchEvent (SearchId, Sequence, ReceivedUtc, IndexName, QueryText, Page, PageSize, FacetFilterCount, ResultCount, LatencyMs, IsSubmit, Origin)
            VALUES (NEWID(), 1, SYSUTCDATETIME(), 'upgrade-test', 'kept', 1, 10, 0, 0, 0, 0, 1)
            """);
        string searchEventDefinition = SearchEventDefinition();
        int searchEventRows = Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent");

        CreateInstaller(services).Install();

        AssertInstalledOnce();
        Assert.Equal(searchEventDefinition, SearchEventDefinition());
        Assert.Equal(searchEventRows, Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchEvent"));
    }

    [DatabaseFact]
    public void NewerInstalledVersion_LogsWarning_AndChangesNothing()
    {
        var services = Kentico.Value;
        CreateInstaller(services).Install();
        RemoveSuiteObjects(keepSearchEvent: true);
        SetInstalledVersion(AnalyticsModuleInstaller.Version + 1);
        var logger = new RecordingLogger();

        CreateInstaller(services, logger).Install();

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal(0, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName = '{SearchInteractionInfo.CLASS_NAME}'"));
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
    private static void RemoveSuiteObjects(bool keepSearchEvent)
    {
        string[] classNames = keepSearchEvent
            ? [SearchInteractionInfo.CLASS_NAME]
            : [SearchInteractionInfo.CLASS_NAME, SearchEventInfo.CLASS_NAME];
        foreach (string className in classNames)
        {
            Execute($"DROP TABLE IF EXISTS [{className.Replace('.', '_')}]; DELETE FROM CMS_Class WHERE ClassName = '{className}';");
            ClassStructureInfo.Remove(className, false);
        }

        if (!keepSearchEvent)
        {
            Execute(
                $"""
                DELETE FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}';
                DELETE FROM CMS_Resource WHERE ResourceName = '{AnalyticsModuleInstaller.ResourceName}';
                """);
        }

        foreach (string objectType in new[] { DataClassInfo.OBJECT_TYPE, ResourceInfo.OBJECT_TYPE, SettingsKeyInfo.OBJECT_TYPE })
        {
            ProviderHelper.ClearHashtables(objectType, false);
        }

        Assert.Equal(keepSearchEvent ? 1 : 0, Scalar("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'LuceneSuite[_]%'"));
    }

    private static void SetInstalledVersion(int version)
    {
        var keys = Kentico.Value.GetRequiredService<IInfoProvider<SettingsKeyInfo>>();
        var key = keys.Get().WhereEquals(nameof(SettingsKeyInfo.KeyName), AnalyticsModuleInstaller.VersionKeyName).First();
        key.KeyValue = version.ToString(CultureInfo.InvariantCulture);
        keys.Set(key);
    }

    private static string SearchEventDefinition()
    {
        using var connection = Open();
        using var command = new SqlCommand($"SELECT ClassFormDefinition FROM CMS_Class WHERE ClassName = '{SearchEventInfo.CLASS_NAME}'", connection);
        return (string)command.ExecuteScalar();
    }

    private static void AssertInstalledOnce()
    {
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Resource WHERE ResourceName = '{AnalyticsModuleInstaller.ResourceName}'"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}' AND KeyValue = '{AnalyticsModuleInstaller.Version}'"));
        foreach (var (className, indexName) in new[]
        {
            (SearchEventInfo.CLASS_NAME, AnalyticsModuleInstaller.ReceivedIndexName),
            (SearchInteractionInfo.CLASS_NAME, AnalyticsModuleInstaller.InteractionReceivedIndexName),
        })
        {
            Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName = '{className}'"));
            Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM sys.tables WHERE name = '{className.Replace('.', '_')}'"));
            Assert.Equal(2, Scalar(
                $"""
                SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.name = '{indexName}' AND ic.is_descending_key = 0
                """));
        }
    }

    private static int Scalar(string sql)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        return (int)command.ExecuteScalar();
    }

    private static void Execute(string sql)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static SqlConnection Open()
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
