using System.Text.Json;

using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Synonyms;

/// <summary>Validates and normalizes stored or edited synonyms.</summary>
public static class SynonymSerializer
{
    /// <summary>Stored value of <see cref="SynonymKind.TwoWay"/>.</summary>
    public const string TwoWay = "twoWay";

    /// <summary>Stored value of <see cref="SynonymKind.OneWay"/>.</summary>
    public const string OneWay = "oneWay";

    /// <summary>Parses and validates a synonym. Terms are a JSON array of strings; null or blank is an empty array.</summary>
    public static SynonymParseResult Parse(int id, string? indexName, string? kind, string? input, string? termsJson)
    {
        var errors = new List<RuleError>();
        if (string.IsNullOrWhiteSpace(indexName) || indexName.Length > RuleLimits.IndexNameLength)
        {
            errors.Add(new RuleError("indexName", $"Required, at most {RuleLimits.IndexNameLength} characters."));
        }

        SynonymKind? parsedKind = kind switch
        {
            TwoWay => SynonymKind.TwoWay,
            OneWay => SynonymKind.OneWay,
            _ => null,
        };
        if (parsedKind is null)
        {
            errors.Add(new RuleError("kind", $"Must be \"{TwoWay}\" or \"{OneWay}\"."));
        }

        string? normalizedInput = null;
        if (parsedKind == SynonymKind.OneWay)
        {
            normalizedInput = CheckTerm(input, "input", errors);
        }
        else if (parsedKind == SynonymKind.TwoWay && !string.IsNullOrWhiteSpace(input))
        {
            errors.Add(new RuleError("input", "Must be empty for a two-way synonym."));
        }

        var terms = ParseTerms(termsJson, errors);
        var normalizedTerms = new List<string>();
        if (terms is not null)
        {
            int minimum = parsedKind == SynonymKind.TwoWay ? 2 : 1;
            if (terms.Count < minimum || terms.Count > SynonymLimits.Terms)
            {
                errors.Add(new RuleError("terms", $"Must have {minimum}–{SynonymLimits.Terms} terms."));
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (normalizedInput is not null)
            {
                keys.Add(SynonymEngine.MatchKey(normalizedInput));
            }

            for (int i = 0; i < terms.Count; i++)
            {
                if (CheckTerm(terms[i], $"terms[{i}]", errors) is not { } term)
                {
                    continue;
                }

                if (!keys.Add(SynonymEngine.MatchKey(term)))
                {
                    errors.Add(new RuleError($"terms[{i}]", normalizedInput is not null && SynonymEngine.MatchKey(term) == SynonymEngine.MatchKey(normalizedInput)
                        ? "Must differ from the input."
                        : "Duplicate term."));
                }

                normalizedTerms.Add(term);
            }
        }

        return errors.Count > 0 || parsedKind is not { } validKind
            ? new SynonymParseResult(null, errors)
            : new SynonymParseResult(new SynonymDefinition(id, indexName!, validKind, normalizedInput, normalizedTerms), []);
    }

    /// <summary>Serializes normalized terms to the stored JSON shape.</summary>
    public static string SerializeTerms(IReadOnlyList<string> terms) => JsonSerializer.Serialize(terms);

    private static List<string?>? ParseTerms(string? json, List<RuleError> errors)
    {
        try
        {
            var terms = JsonSerializer.Deserialize<List<string?>>(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (terms is null)
            {
                errors.Add(new RuleError("terms", "Must be an array."));
            }

            return terms;
        }
        catch (JsonException exception)
        {
            errors.Add(new RuleError("terms", exception.Message));
            return null;
        }
    }

    private static string? CheckTerm(string? value, string path, List<RuleError> errors)
    {
        string term = RuleSerializer.NormalizeText(value);
        string key = SynonymEngine.MatchKey(term);
        int words = key.Length == 0 ? 0 : key.Count(c => c == ' ') + 1;
        if (term.Length is 0 or > SynonymLimits.TermLength || words > SynonymLimits.TermWords)
        {
            errors.Add(new RuleError(path, $"Required, at most {SynonymLimits.TermLength} characters and {SynonymLimits.TermWords} words."));
            return null;
        }

        if (!term.Any(char.IsLetterOrDigit))
        {
            errors.Add(new RuleError(path, "Must contain a letter or digit."));
            return null;
        }

        return term;
    }
}
