using System.Text.Json.Nodes;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RuleEditModelTests
{
    private const string Pin = """{"itemGuid":"11111111-1111-1111-1111-111111111111","position":2}""";
    private const string Hide = "22222222-2222-2222-2222-222222222222";

    private static StoredRule Stored(string actions) => new(
        5, "Coffee", null, true, 3, null, null,
        """[{"anchoring":"contains","pattern":"coffee"}]""", actions,
        new DateTime(2026, 10, 11, 8, 30, 15, 123, DateTimeKind.Utc));

    [Fact]
    public void FromRule_SplitsBoostsAndCustomData_AndCarriesTheVersion()
    {
        var model = RuleEditMapping.FromRule(Stored($$$"""{"pins":[{{{Pin}}}],"boosts":[{"field":"Type","value":"Article","strength":40}],"userData":{"banner":"x"}}"""));

        Assert.Equal("""[{"field":"Type","value":"Article","strength":40}]""", model.Boosts);
        Assert.Equal("x", JsonNode.Parse(model.UserData!)!["banner"]!.GetValue<string>());
        Assert.Equal((3, true, """[{"anchoring":"contains","pattern":"coffee"}]"""), (model.Priority, model.Enabled, model.Conditions));
        Assert.False(RuleEditMapping.IsStale(model.LoadedVersion, Stored("{}")));
    }

    [Fact]
    public void PinsHidesAndFilterSetting_RoundTripThroughTheForm()
    {
        string stored = $$$"""{"pins":[{{{Pin}}}],"hides":["{{{Hide}}}"],"pinsRespectFilters":false,"boosts":[{"field":"Old","value":"x","strength":5}]}""";

        var model = RuleEditMapping.FromRule(Stored(stored));
        var actions = JsonNode.Parse(RuleEditMapping.ActionsJson(model))!;

        Assert.False(model.PinsRespectFilters);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(stored), actions));
    }

    [Fact]
    public void ActionsJson_DropsEmptyLists_AndAddsCustomData()
    {
        var model = new RuleEditModel { Boosts = "[]", Pins = "[]", Hides = " ", UserData = """{"a":1}""" };

        var actions = JsonNode.Parse(RuleEditMapping.ActionsJson(model))!.AsObject();

        Assert.Equal(["userData", "pinsRespectFilters"], actions.Select(property => property.Key));
        Assert.Equal(1, actions["userData"]!["a"]!.GetValue<int>());
        Assert.True(actions["pinsRespectFilters"]!.GetValue<bool>());
    }

    [Fact]
    public void Validate_RefusesAnItemBothPinnedAndHidden_OnTheHide()
    {
        var model = RuleEditMapping.FromRule(Stored($$"""{"pins":[{{Pin}}]}"""));
        model.Hides = """["11111111-1111-1111-1111-111111111111"]""";

        Assert.Equal(["actions.hides[0]"], RuleEditMapping.Validate(model, Stored("{}"), "articles").Errors.Select(error => error.Path));
    }

    [Fact]
    public void HidesConflictRule_NamesOverlappingItems_AndIgnoresMalformedJson()
    {
        string pins = $"[{Pin}]";

        Assert.Contains("11111111-1111-1111-1111-111111111111", RuleHidesConflictValidationRule.Conflict("""["11111111-1111-1111-1111-111111111111"]""", pins));
        Assert.Null(RuleHidesConflictValidationRule.Conflict($"""["{Hide}"]""", pins));
        Assert.Null(RuleHidesConflictValidationRule.Conflict("[{", pins));
        Assert.Null(RuleHidesConflictValidationRule.Conflict("""["11111111-1111-1111-1111-111111111111"]""", "[1]"));
    }

    [Fact]
    public void ValidateAndApply_StoreNormalizedJson_AndPreservePins()
    {
        var stored = Stored($$"""{"pins":[{{Pin}}]}""");
        var model = RuleEditMapping.FromRule(stored);
        model.Conditions = """[{"anchoring":"contains","pattern":"  Coffee  GRINDER "}]""";
        model.Boosts = """[{"field":"Type","value":"Article","strength":50}]""";

        var result = RuleEditMapping.Validate(model, stored, "articles");
        var saved = RuleEditMapping.Apply(model, result.Definition!, stored);

        Assert.Empty(result.Errors);
        Assert.Contains("\"pattern\":\"coffee grinder\"", saved.Conditions);
        var actions = JsonNode.Parse(saved.Actions)!.AsObject();
        Assert.Single(actions["pins"]!.AsArray());
        Assert.Single(actions["boosts"]!.AsArray());
    }

    [Fact]
    public void Validate_ReportsErrorsByPath_IncludingNoActions()
    {
        var stored = Stored("{}");
        var model = new RuleEditModel { DisplayName = "x", Priority = 0, Conditions = """[{"anchoring":"contains","pattern":""}]""" };

        var paths = RuleEditMapping.Validate(model, stored, "articles").Errors.Select(error => error.Path).Order(StringComparer.Ordinal);

        Assert.Equal(["actions", "conditions[0].pattern", "metadata.priority"], paths);
    }

    [Fact]
    public void Dates_RoundTripAsTheEnteredUtcInstant()
    {
        var stored = Stored("""{"userData":{}}""");
        var model = RuleEditMapping.FromRule(stored);
        model.ValidFromUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var saved = RuleEditMapping.Apply(model, RuleEditMapping.Validate(model, stored, "articles").Definition!, stored);

        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), saved.ValidFromUtc);
        Assert.Equal(DateTimeKind.Utc, saved.ValidFromUtc!.Value.Kind);
    }

    [Fact]
    public void IsStale_WhenTheRowChangedAfterLoading()
    {
        var stored = Stored("{}");
        string loaded = RuleEditMapping.VersionOf(stored);

        var changed = stored with { LastModified = stored.LastModified.AddMilliseconds(1) };

        Assert.True(RuleEditMapping.IsStale(loaded, changed));
        Assert.True(RuleEditMapping.IsStale(null, stored));
    }

    [Theory]
    [InlineData(RuleJsonSection.Conditions, """[{"anchoring":"contains","pattern":""},{"facets":[{"dimension":"d","value":""}]}]""", "conditions[0].pattern,conditions[1].facets[0].value")]
    [InlineData(RuleJsonSection.Conditions, "[{", "conditions")]
    [InlineData(RuleJsonSection.Conditions, "[]", "")]
    [InlineData(RuleJsonSection.Boosts, """[{"field":"Type","value":"a","strength":0}]""", "actions.boosts[0].strength")]
    [InlineData(RuleJsonSection.Boosts, "[{", "actions.boosts")]
    [InlineData(RuleJsonSection.Boosts, "[]", "")]
    [InlineData(RuleJsonSection.Pins, $$$"""[{{{Pin}}},{"itemGuid":"33333333-3333-3333-3333-333333333333","position":2}]""", "actions.pins[1].position")]
    [InlineData(RuleJsonSection.Pins, """[{"itemGuid":"33333333-3333-3333-3333-333333333333","position":301}]""", "actions.pins[0].position")]
    [InlineData(RuleJsonSection.Pins, "[{", "actions.pins")]
    [InlineData(RuleJsonSection.Pins, "[]", "")]
    [InlineData(RuleJsonSection.Hides, $"""["{Hide}","{Hide}"]""", "actions.hides[1]")]
    [InlineData(RuleJsonSection.Hides, "[{", "actions.hides")]
    [InlineData(RuleJsonSection.Hides, "[]", "")]
    [InlineData(RuleJsonSection.UserData, "[1]", "actions.userData")]
    [InlineData(RuleJsonSection.UserData, "{", "actions.userData")]
    [InlineData(RuleJsonSection.UserData, "  ", "")]
    [InlineData(RuleJsonSection.UserData, """{"a":1}""", "")]
    public void SectionValidator_ReportsOnlyItsOwnPaths(RuleJsonSection section, string json, string expected) =>
        Assert.Equal(expected, string.Join(",", RuleSectionValidator.Validate(section, json).Select(error => error.Path).Order(StringComparer.Ordinal)));

    [Theory]
    [InlineData("[]", "Every search")]
    [InlineData("""[{"anchoring":"contains","pattern":"coffee"}]""", "contains 'coffee'")]
    [InlineData("""[{"anchoring":"is","pattern":""},{"contexts":["vip"]}]""", "Empty query +1")]
    [InlineData("""[{"facets":[{"dimension":"d","value":"v"}],"contexts":["a","b"]}]""", "Any query, 1 facet, 2 contexts")]
    [InlineData("[{", RuleSummaries.Invalid)]
    public void ConditionsSummary(string json, string expected) => Assert.Equal(expected, RuleSummaries.Conditions(json));

    [Theory]
    [InlineData("{}", "None")]
    [InlineData($$$"""{"pins":[{{{Pin}}}],"hides":["{{{Hide}}}","{{{Hide}}}"],"boosts":[{"field":"a","value":"b","strength":1}],"userData":{}}""", "1 pin, 2 hides, 1 boost, data")]
    [InlineData("{", RuleSummaries.Invalid)]
    public void ActionsSummary(string json, string expected) => Assert.Equal(expected, RuleSummaries.Actions(json));

    [Fact]
    public void FieldMessage_SummarizesStructuredSections_AndShowsCustomDataErrors()
    {
        RuleError[] two = [new("conditions[0].pattern", "a"), new("conditions[1].pattern", "b")];

        Assert.Equal("Fix the 2 highlighted problems.", RuleJsonValidationRule.Message(RuleJsonSection.Conditions, two));
        Assert.Equal("Must be a JSON object.", RuleJsonValidationRule.Message(RuleJsonSection.UserData, [new("actions.userData", "Must be a JSON object.")]));
    }
}
