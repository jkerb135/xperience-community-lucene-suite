using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Synonyms;

/// <summary>How a synonym maps terms.</summary>
internal enum SynonymKind
{
    /// <summary>Every term of the group finds every other term.</summary>
    TwoWay,

    /// <summary>The input finds the terms; the terms never find the input.</summary>
    OneWay,
}

/// <summary>A validated, normalized synonym.</summary>
/// <param name="Id">Row identifier.</param>
/// <param name="IndexName">Targeted Kentico Lucene index.</param>
/// <param name="Kind">Two-way group or one-way synonym.</param>
/// <param name="Input">The expanded term of a one-way synonym; null for a two-way group.</param>
/// <param name="Terms">The group (two-way) or what the input expands to (one-way), normalized.</param>
internal sealed record SynonymDefinition(int Id, string IndexName, SynonymKind Kind, string? Input, IReadOnlyList<string> Terms);

/// <summary>Either a definition (no errors) or every error found (no definition).</summary>
/// <param name="Definition">The validated synonym, or null when invalid.</param>
/// <param name="Errors">All errors; empty when valid. Paths: <c>indexName</c>, <c>kind</c>, <c>input</c>, <c>terms</c>, <c>terms[2]</c>.</param>
internal sealed record SynonymParseResult(SynonymDefinition? Definition, IReadOnlyList<RuleError> Errors);

/// <summary>Size limits enforced by <see cref="SynonymSerializer"/>.</summary>
internal static class SynonymLimits
{
    /// <summary>Terms per synonym.</summary>
    public const int Terms = 20;

    /// <summary>Characters per normalized term or input.</summary>
    public const int TermLength = 100;

    /// <summary>Words per term or input.</summary>
    public const int TermWords = 5;
}
