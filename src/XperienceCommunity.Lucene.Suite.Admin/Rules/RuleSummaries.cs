using System.Text.Json;
using System.Text.Json.Nodes;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>One-line summaries of stored rule JSON for the rules listing.</summary>
internal static class RuleSummaries
{
    internal const string Invalid = "Invalid — open to fix";

    public static string Conditions(string? json)
    {
        if (Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json) is not JsonArray conditions)
        {
            return Invalid;
        }

        if (conditions.Count == 0)
        {
            return "Every search";
        }

        string first = Describe(conditions[0] as JsonObject);
        return conditions.Count == 1 ? first : $"{first} +{conditions.Count - 1}";
    }

    public static string Actions(string? json)
    {
        if (Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) is not JsonObject actions)
        {
            return Invalid;
        }

        var parts = new List<string>();
        void Count(string property, string singular, string plural)
        {
            if (actions[property] is JsonArray { Count: > 0 } items)
            {
                parts.Add($"{items.Count} {(items.Count == 1 ? singular : plural)}");
            }
        }

        Count("pins", "pin", "pins");
        Count("hides", "hide", "hides");
        Count("boosts", "boost", "boosts");
        if (actions["userData"] is JsonObject)
        {
            parts.Add("data");
        }

        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    /// <summary>The action a fired-rule chip shows: "pin", "boost", "hide" or "data", first that applies; null for no actions or invalid JSON, never throws.</summary>
    public static string? PrimaryAction(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || Parse(json) is not JsonObject actions)
        {
            return null;
        }

        return actions["pins"] is JsonArray { Count: > 0 } ? "pin"
            : actions["boosts"] is JsonArray { Count: > 0 } ? "boost"
            : actions["hides"] is JsonArray { Count: > 0 } ? "hide"
            : actions["userData"] is JsonObject ? "data"
            : null;
    }

    private static string Describe(JsonObject? condition)
    {
        if (condition is null)
        {
            return Invalid;
        }

        string? anchoring = condition["anchoring"]?.GetValue<string>();
        string? pattern = condition["pattern"]?.GetValue<string>();
        string text = anchoring is null || pattern is null
            ? "Any query"
            : anchoring == "is" && pattern.Trim().Length == 0
                ? "Empty query"
                : $"{Words(anchoring)} '{pattern}'";

        int facets = (condition["facets"] as JsonArray)?.Count ?? 0;
        int contexts = (condition["contexts"] as JsonArray)?.Count ?? 0;
        if (facets > 0)
        {
            text += $", {facets} facet{(facets == 1 ? string.Empty : "s")}";
        }

        if (contexts > 0)
        {
            text += $", {contexts} context{(contexts == 1 ? string.Empty : "s")}";
        }

        return text;
    }

    private static string Words(string anchoring) => anchoring switch
    {
        "is" => "is",
        "startsWith" => "starts with",
        "endsWith" => "ends with",
        "contains" => "contains",
        _ => anchoring,
    };

    private static JsonNode? Parse(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);

            // A JsonObject builds its dictionary on first access and throws there on duplicate property names.
            _ = (node as JsonObject)?.Count;
            return node;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return null;
        }
    }
}
