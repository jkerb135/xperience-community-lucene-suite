using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Suggestions;

/// <summary>A query text's totals over the suggestion window; <see cref="QueryText"/> is null for "(other)" and empty for browse.</summary>
internal sealed record SuggestionRow(string? QueryText, long Searches, long ZeroResultSearches);

/// <summary>A suggestible query: its most-searched text, the match key it is compared by, and the searches of every text sharing that key.</summary>
internal sealed record SuggestionCandidate(string Text, string Key, long Searches);

/// <summary>A blocklist entry by match key; <see cref="Word"/> blocks every candidate containing the key as whole words, otherwise only an equal key.</summary>
internal sealed record SuggestionBlock(string Key, bool Word);

/// <summary>Filters suggestion candidates and matches visitor input against them; pure, so the cached candidates are shared by all requests.</summary>
internal static class SuggestionEngine
{
    /// <summary>
    /// Drops browse, "(other)", placeholder texts, texts below <see cref="SuiteSuggestionOptions.MinSearches"/> and texts at or above
    /// <see cref="SuiteSuggestionOptions.MaxNoResultRate"/>; merges texts with one match key under the most-searched text and keeps the
    /// <see cref="SuiteSuggestionOptions.MaxCandidates"/> most searched.
    /// </summary>
    public static IReadOnlyList<SuggestionCandidate> Candidates(IEnumerable<SuggestionRow> rows, SuiteSuggestionOptions options) =>
        rows
            .Where(row => !string.IsNullOrEmpty(row.QueryText)
                && !row.QueryText.Contains(QueryScrubber.EmailPlaceholder, StringComparison.OrdinalIgnoreCase)
                && !row.QueryText.Contains(QueryScrubber.NumberPlaceholder, StringComparison.OrdinalIgnoreCase)
                && row.Searches >= options.MinSearches
                && row.ZeroResultSearches < options.MaxNoResultRate * row.Searches)
            .Select(row => (Text: row.QueryText!, Key: SynonymEngine.MatchKey(row.QueryText), row.Searches))
            .Where(row => row.Key.Length > 0)
            .GroupBy(row => row.Key, StringComparer.Ordinal)
            .Select(group => new SuggestionCandidate(
                group.OrderByDescending(row => row.Searches).ThenBy(row => row.Text, StringComparer.Ordinal).First().Text,
                group.Key,
                group.Sum(row => row.Searches)))
            .OrderByDescending(candidate => candidate.Searches)
            .ThenBy(candidate => candidate.Text, StringComparer.Ordinal)
            .Take(options.MaxCandidates)
            .ToList();

    /// <summary>
    /// Candidates whose key starts with the input's key, then those where it starts at a later word; each group by searches, then text.
    /// Blocked candidates are skipped; an input whose key is shorter than <paramref name="minInputLength"/> matches nothing.
    /// </summary>
    public static IReadOnlyList<string> Match(
        IReadOnlyList<SuggestionCandidate> candidates, IReadOnlyCollection<SuggestionBlock> blocked, string? input, int minInputLength, int max)
    {
        string key = SynonymEngine.MatchKey(input);
        if (key.Length < minInputLength || max < 1)
        {
            return [];
        }

        // Keys are words joined by single spaces, so " " + key found inside " " + candidate means the input's words match whole,
        // in order, with only the last one as a prefix.
        string inner = " " + key;
        var prefix = new List<SuggestionCandidate>();
        var later = new List<SuggestionCandidate>();
        foreach (var candidate in candidates)
        {
            var group = candidate.Key.StartsWith(key, StringComparison.Ordinal) ? prefix
                : candidate.Key.Contains(inner, StringComparison.Ordinal) ? later
                : null;
            if (group is not null && !IsBlocked(candidate.Key, blocked))
            {
                group.Add(candidate);
            }
        }

        return prefix.OrderByDescending(c => c.Searches).ThenBy(c => c.Text, StringComparer.Ordinal)
            .Concat(later.OrderByDescending(c => c.Searches).ThenBy(c => c.Text, StringComparer.Ordinal))
            .Select(c => c.Text)
            .Take(max)
            .ToList();
    }

    private static bool IsBlocked(string key, IReadOnlyCollection<SuggestionBlock> blocked)
    {
        string padded = " " + key + " ";
        foreach (var block in blocked)
        {
            if (block.Word ? padded.Contains(" " + block.Key + " ", StringComparison.Ordinal) : key == block.Key)
            {
                return true;
            }
        }

        return false;
    }
}
