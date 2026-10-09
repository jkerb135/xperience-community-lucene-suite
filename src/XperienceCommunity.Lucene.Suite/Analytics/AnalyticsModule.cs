using CMS;
using CMS.Base;
using CMS.Core;
using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Analytics;

[assembly: AssemblyDiscoverable]
[assembly: RegisterModule(typeof(AnalyticsModule))]

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Installs the analytics classes on application start.</summary>
internal sealed class AnalyticsModule : Module
{
    private IServiceProvider? services;

    public AnalyticsModule()
        : base("XperienceCommunity.Lucene.Suite.Analytics")
    {
    }

    protected override void OnInit(ModuleInitParameters parameters)
    {
        base.OnInit(parameters);
        services = parameters.Services;
        ApplicationEvents.Initialized.Execute += (_, _) => Install();
    }

    private void Install()
    {
        // Registered only by AddLuceneSuite; without it there is nothing to install.
        if (services?.GetService<AnalyticsModuleInstaller>() is not { } installer)
        {
            return;
        }

        try
        {
            installer.Install();
        }
        catch (Exception exception)
        {
            // Search must keep working without analytics; the writer logs failed batches separately.
            services.GetService<ILogger<AnalyticsModule>>()?.LogError(exception, "Lucene Suite analytics installation failed; search events will not be stored.");
        }
    }
}
