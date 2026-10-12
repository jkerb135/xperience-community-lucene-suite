namespace XperienceCommunity.Lucene.Suite.Suggestions;

/// <summary>
/// As-you-type query suggestions for a Kentico Lucene index, built from the queries visitors already search successfully
/// (surfaced queries of the last <c>LuceneSuite:Suggestions:WindowDays</c> days, minus mostly-empty ones and the index's blocklist).
/// Registered by AddLuceneSuite. Records no analytics and runs no Lucene search.
/// </summary>
/// <remarks>The returned strings are past visitor input: render them as text (for example with <c>textContent</c>), never as HTML.</remarks>
public interface ISuiteSuggestions
{
    /// <summary>Longest accepted input in characters; longer input gets no suggestions.</summary>
    public const int MaxInputLength = 100;

    /// <summary>Largest accepted <c>maxResults</c>.</summary>
    public const int MaxResultsLimit = 20;

    /// <summary>Returns suggestions for <paramref name="input"/>: queries starting with it first, then queries with a later word starting with it, each by searches.</summary>
    /// <param name="indexName">Kentico Lucene index name. An unknown index yields an empty list.</param>
    /// <param name="input">What the visitor typed. Null, longer than <see cref="MaxInputLength"/> or shorter than <c>LuceneSuite:Suggestions:MinInputLength</c> (after normalization) yields an empty list.</param>
    /// <param name="maxResults">Most suggestions to return (1–<see cref="MaxResultsLimit"/>); null uses <c>LuceneSuite:Suggestions:MaxResults</c>.</param>
    /// <param name="cancellationToken">Cancels loading the candidates.</param>
    /// <returns>Distinct query texts, best first.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxResults"/> is outside 1–<see cref="MaxResultsLimit"/>.</exception>
    Task<IReadOnlyList<string>> SuggestAsync(string indexName, string? input, int? maxResults = null, CancellationToken cancellationToken = default);
}
