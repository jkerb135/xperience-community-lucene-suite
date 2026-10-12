using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Activities;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Validates interaction calls synchronously, then buffers them for <see cref="SearchEventWriter"/>; capture failures never reach the caller.</summary>
internal sealed class SearchInteractionLogger(
    SearchEventQueue queue,
    SearchSessionCookie sessionCookie,
    AnalyticsRequestFilter requestFilter,
    SearchActivityLogger activityLogger,
    TimeProvider timeProvider,
    ILogger<SearchInteractionLogger> logger) : ISearchInteractionLogger
{
    private readonly LogThrottle logThrottle = new(timeProvider, TimeSpan.FromMinutes(1));

    public void LogClick(SearchClick click)
    {
        ArgumentNullException.ThrowIfNull(click);
        Validate(click.SearchId, click.Sequence, click.ItemGuid, click.Position, click.EventId);
        if (Capture(click.SearchId, click.Sequence, click.ItemGuid, click.Position, SearchInteractionType.Click, click.EventId))
        {
            activityLogger.LogClick(click.ItemGuid, click.Position, click.QueryText, click.ResultTitle);
        }
    }

    public void LogConversion(Guid searchId, int sequence, Guid itemGuid, int? position = null, Guid? eventId = null)
    {
        Validate(searchId, sequence, itemGuid, position, eventId);
        Capture(searchId, sequence, itemGuid, position, SearchInteractionType.Conversion, eventId);
    }

    private static void Validate(Guid searchId, int sequence, Guid itemGuid, int? position, Guid? eventId)
    {
        if (searchId == Guid.Empty)
        {
            throw new ArgumentException("searchId must not be an empty GUID.", nameof(searchId));
        }
        if (itemGuid == Guid.Empty)
        {
            throw new ArgumentException("itemGuid must not be an empty GUID.", nameof(itemGuid));
        }
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("eventId must not be an empty GUID.", nameof(eventId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        if (position is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(position));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SearchExecutor.MaxResultWindow, nameof(position));
        }
    }

    /// <summary>False when the request filter excluded the event.</summary>
    private bool Capture(Guid searchId, int sequence, Guid itemGuid, int? position, SearchInteractionType type, Guid? eventId)
    {
        try
        {
            if (!requestFilter.ShouldCapture())
            {
                return false;
            }

            queue.Enqueue(new SearchInteractionEvent(
                searchId,
                sequence,
                itemGuid,
                position,
                type,
                eventId ?? Guid.NewGuid(),
                sessionCookie.GetSessionId(),
                timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (Exception exception)
        {
            if (logThrottle.ShouldLog())
            {
                logger.LogWarning(exception, "Lucene Suite interaction capture failed; the event was discarded (further failures suppressed for a minute).");
            }
        }

        return true;
    }
}
