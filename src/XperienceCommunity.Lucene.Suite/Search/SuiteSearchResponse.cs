namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>The result of a suite search.</summary>
/// <param name="Hits">Hits on the requested page, best first.</param>
/// <param name="TotalHits">Number of documents matching the query and facet filters.</param>
/// <param name="Facets">Facet counts; empty when the index has no facets.</param>
/// <param name="Elapsed">Wall-clock time of the search.</param>
public sealed record SuiteSearchResponse(
    IReadOnlyList<SuiteSearchHit> Hits,
    int TotalHits,
    IReadOnlyList<SuiteFacet> Facets,
    TimeSpan Elapsed);

/// <summary>One search hit.</summary>
/// <param name="ItemGuid">The document's ItemGuid, or null when missing or unparsable.</param>
/// <param name="Score">Relevance score.</param>
/// <param name="Fields">Stored fields; multi-valued fields keep every value.</param>
public sealed record SuiteSearchHit(
    Guid? ItemGuid,
    float Score,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Fields);

/// <summary>Counts for one facet dimension.</summary>
/// <param name="Dimension">Facet dimension name.</param>
/// <param name="Values">Top values with their counts.</param>
public sealed record SuiteFacet(string Dimension, IReadOnlyList<SuiteFacetValue> Values);

/// <summary>A facet value and its document count.</summary>
/// <param name="Label">Facet value.</param>
/// <param name="Count">Number of matching documents.</param>
public sealed record SuiteFacetValue(string Label, int Count);
