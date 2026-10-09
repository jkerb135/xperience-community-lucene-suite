using CMS.Core;
using CMS.DataEngine;
using CMS.Modules;

using Kentico.Xperience.Lucene.Core;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using XperienceCommunity.Lucene.Suite.Analytics;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Runs only when LUCENESUITE_TEST_CONNECTIONSTRING points at an Xperience database (e.g. xbk's).</summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    internal const string Variable = "LUCENESUITE_TEST_CONNECTIONSTRING";

    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"Set {Variable} to an Xperience database connection string to run.";
        }
    }
}

public sealed class AnalyticsInstallerDatabaseTests
{
    private static readonly Lazy<IServiceProvider> Kentico = new(InitializeKentico);

    [DatabaseFact]
    public void ConcurrentInstalls_CreateEachObjectOnce_AndRerunIsIdempotent()
    {
        var services = Kentico.Value;
        var installers = Enumerable.Range(0, 4)
            .Select(_ => new AnalyticsModuleInstaller(
                services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
                services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>()))
            .ToArray();
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

    private static void AssertInstalledOnce()
    {
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Resource WHERE ResourceName = '{AnalyticsModuleInstaller.ResourceName}'"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_Class WHERE ClassName = '{SearchEventInfo.CLASS_NAME}'"));
        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM CMS_SettingsKey WHERE KeyName = '{AnalyticsModuleInstaller.VersionKeyName}' AND KeyValue = '{AnalyticsModuleInstaller.Version}'"));
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sys.tables WHERE name = 'LuceneSuite_SearchEvent'"));
        Assert.Equal(2, Scalar(
            $"""
            SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.name = '{AnalyticsModuleInstaller.ReceivedIndexName}' AND ic.is_descending_key = 0
            """));
    }

    private static int Scalar(string sql)
    {
        using var connection = new SqlConnection(Environment.GetEnvironmentVariable(DatabaseFactAttribute.Variable));
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        return (int)command.ExecuteScalar();
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
