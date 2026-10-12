using CMS.Websites;

using Kentico.Web.Mvc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite;

/// <summary>Registers the Lucene Suite services.</summary>
public static class LuceneSuiteServiceCollectionExtensions
{
    /// <summary>Adds suite search, search event capture, <see cref="ISearchInteractionLogger"/> and the daily rollup and retention job; options bind from configuration section "LuceneSuite", then <paramref name="configure"/>, and are validated on start. Call after AddKenticoLucene.</summary>
    public static IServiceCollection AddLuceneSuite(this IServiceCollection services, Action<SuiteSearchOptions>? configure = null)
    {
        services.TryAddSingleton<ISearchQueryFactory, DefaultSearchQueryFactory>();
        services.TryAddSingleton<SuiteSearchService>();
        services.TryAddSingleton<RuleProvider>();
        services.TryAddSingleton<SynonymProvider>();
        services.TryAddSingleton<SuiteItemLookup>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddSingleton<SearchSessionCookie>();
        services.TryAddSingleton<SearchEventQueue>();
        services.TryAddSingleton<AnalyticsRequestFilter>();
        services.TryAddSingleton<ISearchInteractionLogger, SearchInteractionLogger>();
        services.TryAddSingleton<AnalyticsModuleInstaller>();
        services.TryAddSingleton<ISuiteSearchService>(provider => ActivatorUtilities.CreateInstance<SearchCaptureService>(
            provider, provider.GetRequiredService<SuiteSearchService>()));
        services.AddHostedService<SearchEventWriter>();
        services.TryAddSingleton<DailyRollupStore>();
        services.TryAddSingleton<AnalyticsRetention>();
        services.TryAddSingleton<SearchReportService>();
        services.AddHostedService<AnalyticsRollupJob>();
        services.Configure<CookieLevelOptions>(cookies =>
            cookies.CookieConfigurations[SearchSessionCookie.Name] = new CookieLevel(CookieLevelConstants.VISITOR));
        // Bound by hand rather than BindConfiguration so a container without IConfiguration still works.
        var options = services.AddOptions<SuiteSearchOptions>()
            .Configure<IServiceProvider>((o, provider) => provider.GetService<IConfiguration>()?.GetSection(SuiteSearchOptions.SectionName).Bind(o))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SuiteSearchOptions>, SuiteSearchOptionsValidator>());
        if (configure is not null)
        {
            options.Configure(configure);
        }

        return services;
    }
}
