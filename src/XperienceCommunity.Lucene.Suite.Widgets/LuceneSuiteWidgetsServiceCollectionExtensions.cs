using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Events;
using XperienceCommunity.Lucene.Suite.Widgets.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Suggestions;

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

    /// <summary>Indexes (case-insensitive) the suggest endpoint serves; any other index gets 404. Empty (the default) turns the endpoint off.</summary>
    public string[] SuggestionIndexes { get; set; } = [];

    /// <summary>Suggest requests accepted per client IP per minute (0–10 000), separate from <see cref="EventsPerMinute"/>; 0 turns the limit off. Default 300.</summary>
    public int SuggestionsPerMinute { get; set; } = 300;

    /// <summary>How long browsers and CDNs may cache a suggest response (0–3600 s), so also how long a blocklist change can take to reach a visitor; 0 sends no-store. Default 60.</summary>
    public int SuggestionsMaxAgeSeconds { get; set; } = 60;

    /// <summary>Whether a search box with suggestions references the shipped <c>lucene-suite-suggest.css</c>. Turn off to style the documented class names yourself. Default true.</summary>
    public bool SuggestionStyles { get; set; } = true;
}

/// <summary>Registers the Lucene Suite Widgets services.</summary>
public static class LuceneSuiteWidgetsServiceCollectionExtensions
{
    /// <summary>Adds the services behind <see cref="SuiteEventsEndpoints.MapLuceneSuiteEvents"/> and <see cref="SuiteSuggestEndpoints.MapLuceneSuiteSuggestions"/>. Call after AddLuceneSuite. Leaves the host's own rate limiter configuration untouched.</summary>
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
            .Validate(o => o.SuggestionIndexes is not null, "LuceneSuite Widgets SuggestionIndexes must not be null.")
            .Validate(o => o.SuggestionsPerMinute is >= 0 and <= 10_000, "LuceneSuite Widgets SuggestionsPerMinute must be between 0 and 10 000.")
            .Validate(o => o.SuggestionsMaxAgeSeconds is >= 0 and <= 3600, "LuceneSuite Widgets SuggestionsMaxAgeSeconds must be between 0 and 3600.")
            .ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SuiteEventsRateLimiter>();
        services.TryAddSingleton<SuiteSuggestRateLimiter>();
        services.TryAddSingleton<SuiteEventsPath>();
        services.TryAddSingleton<SuiteSuggestPath>();
        services.TryAddSingleton<SuiteContactGroupContexts>();
        services.TryAddSingleton<ISuitePreviewState, KenticoPreviewState>();
        services.TryAddSingleton(provider => new SuitePageSearch(
            provider.GetRequiredService<ISuiteSearchService>(),
            provider.GetRequiredService<ILuceneIndexManager>(),
            provider.GetRequiredService<SuiteContactGroupContexts>(),
            provider.GetRequiredService<SuiteEventsPath>(),
            provider.GetRequiredService<ISuitePreviewState>(),
            provider.GetRequiredService<IOptions<SuiteWidgetsOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SuitePageSearch>>()));
        return services;
    }
}
