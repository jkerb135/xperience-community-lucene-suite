using CMS.Websites;

using Kentico.Web.Mvc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite;

/// <summary>Registers the Lucene Suite services.</summary>
public static class LuceneSuiteServiceCollectionExtensions
{
    /// <summary>Adds suite search, search event capture and <see cref="ISearchInteractionLogger"/>; call after AddKenticoLucene.</summary>
    public static IServiceCollection AddLuceneSuite(this IServiceCollection services, Action<SuiteSearchOptions>? configure = null)
    {
        services.TryAddSingleton<ISearchQueryFactory, DefaultSearchQueryFactory>();
        services.TryAddSingleton<SuiteSearchService>();
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
        services.Configure<CookieLevelOptions>(cookies =>
            cookies.CookieConfigurations[SearchSessionCookie.Name] = new CookieLevel(CookieLevelConstants.VISITOR));
        var options = services.AddOptions<SuiteSearchOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        return services;
    }
}
