using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Widgets.Events;

namespace XperienceCommunity.Lucene.Suite.Widgets;

/// <summary>Widgets package settings.</summary>
public sealed class SuiteWidgetsOptions
{
    /// <summary>Click and conversion events accepted per client IP per minute (0–10 000); 0 turns the limit off, for sites that rate-limit upstream.</summary>
    public int EventsPerMinute { get; set; } = 60;
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
            .ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SuiteEventsRateLimiter>();
        return services;
    }
}
