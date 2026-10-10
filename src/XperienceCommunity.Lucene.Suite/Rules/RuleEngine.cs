using System.Text.Json;

namespace XperienceCommunity.Lucene.Suite.Rules;

/// <summary>What the engine matches rules against.</summary>
/// <param name="IndexName">Searched index.</param>
/// <param name="Query">Raw query text; normalized by the engine.</param>
/// <param name="Facets">Applied facets: dimension → values.</param>
/// <param name="Contexts">Visitor contexts (for example contact groups).</param>
/// <param name="NowUtc">Evaluation time for validity windows.</param>
internal sealed record RuleInput(
    string IndexName,
    string? Query,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Facets,
    IReadOnlySet<string> Contexts,
    DateTime NowUtc);

/// <summary>A pin after conflict resolution.</summary>
internal sealed record ResolvedPin(Guid ItemGuid, int Position, bool RespectFilters, int RuleId);

/// <summary>User data of one fired rule.</summary>
internal sealed record RuleUserData(int RuleId, JsonElement Data);

/// <summary>The fired rules and their single resolved outcome.</summary>
/// <param name="FiredRuleIds">Fired rules in run order.</param>
/// <param name="Pins">Placed pins ordered by position.</param>
/// <param name="Hides">Hidden items ordered by Guid.</param>
/// <param name="Boosts">Summed, clamped, non-zero boosts ordered by field then value.</param>
/// <param name="UserData">User data in run order.</param>
internal sealed record RuleOutcome(
    IReadOnlyList<int> FiredRuleIds,
    IReadOnlyList<ResolvedPin> Pins,
    IReadOnlyList<Guid> Hides,
    IReadOnlyList<RuleBoost> Boosts,
    IReadOnlyList<RuleUserData> UserData);

/// <summary>Pure, deterministic rule matching and conflict resolution: no I/O, no clock.</summary>
internal static class RuleEngine
{
    /// <summary>Returns the rules that fire for <paramref name="input"/> and their resolved outcome.</summary>
    public static RuleOutcome Evaluate(IEnumerable<RuleDefinition> rules, RuleInput input)
    {
        string query = RuleSerializer.NormalizeText(input.Query);

        var fired = rules
            .Where(rule => Fires(rule, input, query))
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToList();

        var hides = new SortedSet<Guid>(fired.SelectMany(rule => rule.Actions.Hides));
        return new RuleOutcome(
            fired.ConvertAll(rule => rule.Id),
            ResolvePins(fired, hides),
            [.. hides],
            ResolveBoosts(fired),
            [.. fired.Where(rule => rule.Actions.UserData is not null).Select(rule => new RuleUserData(rule.Id, rule.Actions.UserData!.Value))]);
    }

    private static bool Fires(RuleDefinition rule, RuleInput input, string query) =>
        rule.Enabled
        && string.Equals(rule.IndexName, input.IndexName, StringComparison.OrdinalIgnoreCase)
        && (rule.ValidFromUtc is null || input.NowUtc >= rule.ValidFromUtc)
        && (rule.ValidUntilUtc is null || input.NowUtc < rule.ValidUntilUtc)
        && (rule.Conditions.Count == 0 || rule.Conditions.Any(condition => Matches(condition, input, query)));

    private static bool Matches(RuleCondition condition, RuleInput input, string query) =>
        MatchesPattern(condition, query)
        && condition.Facets.All(facet => input.Facets.TryGetValue(facet.Dimension, out var values) && values.Contains(facet.Value, StringComparer.Ordinal))
        && (condition.Contexts.Count == 0 || condition.Contexts.Any(input.Contexts.Contains));

    // Query and pattern are both normalized (single spaces, no edges), so whole-word matching is a substring
    // match bounded by a space or the string edge on both sides.
    private static bool MatchesPattern(RuleCondition condition, string query)
    {
        if (condition.Pattern is not { } pattern || condition.Anchoring is null)
        {
            return true;
        }

        var q = query.AsSpan();
        return condition.Anchoring switch
        {
            RuleAnchoring.Is => q.SequenceEqual(pattern),
            RuleAnchoring.StartsWith => q.StartsWith(pattern, StringComparison.Ordinal) && IsWord(q, 0, pattern.Length),
            RuleAnchoring.EndsWith => q.EndsWith(pattern, StringComparison.Ordinal) && IsWord(q, q.Length - pattern.Length, pattern.Length),
            RuleAnchoring.Contains => ContainsWords(q, pattern),
            _ => false,
        };
    }

    private static bool ContainsWords(ReadOnlySpan<char> query, string pattern)
    {
        for (int from = 0; from <= query.Length - pattern.Length;)
        {
            int index = query[from..].IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            if (IsWord(query, from + index, pattern.Length))
            {
                return true;
            }

            from += index + 1;
        }

        return false;
    }

    private static bool IsWord(ReadOnlySpan<char> query, int start, int length) =>
        length > 0
        && (start == 0 || query[start - 1] == ' ')
        && (start + length == query.Length || query[start + length] == ' ');

    private static List<ResolvedPin> ResolvePins(List<RuleDefinition> fired, SortedSet<Guid> hides)
    {
        var bySlot = new ResolvedPin?[RuleLimits.MaxPosition + 2];
        var pinned = new HashSet<Guid>();
        foreach (var rule in fired)
        {
            foreach (var pin in rule.Actions.Pins.OrderBy(pin => pin.Position))
            {
                // Hide beats pin; an earlier rule's placement beats a later rule's position.
                if (hides.Contains(pin.ItemGuid) || pinned.Contains(pin.ItemGuid))
                {
                    continue;
                }

                int slot = pin.Position;
                while (slot <= RuleLimits.MaxPosition && bySlot[slot] is not null)
                {
                    slot++;
                }

                if (slot > RuleLimits.MaxPosition)
                {
                    continue;
                }

                bySlot[slot] = new ResolvedPin(pin.ItemGuid, slot, rule.Actions.PinsRespectFilters, rule.Id);
                pinned.Add(pin.ItemGuid);
            }
        }

        return [.. bySlot.OfType<ResolvedPin>()];
    }

    private static List<RuleBoost> ResolveBoosts(List<RuleDefinition> fired)
    {
        var sums = new Dictionary<(string Field, string Value), int>();
        foreach (var boost in fired.SelectMany(rule => rule.Actions.Boosts))
        {
            sums[(boost.Field, boost.Value)] = sums.GetValueOrDefault((boost.Field, boost.Value)) + boost.Strength;
        }

        return
        [
            .. sums
                .Select(sum => new RuleBoost(sum.Key.Field, sum.Key.Value, Math.Clamp(sum.Value, -RuleLimits.MaxStrength, RuleLimits.MaxStrength)))
                .Where(boost => boost.Strength != 0)
                .OrderBy(boost => boost.Field, StringComparer.Ordinal)
                .ThenBy(boost => boost.Value, StringComparer.Ordinal),
        ];
    }
}
