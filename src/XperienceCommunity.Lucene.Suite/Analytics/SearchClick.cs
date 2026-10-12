namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>A click on a search result, logged with <see cref="ISearchInteractionLogger.LogClick(SearchClick)"/>.</summary>
/// <param name="SearchId">Search id of the result list (the response's <c>SearchId</c>).</param>
/// <param name="Sequence">1-based sequence of the search request whose response listed the result.</param>
/// <param name="ItemGuid">Item GUID of the clicked hit.</param>
/// <param name="Position">1-based absolute position of the hit: (page − 1) × pageSize + index on the page.</param>
/// <param name="EventId">Stable id of this event so a retried call is counted once; minted when null.</param>
/// <param name="QueryText">Query of the result list, as the client sent it. Untrusted: scrubbed and used only for the contact's
/// "Search result clicked" activity, never stored in analytics.</param>
/// <param name="ResultTitle">Title of the clicked result, as the client sent it. Untrusted: used only as display text of the
/// contact's "Search result clicked" activity, never stored in analytics.</param>
public sealed record SearchClick(
    Guid SearchId,
    int Sequence,
    Guid ItemGuid,
    int Position,
    Guid? EventId = null,
    string? QueryText = null,
    string? ResultTitle = null);
