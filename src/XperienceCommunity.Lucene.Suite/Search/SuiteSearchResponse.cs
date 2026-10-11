using System.Text.Json;

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
    TimeSpan Elapsed)
{
    /// <summary>Id of the logical search this request was recorded under (the request's, or a newly minted one).</summary>
    public Guid SearchId { get; init; }

    /// <summary>Ids of the rules that fired, in the order they ran.</summary>
    public IReadOnlyList<int> FiredRuleIds { get; init; } = [];

    /// <summary>User data of the fired rules, in the order they ran.</summary>
    public IReadOnlyList<SuiteRuleUserData> UserData { get; init; } = [];

    /// <summary>True when rules could not be loaded or applied, so these results are unruled.</summary>
    public bool RulesFailed { get; init; }

    // Null when unknown (a replaced inner service); 0 means the index is empty or rebuilding.
    internal int? IndexDocumentCount { get; init; }
}

/// <summary>Custom data returned by a fired rule.</summary>
/// <param name="RuleId">Rule that returned it.</param>
/// <param name="Data">A JSON object of at most 1 KB.</param>
public sealed record SuiteRuleUserData(int RuleId, JsonElement Data);

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
