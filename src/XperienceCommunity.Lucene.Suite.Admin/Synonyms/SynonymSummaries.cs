using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Admin.Synonyms;

/// <summary>One-line summaries of stored synonyms for the synonyms listing.</summary>
internal static class SynonymSummaries
{
    /// <summary>"a ⇔ b ⇔ c" for a two-way group, "input → a, b" for a one-way synonym.</summary>
    public static string Terms(string? kind, string? input, string? terms)
    {
        var result = SynonymSerializer.Parse(0, "listing", kind, input, terms);
        return result.Definition switch
        {
            { Kind: SynonymKind.OneWay } synonym => $"{synonym.Input} → {string.Join(", ", synonym.Terms)}",
            { } synonym => string.Join(" ⇔ ", synonym.Terms),
            null => RuleSummaries.Invalid,
        };
    }

    public static string Kind(string? kind) => kind switch
    {
        SynonymSerializer.TwoWay => "Two-way",
        SynonymSerializer.OneWay => "One-way",
        _ => RuleSummaries.Invalid,
    };
}
