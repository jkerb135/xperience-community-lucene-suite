using System.Text.Json;

namespace XperienceCommunity.Lucene.Suite.Rules;

/// <summary>A validated search rule: when any condition matches a search on <paramref name="IndexName"/>, its actions apply.</summary>
/// <param name="Id">Rule id; tie-break after <paramref name="Priority"/>.</param>
/// <param name="IndexName">Kentico Lucene index the rule targets (case-insensitive).</param>
/// <param name="Priority">1 runs first; earlier rules win pin conflicts.</param>
/// <param name="Enabled">Whether the rule can fire.</param>
/// <param name="ValidFromUtc">Inclusive start of the validity window; null is unbounded.</param>
/// <param name="ValidUntilUtc">Exclusive end of the validity window; null is unbounded.</param>
/// <param name="Conditions">OR'd conditions; empty fires on every search of the index.</param>
/// <param name="Actions">What the rule does when it fires.</param>
internal sealed record RuleDefinition(
    int Id,
    string IndexName,
    int Priority,
    bool Enabled,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    IReadOnlyList<RuleCondition> Conditions,
    RuleActions Actions);

/// <summary>Stored rule fields outside the conditions and actions JSON.</summary>
/// <param name="Id">Rule id.</param>
/// <param name="IndexName">Targeted index.</param>
/// <param name="Priority">1 runs first.</param>
/// <param name="Enabled">Whether the rule can fire.</param>
/// <param name="ValidFromUtc">Inclusive start; Unspecified kind is read as UTC.</param>
/// <param name="ValidUntilUtc">Exclusive end; Unspecified kind is read as UTC.</param>
internal sealed record RuleMetadata(int Id, string IndexName, int Priority, bool Enabled, DateTime? ValidFromUtc, DateTime? ValidUntilUtc);

/// <summary>One condition; every present part must match.</summary>
/// <param name="Anchoring">How <paramref name="Pattern"/> is compared; null together with a null pattern means any query.</param>
/// <param name="Pattern">Normalized pattern; null means any query, "" with <see cref="RuleAnchoring.Is"/> means the empty (browse) query.</param>
/// <param name="Facets">Facet pairs that must all be applied.</param>
/// <param name="Contexts">Visitor contexts of which at least one must be present; empty means any visitor.</param>
internal sealed record RuleCondition(RuleAnchoring? Anchoring, string? Pattern, IReadOnlyList<RuleFacet> Facets, IReadOnlyList<string> Contexts);

/// <summary>An applied facet value.</summary>
/// <param name="Dimension">Facet dimension (ordinal).</param>
/// <param name="Value">Facet value (ordinal).</param>
internal sealed record RuleFacet(string Dimension, string Value);

/// <summary>How a pattern is compared with the query, on whole words.</summary>
internal enum RuleAnchoring
{
    /// <summary>Same word sequence.</summary>
    Is,

    /// <summary>Query starts with the pattern's words.</summary>
    StartsWith,

    /// <summary>Query ends with the pattern's words.</summary>
    EndsWith,

    /// <summary>Pattern's words appear contiguously in the query.</summary>
    Contains,
}

/// <summary>What a fired rule does.</summary>
/// <param name="Pins">Items placed at fixed 1-based positions.</param>
/// <param name="Hides">Items removed from results.</param>
/// <param name="Boosts">Field/value score adjustments; negative buries.</param>
/// <param name="UserData">Custom JSON object returned with the results.</param>
/// <param name="PinsRespectFilters">Whether pinned items must still match the applied facets.</param>
internal sealed record RuleActions(
    IReadOnlyList<RulePin> Pins,
    IReadOnlyList<Guid> Hides,
    IReadOnlyList<RuleBoost> Boosts,
    JsonElement? UserData,
    bool PinsRespectFilters = true);

/// <summary>An item pinned at a 1-based position.</summary>
/// <param name="ItemGuid">Pinned item.</param>
/// <param name="Position">1-based position.</param>
internal sealed record RulePin(Guid ItemGuid, int Position);

/// <summary>A score adjustment for documents whose field has the value.</summary>
/// <param name="Field">Index field name.</param>
/// <param name="Value">Field value.</param>
/// <param name="Strength">−100..100, not 0; negative buries.</param>
internal sealed record RuleBoost(string Field, string Value, int Strength);

/// <summary>Size limits enforced by <see cref="RuleSerializer"/>.</summary>
internal static class RuleLimits
{
    /// <summary>Conditions per rule.</summary>
    public const int Conditions = 25;

    /// <summary>Characters per normalized pattern.</summary>
    public const int PatternLength = 200;

    /// <summary>Facet pairs per condition.</summary>
    public const int Facets = 10;

    /// <summary>Contexts per condition.</summary>
    public const int Contexts = 20;

    /// <summary>Pins per rule.</summary>
    public const int Pins = 300;

    /// <summary>Highest pin position.</summary>
    public const int MaxPosition = 300;

    /// <summary>Hides per rule.</summary>
    public const int Hides = 50;

    /// <summary>Boosts per rule.</summary>
    public const int Boosts = 50;

    /// <summary>Highest absolute boost strength.</summary>
    public const int MaxStrength = 100;

    /// <summary>Characters per boost field name.</summary>
    public const int BoostFieldLength = 100;

    /// <summary>Characters per boost value, facet dimension and facet value.</summary>
    public const int ValueLength = 200;

    /// <summary>Characters per index name.</summary>
    public const int IndexNameLength = 200;

    /// <summary>UTF-8 bytes of the compact userData JSON.</summary>
    public const int UserDataBytes = 1024;
}
