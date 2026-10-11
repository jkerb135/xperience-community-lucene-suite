using System.Text.Json;
using System.Text.Json.Nodes;

using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>The part of a rule one editor field owns.</summary>
public enum RuleJsonSection
{
    /// <summary>The conditions array.</summary>
    Conditions,

    /// <summary>The boosts array inside actions.</summary>
    Boosts,

    /// <summary>The userData object inside actions.</summary>
    UserData,

    /// <summary>The pins array inside actions.</summary>
    Pins,

    /// <summary>The hides array inside actions.</summary>
    Hides,
}

/// <summary>Validates one field's JSON with <see cref="RuleSerializer"/>, so the editor and the engine share one set of rules.</summary>
internal static class RuleSectionValidator
{
    private static readonly RuleMetadata Placeholder = new(0, "validation", 1, true, null, null);

    /// <summary>Errors under the section's own path; paths are as <see cref="RuleSerializer"/> reports them.</summary>
    public static IReadOnlyList<RuleError> Validate(RuleJsonSection section, string? json)
    {
        switch (section)
        {
            case RuleJsonSection.Conditions:
                // A placeholder action keeps the "no actions" rule from firing for a conditions-only check.
                return Only(RuleSerializer.Parse(Placeholder, json, """{"userData":{}}"""), "conditions");

            case RuleJsonSection.Boosts:
                return ActionArray(json, "boosts", "Boosts");

            case RuleJsonSection.Pins:
                return ActionArray(json, "pins", "Pins");

            case RuleJsonSection.Hides:
                return ActionArray(json, "hides", "Hidden items");

            case RuleJsonSection.UserData:
                if (string.IsNullOrWhiteSpace(json))
                {
                    return [];
                }

                if (!TryParse(json, out var data))
                {
                    return [new RuleError("actions.userData", "Custom data is not valid JSON.")];
                }

                var withData = new JsonObject { ["userData"] = data };
                return Only(RuleSerializer.Parse(Placeholder, "[]", withData.ToJsonString()), "actions.userData");

            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    private static IReadOnlyList<RuleError> ActionArray(string? json, string name, string label)
    {
        if (!TryParse(string.IsNullOrWhiteSpace(json) ? "[]" : json, out var array))
        {
            return [new RuleError("actions." + name, $"{label} are not valid JSON.")];
        }

        // Placeholder custom data keeps the "no actions" rule from firing for an empty list.
        var actions = new JsonObject { [name] = array, ["userData"] = new JsonObject() };
        return Only(RuleSerializer.Parse(Placeholder, "[]", actions.ToJsonString()), "actions." + name);
    }

    private static IReadOnlyList<RuleError> Only(RuleParseResult result, string root) =>
        [.. result.Errors.Where(error => error.Path == root || error.Path.StartsWith(root + "[", StringComparison.Ordinal) || error.Path.StartsWith(root + ".", StringComparison.Ordinal))];

    private static bool TryParse(string json, out JsonNode? node)
    {
        try
        {
            node = JsonNode.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            node = null;
            return false;
        }
    }
}
