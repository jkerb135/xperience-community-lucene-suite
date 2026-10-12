using System.Text.Json;

using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RuleSerializerTests
{
    private const string G1 = "11111111-1111-1111-1111-111111111111";
    private const string G2 = "22222222-2222-2222-2222-222222222222";
    private const string NoConditions = "[]";
    private const string HideOne = $$"""{"hides":["{{G1}}"]}""";

    private static readonly RuleMetadata Metadata = new(7, "articles", 1, true, null, null);

    [Fact]
    public void ValidRule_IsNormalized_AndRoundTripsLosslessly()
    {
        const string conditions = """[{"anchoring":"contains","pattern":"  Coffee \t GRINDER ","facets":[{"dimension":"ContentType","value":"Article"}],"contexts":["vip"]}]""";
        const string actions = $$"""{"pins":[{"itemGuid":"{{G1}}","position":1}],"hides":["{{G2}}"],"boosts":[{"field":"Title","value":"sale","strength":-20}],"userData":{"banner":"x"},"pinsRespectFilters":false}""";

        var result = RuleSerializer.Parse(Metadata, conditions, actions);

        Assert.Empty(result.Errors);
        var rule = Assert.IsType<RuleDefinition>(result.Definition);
        var condition = Assert.Single(rule.Conditions);
        Assert.Equal(RuleAnchoring.Contains, condition.Anchoring);
        Assert.Equal("coffee grinder", condition.Pattern);
        Assert.Equal(new RuleFacet("ContentType", "Article"), Assert.Single(condition.Facets));
        Assert.False(rule.Actions.PinsRespectFilters);
        Assert.Equal("x", rule.Actions.UserData!.Value.GetProperty("banner").GetString());

        string conditionsJson = RuleSerializer.SerializeConditions(rule.Conditions);
        string actionsJson = RuleSerializer.SerializeActions(rule.Actions);
        Assert.Contains("\"anchoring\":\"contains\"", conditionsJson);
        var again = RuleSerializer.Parse(Metadata, conditionsJson, actionsJson).Definition!;
        Assert.Equal(conditionsJson, RuleSerializer.SerializeConditions(again.Conditions));
        Assert.Equal(actionsJson, RuleSerializer.SerializeActions(again.Actions));
    }

    [Fact]
    public void EmptyPatternWithIs_IsTheBrowseQuery_AndValid()
    {
        var result = RuleSerializer.Parse(Metadata, """[{"anchoring":"is","pattern":"  "}]""", HideOne);

        Assert.Empty(result.Errors);
        Assert.Equal(string.Empty, Assert.Single(result.Definition!.Conditions).Pattern);
    }

    [Fact]
    public void MissingOptionalParts_DefaultToEmpty_AndPinsRespectFilters()
    {
        var result = RuleSerializer.Parse(Metadata, null, $$"""{"pins":[{"itemGuid":"{{G1}}","position":3}]}""");

        var rule = Assert.IsType<RuleDefinition>(result.Definition);
        Assert.Empty(rule.Conditions);
        Assert.Empty(rule.Actions.Hides);
        Assert.Empty(rule.Actions.Boosts);
        Assert.Null(rule.Actions.UserData);
        Assert.True(rule.Actions.PinsRespectFilters);
    }

    [Fact]
    public void UnspecifiedValidityKind_IsReadAsUtc()
    {
        var metadata = Metadata with { ValidFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) };

        var rule = RuleSerializer.Parse(metadata, NoConditions, HideOne).Definition!;

        Assert.Equal(DateTimeKind.Utc, rule.ValidFromUtc!.Value.Kind);
    }

    [Fact]
    public void UserData_OutlivesTheParsedDocument()
    {
        var rule = RuleSerializer.Parse(Metadata, NoConditions, """{"userData":{"a":[1,2]}}""").Definition!;
        GC.Collect();

        Assert.Equal(2, rule.Actions.UserData!.Value.GetProperty("a").GetArrayLength());
    }

    [Fact]
    public void EveryError_IsCollected_NotJustTheFirst()
    {
        var result = RuleSerializer.Parse(
            Metadata with { Priority = 0 },
            """[{"anchoring":"contains","pattern":""},{"contexts":["bad name"]}]""",
            $$"""{"pins":[{"itemGuid":"{{G1}}","position":0}]}""");

        Assert.Null(result.Definition);
        Assert.Equal(
            ["actions.pins[0].position", "conditions[0].pattern", "conditions[1].contexts[0]", "metadata.priority"],
            result.Errors.Select(error => error.Path).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(InvalidJson))]
    public void InvalidJson_ReportsThePath(string conditions, string actions, string expectedPath)
    {
        var result = RuleSerializer.Parse(Metadata, conditions, actions);

        Assert.Null(result.Definition);
        Assert.Contains(expectedPath, result.Errors.Select(error => error.Path));
    }

    [Theory]
    [MemberData(nameof(InvalidMetadata))]
    public void InvalidMetadata_ReportsThePath(object metadata, string expectedPath)
    {
        var result = RuleSerializer.Parse((RuleMetadata)metadata, NoConditions, HideOne);

        Assert.Null(result.Definition);
        Assert.Equal(expectedPath, Assert.Single(result.Errors).Path);
    }

    [Fact]
    public void NormalizeText_KeepsDigitRuns_UnlikeTheAnalyticsScrubber() =>
        Assert.Equal("sku 1234567890", RuleSerializer.NormalizeText("  SKU   1234567890 "));

    public static TheoryData<string, string, string> InvalidJson()
    {
        string Conditions(string condition) => $"[{condition}]";
        string Pins(int count) => string.Join(',', Enumerable.Range(1, count).Select(i => $$"""{"itemGuid":"{{Guid.NewGuid()}}","position":{{Math.Min(i, 300)}}}"""));
        string Guids(int count) => string.Join(',', Enumerable.Range(0, count).Select(_ => $"\"{Guid.NewGuid()}\""));
        string Boosts(int count) => string.Join(',', Enumerable.Range(0, count).Select(i => $$"""{"field":"Title","value":"v{{i}}","strength":1}"""));
        string Facets(int count) => string.Join(',', Enumerable.Range(0, count).Select(i => $$"""{"dimension":"d","value":"v{{i}}"}"""));
        string Contexts(int count) => string.Join(',', Enumerable.Range(0, count).Select(i => $"\"c{i}\""));
        string Pin(string guid, int position) => $$"""{"itemGuid":"{{guid}}","position":{{position}}}""";
        string Boost(string field, string value, int strength) => $$"""{"field":"{{field}}","value":"{{value}}","strength":{{strength}}}""";

        return new TheoryData<string, string, string>
        {
            // Malformed and unknown members.
            { "[{", HideOne, "conditions" },
            { NoConditions, "{\"hides\":", "actions" },
            { Conditions("""{"anchoring":"is","pattern":"a","foo":1}"""), HideOne, "conditions[0].foo" },
            { NoConditions, $$"""{"hides":["{{G1}}"],"bar":1}""", "actions.bar" },
            { Conditions("""{"anchoring":"fuzzy","pattern":"a"}"""), HideOne, "conditions[0].anchoring" },
            { "null", HideOne, "conditions" },

            // Conditions.
            { Conditions("""{"anchoring":"contains","pattern":" "}"""), HideOne, "conditions[0].pattern" },
            { Conditions("""{"anchoring":"is"}"""), HideOne, "conditions[0].pattern" },
            { Conditions("""{"pattern":"a"}"""), HideOne, "conditions[0].anchoring" },
            { "[" + string.Join(',', Enumerable.Repeat("{}", 26)) + "]", HideOne, "conditions" },
            { Conditions($$"""{"anchoring":"is","pattern":"{{new string('a', 201)}}"}"""), HideOne, "conditions[0].pattern" },
            { Conditions($$"""{"facets":[{{Facets(11)}}]}"""), HideOne, "conditions[0].facets" },
            { Conditions("""{"facets":[{"dimension":"","value":"v"}]}"""), HideOne, "conditions[0].facets[0].dimension" },
            { Conditions($$"""{"facets":[{"dimension":"d","value":"{{new string('v', 201)}}"}]}"""), HideOne, "conditions[0].facets[0].value" },
            { Conditions($$"""{"contexts":[{{Contexts(21)}}]}"""), HideOne, "conditions[0].contexts" },
            { Conditions("""{"contexts":["bad name"]}"""), HideOne, "conditions[0].contexts[0]" },
            { Conditions("""{"contexts":["vip\n"]}"""), HideOne, "conditions[0].contexts[0]" },
            { Conditions($$"""{"contexts":["{{new string('c', 101)}}"]}"""), HideOne, "conditions[0].contexts[0]" },

            // Pins.
            { NoConditions, $$"""{"pins":[{{Pin(G1, 0)}}]}""", "actions.pins[0].position" },
            { NoConditions, $$"""{"pins":[{{Pin(G1, 301)}}]}""", "actions.pins[0].position" },
            { NoConditions, $$"""{"pins":[{{Pin(G1, 2)}},{{Pin(G2, 2)}}]}""", "actions.pins[1].position" },
            { NoConditions, $$"""{"pins":[{{Pin(G1, 1)}},{{Pin(G1, 2)}}]}""", "actions.pins[1].itemGuid" },
            { NoConditions, $$"""{"pins":[{{Pin(Guid.Empty.ToString(), 1)}}]}""", "actions.pins[0].itemGuid" },
            { NoConditions, $$"""{"pins":[{{Pins(301)}}]}""", "actions.pins" },

            // Hides.
            { NoConditions, $$"""{"hides":[{{Guids(51)}}]}""", "actions.hides" },
            { NoConditions, $$"""{"hides":["{{Guid.Empty}}"]}""", "actions.hides[0]" },
            { NoConditions, $$"""{"hides":["{{G1}}","{{G1}}"]}""", "actions.hides[1]" },
            { NoConditions, $$"""{"pins":[{{Pin(G1, 1)}}],"hides":["{{G1}}"]}""", "actions.hides[0]" },

            // Boosts.
            { NoConditions, $$"""{"boosts":[{{Boost("Title", "a", 0)}}]}""", "actions.boosts[0].strength" },
            { NoConditions, $$"""{"boosts":[{{Boost("Title", "a", 101)}}]}""", "actions.boosts[0].strength" },
            { NoConditions, $$"""{"boosts":[{{Boost("Title", "a", -101)}}]}""", "actions.boosts[0].strength" },
            { NoConditions, $$"""{"boosts":[{{Boost("", "a", 5)}}]}""", "actions.boosts[0].field" },
            { NoConditions, $$"""{"boosts":[{{Boost("Title", " ", 5)}}]}""", "actions.boosts[0].value" },
            { NoConditions, $$"""{"boosts":[{{Boost(new string('f', 101), "a", 5)}}]}""", "actions.boosts[0].field" },
            { NoConditions, $$"""{"boosts":[{{Boost("Title", new string('v', 201), 5)}}]}""", "actions.boosts[0].value" },
            { NoConditions, $$"""{"boosts":[{{Boost("Title", "a", 5)}},{{Boost("Title", "a", -5)}}]}""", "actions.boosts[1]" },
            { NoConditions, $$"""{"boosts":[{{Boosts(51)}}]}""", "actions.boosts" },

            // UserData and empty actions.
            { NoConditions, """{"userData":[1]}""", "actions.userData" },
            { NoConditions, $$$"""{"userData":{"a":"{{{new string('x', 1020)}}}"}}""", "actions.userData" },
            { NoConditions, "{}", "actions" },
            { NoConditions, """{"pins":[],"hides":[],"boosts":[]}""", "actions" },
        };
    }

    public static TheoryData<object, string> InvalidMetadata() => new()
    {
        { Metadata with { IndexName = " " }, "metadata.indexName" },
        { Metadata with { IndexName = new string('i', 201) }, "metadata.indexName" },
        { Metadata with { Priority = 0 }, "metadata.priority" },
        { Metadata with { ValidFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) }, "metadata.validFromUtc" },
        { Metadata with { ValidFromUtc = Utc(2026, 2, 1), ValidUntilUtc = Utc(2026, 2, 1) }, "metadata.validUntilUtc" },
        { Metadata with { ValidFromUtc = Utc(2026, 3, 1), ValidUntilUtc = Utc(2026, 2, 1) }, "metadata.validUntilUtc" },
    };

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
