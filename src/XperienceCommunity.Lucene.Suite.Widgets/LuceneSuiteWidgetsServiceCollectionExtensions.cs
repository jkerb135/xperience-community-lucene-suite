using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Events;
using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets;

/// <summary>Widgets package settings.</summary>
public sealed class SuiteWidgetsOptions
{
    /// <summary>Click and conversion events accepted per client IP per minute (0–10 000); 0 turns the limit off, for sites that rate-limit upstream.</summary>
    public int EventsPerMinute { get; set; } = 60;

    /// <summary>Results per page for the search components (1–100). Site-wide because all components on a page share one search.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Whether the components search when the visitor has entered no text and chosen no filter (browse all).</summary>
    public bool SearchOnEmptyQuery { get; set; }

    /// <summary>Whether the current contact's contact groups are sent as search contexts, so rules can target them. A contact is never created for this.</summary>
    public bool ContactGroupContexts { get; set; } = true;
}

/// <summary>Registers the Lucene Suite Widgets services.</summary>
public static class LuceneSuiteWidgetsServiceCollectionExtensions
{
    /// <summary>Adds the services behind <see cref="SuiteEventsEndpoints.MapLuceneSuiteEvents"/>. Call after AddLuceneSuite. Leaves the host's own rate limiter configuration untouched.</summary>
    /// <exception cref="InvalidOperationException">When AddLuceneSuite has not been called.</exception>
    public static IServiceCollection AddLuceneSuiteWidgets(this IServiceCollection services, Action<SuiteWidgetsOptions>? configure = null)
    {
        if (!services.Any(d => d.ServiceType == typeof(ISearchInteractionLogger)))
        {
            throw new InvalidOperationException("Lucene Suite Widgets needs the Lucene Suite services: call services.AddLuceneSuite() before services.AddLuceneSuiteWidgets().");
        }

        var options = services.AddOptions<SuiteWidgetsOptions>()
            .Validate(o => o.EventsPerMinute is >= 0 and <= 10_000, "LuceneSuite Widgets EventsPerMinute must be between 0 and 10 000.")
            .Validate(o => o.PageSize is >= 1 and <= 100, "LuceneSuite Widgets PageSize must be between 1 and 100.")
            .ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SuiteEventsRateLimiter>();
        services.TryAddSingleton<SuiteEventsPath>();
        services.TryAddSingleton<SuiteContactGroupContexts>();
        services.TryAddSingleton(provider => new SuitePageSearch(
            provider.GetRequiredService<ISuiteSearchService>(),
            provider.GetRequiredService<ILuceneIndexManager>(),
            provider.GetRequiredService<SuiteContactGroupContexts>(),
            provider.GetRequiredService<SuiteEventsPath>(),
            provider.GetRequiredService<IOptions<SuiteWidgetsOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SuitePageSearch>>()));
        return services;
    }
}
