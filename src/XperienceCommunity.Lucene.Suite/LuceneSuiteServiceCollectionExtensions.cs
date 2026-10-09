using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite;

/// <summary>Registers the Lucene Suite services.</summary>
public static class LuceneSuiteServiceCollectionExtensions
{
    /// <summary>Adds suite search; call after AddKenticoLucene.</summary>
    public static IServiceCollection AddLuceneSuite(this IServiceCollection services, Action<SuiteSearchOptions>? configure = null)
    {
        services.TryAddSingleton<ISearchQueryFactory, DefaultSearchQueryFactory>();
        services.TryAddSingleton<ISuiteSearchService, SuiteSearchService>();
        var options = services.AddOptions<SuiteSearchOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        return services;
    }
}
