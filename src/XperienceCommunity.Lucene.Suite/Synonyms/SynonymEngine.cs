using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Synonyms;

/// <summary>A synonym used to build a query variant.</summary>
/// <param name="SynonymId">Synonym row that supplied it.</param>
/// <param name="Term">The query words it matched, as typed.</param>
/// <param name="Replacement">What they were replaced with.</param>
internal sealed record SynonymSubstitution(int SynonymId, string Term, string Replacement);

/// <summary>Query texts to search besides the original, and the substitutions that produced them.</summary>
/// <param name="Variants">Variant query texts, in order.</param>
/// <param name="Applied">The substitution behind each variant (same order).</param>
internal sealed record SynonymExpansion(IReadOnlyList<string> Variants, IReadOnlyList<SynonymSubstitution> Applied)
{
    public static SynonymExpansion None { get; } = new([], []);
}

/// <summary>The synonyms of one index as a match key → alternatives lookup, built once per cache entry.</summary>
internal sealed class SynonymSet
{
    private readonly Dictionary<string, List<(int Id, string Alternative)>> alternatives = new(StringComparer.Ordinal);

    public static SynonymSet Empty { get; } = new([]);

    public SynonymSet(IEnumerable<SynonymDefinition> synonyms)
    {
        foreach (var synonym in synonyms.OrderBy(synonym => synonym.Id))
        {
            if (synonym.Kind == SynonymKind.OneWay)
            {
                foreach (string term in synonym.Terms)
                {
                    Add(synonym.Input!, synonym.Id, term);
                }

                continue;
            }

            for (int from = 0; from < synonym.Terms.Count; from++)
            {
                for (int to = 0; to < synonym.Terms.Count; to++)
                {
                    if (to != from)
                    {
                        Add(synonym.Terms[from], synonym.Id, synonym.Terms[to]);
                    }
                }
            }
        }
    }

    public int Count => alternatives.Count;

    public bool TryGet(string key, out List<(int Id, string Alternative)> found) => alternatives.TryGetValue(key, out found!);

    private void Add(string term, int id, string alternative)
    {
        string key = SynonymEngine.MatchKey(term);
        if (!alternatives.TryGetValue(key, out var list))
        {
            alternatives[key] = list = [];
        }

        list.Add((id, alternative));
    }
}

/// <summary>Expands query text with synonyms: contiguous word runs equal to a synonym input, one substitution per variant, no cascading.</summary>
internal static class SynonymEngine
{
    /// <summary>
    /// The key both synonym terms and query word runs are compared by: normalized like rule patterns, words split on whitespace,
    /// leading/trailing non-letter/digit characters trimmed from each word, empty words dropped, joined with one space.
    /// </summary>
    public static string MatchKey(string? text) =>
        string.Join(' ', RuleSerializer.NormalizeText(text).Split(' ').Select(TrimWord).Where(word => word.Length > 0));

    public static SynonymExpansion Expand(SynonymSet set, string? queryText, int maxExpansions)
    {
        if (set.Count == 0 || string.IsNullOrWhiteSpace(queryText) || maxExpansions <= 0)
        {
            return SynonymExpansion.None;
        }

        string[] words = queryText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<string>(StringComparer.Ordinal) { MatchKey(queryText) };
        var variants = new List<string>();
        var applied = new List<SynonymSubstitution>();
        for (int start = 0; start < words.Length; start++)
        {
            for (int length = 1; length <= SynonymLimits.TermWords && start + length <= words.Length; length++)
            {
                string term = string.Join(' ', words, start, length);
                if (!set.TryGet(MatchKey(term), out var found))
                {
                    continue;
                }

                foreach (var (id, alternative) in found)
                {
                    string variant = string.Join(' ', words.Take(start).Append(alternative).Concat(words.Skip(start + length)));
                    if (!seen.Add(MatchKey(variant)))
                    {
                        continue;
                    }

                    variants.Add(variant);
                    applied.Add(new SynonymSubstitution(id, term, alternative));
                    if (variants.Count == maxExpansions)
                    {
                        return new SynonymExpansion(variants, applied);
                    }
                }
            }
        }

        return variants.Count == 0 ? SynonymExpansion.None : new SynonymExpansion(variants, applied);
    }

    private static string TrimWord(string word)
    {
        int start = 0;
        int end = word.Length;
        while (start < end && !char.IsLetterOrDigit(word[start]))
        {
            start++;
        }

        while (end > start && !char.IsLetterOrDigit(word[end - 1]))
        {
            end--;
        }

        return word[start..end];
    }
}
