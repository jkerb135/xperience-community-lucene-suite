using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Kentico.Xperience.Admin.Base.FormAnnotations;

using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>The rule editor form: native fields for the metadata, custom components for conditions and actions.</summary>
[FormCategory(Label = "General", Order = 1)]
[FormCategory(Label = "Conditions", Order = 10)]
[FormCategory(Label = "Actions", Tooltip = "What happens to the results when the rule fires.", Order = 20)]
[FormCategory(Label = "Custom data", Order = 30)]
public sealed class RuleEditModel
{
    /// <summary>Display only: the live status shown above the form. Never mapped to or from the stored rule.</summary>
    [RuleStatusComponent(Label = "Status", Order = 0)]
    public string? Status { get; set; }

    /// <summary>Name shown to marketers.</summary>
    [RequiredValidationRule(ErrorMessage = "Enter a name.")]
    [MaxLengthValidationRule(RuleInfo.DisplayNameMaxLength)]
    [TextInputComponent(Label = "Name", Order = 2)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional notes.</summary>
    [TextAreaComponent(Label = "Description", Order = 3)]
    public string? Description { get; set; }

    /// <summary>Whether the rule can fire.</summary>
    [CheckBoxComponent(Label = "Enabled", Order = 4)]
    public bool Enabled { get; set; } = true;

    /// <summary>1 runs first.</summary>
    [RequiredValidationRule]
    [MinimumIntegerValueValidationRule(1)]
    [NumberInputComponent(Label = "Priority", ExplanationText = "Lower numbers run first. When two rules pin to the same position, the earlier rule wins.", Order = 5)]
    public int Priority { get; set; } = 10;

    /// <summary>Inclusive UTC start; empty is unbounded.</summary>
    [DateTimeInputComponent(Label = "Valid from", ExplanationText = "In your local time; the status line above shows it in UTC. Leave empty to start immediately.", Order = 6)]
    public DateTime? ValidFromUtc { get; set; }

    /// <summary>Exclusive UTC end; empty is unbounded.</summary>
    [RuleValidityValidationRule]
    [DateTimeInputComponent(Label = "Valid until", ExplanationText = "In your local time; the status line above shows it in UTC. Leave empty to run indefinitely.", Order = 7)]
    public DateTime? ValidUntilUtc { get; set; }

    /// <summary>Conditions JSON (array).</summary>
    [RuleJsonValidationRule(RuleJsonSection.Conditions)]
    [RuleConditionsComponent(Label = "Conditions", Order = 11)]
    public string Conditions { get; set; } = "[]";

    /// <summary>Boosts JSON (array).</summary>
    [RuleJsonValidationRule(RuleJsonSection.Boosts)]
    [RuleBoostsComponent(Label = "Boosts and buries", Order = 21)]
    public string Boosts { get; set; } = "[]";

    /// <summary>Pins JSON (array of itemGuid and position).</summary>
    [RuleJsonValidationRule(RuleJsonSection.Pins)]
    [RulePinsComponent(Label = "Pinned items", Order = 22)]
    public string Pins { get; set; } = "[]";

    /// <summary>Whether pinned items must still match the applied facets.</summary>
    [CheckBoxComponent(Label = "Pinned items must match the applied filters", Order = 23)]
    public bool PinsRespectFilters { get; set; } = true;

    /// <summary>Hides JSON (array of item GUIDs).</summary>
    [RuleJsonValidationRule(RuleJsonSection.Hides)]
    [RuleHidesConflictValidationRule]
    [RuleHidesComponent(Label = "Hidden items", Order = 24)]
    public string Hides { get; set; } = "[]";

    /// <summary>Custom data returned with fired results (JSON object).</summary>
    [RuleJsonValidationRule(RuleJsonSection.UserData)]
    [TextAreaComponent(
        Label = "Custom data",
        ExplanationText = "Returned with the search results when the rule fires, for example to show a banner. A JSON object of at most 1 KB.",
        WatermarkText = """{ "banner": "autumn-sale" }""",
        MinRowsNumber = 4,
        Order = 31)]
    public string? UserData { get; set; }

    /// <summary>The stored row's LastModified when the form was loaded; a mismatch at save means someone else changed the rule.</summary>
    [RuleHiddenComponent(Order = 32)]
    public string? LoadedVersion { get; set; }
}

/// <summary>The stored fields of a rule, decoupled from <see cref="RuleInfo"/> (which needs a running Kentico instance).</summary>
internal sealed record StoredRule(
    int RuleId,
    string DisplayName,
    string? Description,
    bool Enabled,
    int Priority,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    string Conditions,
    string Actions,
    DateTime LastModified)
{
    /// <summary>An unsaved rule.</summary>
    public static StoredRule New { get; } = new(0, string.Empty, null, true, 10, null, null, "[]", "{}", default);

    public static StoredRule From(RuleInfo rule) => new(
        rule.RuleID, rule.DisplayName, rule.Description, rule.Enabled, rule.Priority,
        rule.ValidFromUtc, rule.ValidUntilUtc, rule.Conditions, rule.Actions, rule.LastModified);

    public void CopyTo(RuleInfo rule)
    {
        rule.DisplayName = DisplayName;
        rule.Description = Description;
        rule.Enabled = Enabled;
        rule.Priority = Priority;
        rule.ValidFromUtc = ValidFromUtc;
        rule.ValidUntilUtc = ValidUntilUtc;
        rule.Conditions = Conditions;
        rule.Actions = Actions;
    }
}

/// <summary>Maps between <see cref="StoredRule"/> and <see cref="RuleEditModel"/>.</summary>
internal static class RuleEditMapping
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Version token compared at save (round-trip format of LastModified).</summary>
    public static string VersionOf(StoredRule rule) => VersionOf(rule.LastModified);

    /// <inheritdoc cref="VersionOf(StoredRule)"/>
    public static string VersionOf(DateTime lastModified) => lastModified.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>True when the row changed after the form loaded it (or the form carries no version).</summary>
    public static bool IsStale(string? loadedVersion, StoredRule stored) => loadedVersion != VersionOf(stored);

    public static RuleEditModel FromRule(StoredRule rule)
    {
        var actions = ParseObject(rule.Actions);
        return new RuleEditModel
        {
            DisplayName = rule.DisplayName,
            Description = rule.Description,
            Enabled = rule.Enabled,
            Priority = rule.Priority,
            ValidFromUtc = rule.ValidFromUtc,
            ValidUntilUtc = rule.ValidUntilUtc,
            Conditions = string.IsNullOrWhiteSpace(rule.Conditions) ? "[]" : rule.Conditions,
            Boosts = actions?["boosts"]?.ToJsonString() ?? "[]",
            Pins = actions?["pins"]?.ToJsonString() ?? "[]",
            PinsRespectFilters = actions?["pinsRespectFilters"]?.GetValue<bool>() ?? true,
            Hides = actions?["hides"]?.ToJsonString() ?? "[]",
            UserData = actions?["userData"]?.ToJsonString(Indented),
            LoadedVersion = VersionOf(rule),
        };
    }

    /// <summary>Builds the actions JSON from the form; empty lists are omitted.</summary>
    /// <exception cref="JsonException">A field is not valid JSON (the field validation rules reject this first).</exception>
    public static string ActionsJson(RuleEditModel model)
    {
        var actions = new JsonObject();
        AddArray(actions, "pins", model.Pins);
        AddArray(actions, "hides", model.Hides);
        AddArray(actions, "boosts", model.Boosts);
        if (!string.IsNullOrWhiteSpace(model.UserData))
        {
            actions["userData"] = JsonNode.Parse(model.UserData);
        }

        actions["pinsRespectFilters"] = model.PinsRespectFilters;
        return actions.ToJsonString();
    }

    /// <summary>Validates the whole edited rule against the freshly stored row, as the engine will read it.</summary>
    public static RuleParseResult Validate(RuleEditModel model, StoredRule stored, string indexName)
    {
        string actions;
        try
        {
            actions = ActionsJson(model);
        }
        catch (JsonException exception)
        {
            return new RuleParseResult(null, [new RuleError("actions", exception.Message)]);
        }

        var metadata = new RuleMetadata(stored.RuleId, indexName, model.Priority, model.Enabled, AsUtc(model.ValidFromUtc), AsUtc(model.ValidUntilUtc));
        return RuleSerializer.Parse(metadata, model.Conditions, actions);
    }

    /// <summary>The row's new values: the edited fields, with the normalized JSON of the validated definition.</summary>
    public static StoredRule Apply(RuleEditModel model, RuleDefinition definition, StoredRule stored) => stored with
    {
        DisplayName = model.DisplayName.Trim(),
        Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
        Enabled = definition.Enabled,
        Priority = definition.Priority,
        ValidFromUtc = definition.ValidFromUtc,
        ValidUntilUtc = definition.ValidUntilUtc,
        Conditions = RuleSerializer.SerializeConditions(definition.Conditions),
        Actions = RuleSerializer.SerializeActions(definition.Actions),
    };

    /// <summary>Treats Unspecified as UTC, so the stored value is the instant the marketer entered.</summary>
    public static DateTime? AsUtc(DateTime? value) =>
        value is { } date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;

    private static void AddArray(JsonObject actions, string name, string? json)
    {
        if (!string.IsNullOrWhiteSpace(json) && JsonNode.Parse(json) is var node && node is not JsonArray { Count: 0 })
        {
            actions[name] = node;
        }
    }

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
