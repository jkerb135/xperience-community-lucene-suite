using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace XperienceCommunity.Lucene.Suite.Rules;

/// <summary>A validation error at a JSON path such as <c>conditions[2].pattern</c>.</summary>
/// <param name="Path">Path of the offending value.</param>
/// <param name="Message">What is wrong.</param>
public sealed record RuleError(string Path, string Message);

/// <summary>Either a definition (no errors) or every error found (no definition).</summary>
/// <param name="Definition">The validated rule, or null when invalid.</param>
/// <param name="Errors">All errors; empty when valid.</param>
internal sealed record RuleParseResult(RuleDefinition? Definition, IReadOnlyList<RuleError> Errors);

/// <summary>Strict parsing, validation and serialization of rule conditions and actions JSON; never throws on bad input.</summary>
internal static partial class RuleSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>Parses and validates a stored or edited rule. Null or blank JSON is an empty array / object.</summary>
    public static RuleParseResult Parse(RuleMetadata metadata, string? conditionsJson, string? actionsJson)
    {
        var errors = new List<RuleError>();
        var conditions = Deserialize<List<RuleCondition?>>(conditionsJson, "[]", "conditions", errors);
        var actions = Deserialize<RuleActions>(actionsJson, "{}", "actions", errors);

        var (validFrom, validUntil) = ValidateMetadata(metadata, errors);
        var normalizedConditions = conditions is null ? [] : NormalizeConditions(conditions, errors);
        var normalizedActions = actions is null ? null : NormalizeActions(actions, errors);

        if (errors.Count > 0 || conditions is null || normalizedActions is null)
        {
            return new RuleParseResult(null, errors);
        }

        var definition = new RuleDefinition(
            metadata.Id,
            metadata.IndexName,
            metadata.Priority,
            metadata.Enabled,
            validFrom,
            validUntil,
            normalizedConditions,
            normalizedActions);
        return new RuleParseResult(definition, []);
    }

    /// <summary>Serializes conditions to the stored JSON shape.</summary>
    public static string SerializeConditions(IReadOnlyList<RuleCondition> conditions) => JsonSerializer.Serialize(conditions, Options);

    /// <summary>Serializes actions to the stored JSON shape.</summary>
    public static string SerializeActions(RuleActions actions) => JsonSerializer.Serialize(actions, Options);

    /// <summary>Trims, collapses whitespace and lower-cases invariantly; no PII placeholders, unlike the analytics scrubber.</summary>
    internal static string NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : Whitespace().Replace(text.Trim(), " ").ToLowerInvariant();

    private static T? Deserialize<T>(string? json, string empty, string root, List<RuleError> errors)
        where T : class
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(string.IsNullOrWhiteSpace(json) ? empty : json, Options);
            if (value is null)
            {
                errors.Add(new RuleError(root, empty == "[]" ? "Must be an array." : "Must be an object."));
            }

            return value;
        }
        catch (JsonException exception)
        {
            // Syntax errors carry the reader's exception as inner and belong to the whole document; member errors keep their path.
            string path = exception.InnerException is JsonException ? root : MapPath(root, exception.Path);
            errors.Add(new RuleError(path, exception.Message));
            return null;
        }
    }

    // "$[0].pattern" → "conditions[0].pattern"; "$.pins[1]" → "actions.pins[1]".
    private static string MapPath(string root, string? path) =>
        string.IsNullOrEmpty(path) || path == "$" ? root : root + path[1..];

    private static (DateTime? From, DateTime? Until) ValidateMetadata(RuleMetadata metadata, List<RuleError> errors)
    {
        if (string.IsNullOrWhiteSpace(metadata.IndexName) || metadata.IndexName.Length > RuleLimits.IndexNameLength)
        {
            errors.Add(new RuleError("metadata.indexName", $"Required, at most {RuleLimits.IndexNameLength} characters."));
        }

        if (metadata.Priority < 1)
        {
            errors.Add(new RuleError("metadata.priority", "Must be 1 or greater."));
        }

        var from = AsUtc(metadata.ValidFromUtc, "metadata.validFromUtc", errors);
        var until = AsUtc(metadata.ValidUntilUtc, "metadata.validUntilUtc", errors);
        if (from >= until)
        {
            errors.Add(new RuleError("metadata.validUntilUtc", "Must be later than validFromUtc."));
        }

        return (from, until);
    }

    private static DateTime? AsUtc(DateTime? value, string path, List<RuleError> errors)
    {
        if (value is not { } date)
        {
            return null;
        }

        if (date.Kind == DateTimeKind.Local)
        {
            errors.Add(new RuleError(path, "Must be UTC, not local time."));
            return null;
        }

        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    private static List<RuleCondition> NormalizeConditions(List<RuleCondition?> conditions, List<RuleError> errors)
    {
        if (conditions.Count > RuleLimits.Conditions)
        {
            errors.Add(new RuleError("conditions", $"At most {RuleLimits.Conditions} conditions."));
        }

        var result = new List<RuleCondition>(conditions.Count);
        for (int i = 0; i < conditions.Count; i++)
        {
            string path = $"conditions[{i}]";
            if (conditions[i] is not { } condition)
            {
                errors.Add(new RuleError(path, "Must be an object."));
                continue;
            }

            string? pattern = condition.Pattern is null ? null : NormalizeText(condition.Pattern);
            if (pattern is null && condition.Anchoring is not null)
            {
                errors.Add(new RuleError($"{path}.pattern", "Required when anchoring is set."));
            }
            else if (pattern is not null && condition.Anchoring is null)
            {
                errors.Add(new RuleError($"{path}.anchoring", "Required when pattern is set."));
            }
            else if (pattern == string.Empty && condition.Anchoring != RuleAnchoring.Is)
            {
                errors.Add(new RuleError($"{path}.pattern", "An empty pattern is only allowed with \"is\" (the browse query)."));
            }
            else if (pattern?.Length > RuleLimits.PatternLength)
            {
                errors.Add(new RuleError($"{path}.pattern", $"At most {RuleLimits.PatternLength} characters."));
            }

            var facets = condition.Facets ?? [];
            if (facets.Count > RuleLimits.Facets)
            {
                errors.Add(new RuleError($"{path}.facets", $"At most {RuleLimits.Facets} facet pairs."));
            }

            for (int j = 0; j < facets.Count; j++)
            {
                if (facets[j] is not { } facet)
                {
                    errors.Add(new RuleError($"{path}.facets[{j}]", "Must be an object."));
                    continue;
                }

                CheckText(facet.Dimension, RuleLimits.ValueLength, $"{path}.facets[{j}].dimension", errors);
                CheckText(facet.Value, RuleLimits.ValueLength, $"{path}.facets[{j}].value", errors);
            }

            var contexts = condition.Contexts ?? [];
            if (contexts.Count > RuleLimits.Contexts)
            {
                errors.Add(new RuleError($"{path}.contexts", $"At most {RuleLimits.Contexts} contexts."));
            }

            for (int j = 0; j < contexts.Count; j++)
            {
                if (contexts[j] is null || !ContextName().IsMatch(contexts[j]))
                {
                    errors.Add(new RuleError($"{path}.contexts[{j}]", "1–100 letters, digits, '_', '.' or '-'."));
                }
            }

            result.Add(new RuleCondition(condition.Anchoring, pattern, facets, contexts));
        }

        return result;
    }

    private static RuleActions NormalizeActions(RuleActions actions, List<RuleError> errors)
    {
        var pins = actions.Pins ?? [];
        var hides = actions.Hides ?? [];
        var boosts = actions.Boosts ?? [];
        var userData = actions.UserData?.Clone();

        if (pins.Count == 0 && hides.Count == 0 && boosts.Count == 0 && userData is null)
        {
            errors.Add(new RuleError("actions", "At least one pin, hide, boost or userData is required."));
        }

        if (pins.Count > RuleLimits.Pins)
        {
            errors.Add(new RuleError("actions.pins", $"At most {RuleLimits.Pins} pins."));
        }

        var pinnedItems = new HashSet<Guid>();
        var positions = new HashSet<int>();
        for (int i = 0; i < pins.Count; i++)
        {
            string path = $"actions.pins[{i}]";
            if (pins[i] is not { } pin)
            {
                errors.Add(new RuleError(path, "Must be an object."));
                continue;
            }

            if (pin.ItemGuid == Guid.Empty)
            {
                errors.Add(new RuleError($"{path}.itemGuid", "Required."));
            }
            else if (!pinnedItems.Add(pin.ItemGuid))
            {
                errors.Add(new RuleError($"{path}.itemGuid", "Item is already pinned by this rule."));
            }

            if (pin.Position is < 1 or > RuleLimits.MaxPosition)
            {
                errors.Add(new RuleError($"{path}.position", $"Must be 1–{RuleLimits.MaxPosition}."));
            }
            else if (!positions.Add(pin.Position))
            {
                errors.Add(new RuleError($"{path}.position", "Position is already used by this rule."));
            }
        }

        if (hides.Count > RuleLimits.Hides)
        {
            errors.Add(new RuleError("actions.hides", $"At most {RuleLimits.Hides} hides."));
        }

        var hidden = new HashSet<Guid>();
        for (int i = 0; i < hides.Count; i++)
        {
            string path = $"actions.hides[{i}]";
            if (hides[i] == Guid.Empty)
            {
                errors.Add(new RuleError(path, "Required."));
            }
            else if (!hidden.Add(hides[i]))
            {
                errors.Add(new RuleError(path, "Item is already hidden by this rule."));
            }
            else if (pinnedItems.Contains(hides[i]))
            {
                errors.Add(new RuleError(path, "Item is also pinned by this rule."));
            }
        }

        if (boosts.Count > RuleLimits.Boosts)
        {
            errors.Add(new RuleError("actions.boosts", $"At most {RuleLimits.Boosts} boosts."));
        }

        var boosted = new HashSet<(string, string)>();
        for (int i = 0; i < boosts.Count; i++)
        {
            string path = $"actions.boosts[{i}]";
            if (boosts[i] is not { } boost)
            {
                errors.Add(new RuleError(path, "Must be an object."));
                continue;
            }

            bool fieldOk = CheckText(boost.Field, RuleLimits.BoostFieldLength, $"{path}.field", errors);
            bool valueOk = CheckText(boost.Value, RuleLimits.ValueLength, $"{path}.value", errors);
            if (boost.Strength == 0 || Math.Abs(boost.Strength) > RuleLimits.MaxStrength)
            {
                errors.Add(new RuleError($"{path}.strength", $"Must be −{RuleLimits.MaxStrength}..{RuleLimits.MaxStrength} and not 0."));
            }

            if (fieldOk && valueOk && !boosted.Add((boost.Field, boost.Value)))
            {
                errors.Add(new RuleError(path, "Field and value are already boosted by this rule."));
            }
        }

        if (userData is { } data)
        {
            if (data.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new RuleError("actions.userData", "Must be a JSON object."));
            }
            else if (JsonSerializer.SerializeToUtf8Bytes(data).Length > RuleLimits.UserDataBytes)
            {
                errors.Add(new RuleError("actions.userData", $"At most {RuleLimits.UserDataBytes} bytes."));
            }
        }

        return new RuleActions(pins, hides, boosts, userData, actions.PinsRespectFilters);
    }

    private static bool CheckText(string? value, int maxLength, string path, List<RuleError> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            errors.Add(new RuleError(path, $"Required, at most {maxLength} characters."));
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]{1,100}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ContextName();
}
