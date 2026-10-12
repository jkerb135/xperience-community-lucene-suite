namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>
/// Logs identity-free clicks and conversions on search results, tied to the search that listed them.
/// </summary>
/// <remarks>
/// Calls return immediately: events are buffered and written in the background, and storage failures are logged, never thrown.
/// Bots, prefetch/prerender requests and disabled analytics record nothing. Whether the search and position really matched a
/// returned page is checked later, when events are rolled up. See docs/analytics-client-contract.md.
/// </remarks>
public interface ISearchInteractionLogger
{
    /// <summary>Logs a click on a search result.</summary>
    /// <param name="click">The clicked result and the search that listed it.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="click"/> is null.</exception>
    /// <exception cref="ArgumentException">When <see cref="SearchClick.SearchId"/>, <see cref="SearchClick.ItemGuid"/> or <see cref="SearchClick.EventId"/> is an empty GUID.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <see cref="SearchClick.Sequence"/> is below 1 or <see cref="SearchClick.Position"/> is outside 1–1000.</exception>
    void LogClick(SearchClick click);

    /// <summary>Logs a conversion (goal completed) attributed to a search result.</summary>
    /// <param name="searchId">Search id of the result list the visitor came from.</param>
    /// <param name="sequence">1-based sequence of the search request whose response listed the result.</param>
    /// <param name="itemGuid">Item GUID of the converted hit.</param>
    /// <param name="position">1-based absolute position of the hit, when known.</param>
    /// <param name="eventId">Stable id of this event so a retried call is counted once; minted when null.</param>
    /// <exception cref="ArgumentException">When <paramref name="searchId"/>, <paramref name="itemGuid"/> or <paramref name="eventId"/> is an empty GUID.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="sequence"/> is below 1 or <paramref name="position"/> is outside 1–1000.</exception>
    void LogConversion(Guid searchId, int sequence, Guid itemGuid, int? position = null, Guid? eventId = null);
}
