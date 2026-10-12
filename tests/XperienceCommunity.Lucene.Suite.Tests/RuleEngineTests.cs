using System.Diagnostics;
using System.Text.Json;

using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RuleEngineTests
{
    private const string Index = "articles";
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid A = new("aaaaaaaa-0000-0000-0000-000000000000");
    private static readonly Guid B = new("bbbbbbbb-0000-0000-0000-000000000000");
    private static readonly Guid C = new("cccccccc-0000-0000-0000-000000000000");

    [Theory]
    [InlineData(RuleAnchoring.Is, "coffee grinder", "  Coffee   GRINDER ", true)]
    [InlineData(RuleAnchoring.Is, "coffee grinder", "coffee grinder beans", false)]
    [InlineData(RuleAnchoring.Is, "", "", true)]
    [InlineData(RuleAnchoring.Is, "", null, true)]
    [InlineData(RuleAnchoring.Is, "", "coffee", false)]
    [InlineData(RuleAnchoring.StartsWith, "coffee", "coffee grinder", true)]
    [InlineData(RuleAnchoring.StartsWith, "coffee", "best coffee", false)]
    [InlineData(RuleAnchoring.StartsWith, "coffee grinder", "coffee", false)]
    [InlineData(RuleAnchoring.EndsWith, "grinder", "coffee grinder", true)]
    [InlineData(RuleAnchoring.EndsWith, "grinder", "grinder coffee", false)]
    [InlineData(RuleAnchoring.Contains, "coffee grinder", "best coffee grinder deals", true)]
    [InlineData(RuleAnchoring.Contains, "coffee grinder", "coffee best grinder", false)]
    [InlineData(RuleAnchoring.Contains, "grinder", "coffee grinders", false)]
    [InlineData(RuleAnchoring.Contains, "1234567890", "SKU 1234567890", true)]
    [InlineData(null, null, "anything at all", true)]
    public void Pattern_MatchesWholeWords(object? anchoring, string? pattern, string? query, bool fires)
    {
        var rule = Rule(1, conditions: [Condition((RuleAnchoring?)anchoring, pattern)]);

        Assert.Equal(fires, Fired(Evaluate([rule], query)));
    }

    [Fact]
    public void Facets_AreAnded_AgainstAnyAppliedValue()
    {
        var rule = Rule(1, conditions: [Condition(facets: [new("type", "Article"), new("brand", "Acme")])]);

        Assert.True(Fired(Evaluate([rule], facets: new() { ["type"] = ["Blog", "Article"], ["brand"] = ["Acme"] })));
        Assert.False(Fired(Evaluate([rule], facets: new() { ["type"] = ["Article"] })));
        Assert.False(Fired(Evaluate([rule], facets: new() { ["type"] = ["article"], ["brand"] = ["Acme"] })));
    }

    [Fact]
    public void Contexts_AreOred_AndAbsentMeansAnyVisitor()
    {
        var rule = Rule(1, conditions: [Condition(contexts: ["vip", "b2b"])]);

        Assert.True(Fired(Evaluate([rule], contexts: ["b2b"])));
        Assert.False(Fired(Evaluate([rule], contexts: ["guest"])));
        Assert.False(Fired(Evaluate([rule])));
        Assert.True(Fired(Evaluate([Rule(1, conditions: [Condition()])])));
    }

    [Fact]
    public void AnyCondition_Fires_AndZeroConditionsFireOnEverySearch()
    {
        var rule = Rule(1, conditions: [Condition(RuleAnchoring.Is, "tea"), Condition(RuleAnchoring.Is, "coffee")]);

        Assert.True(Fired(Evaluate([rule], "coffee")));
        Assert.False(Fired(Evaluate([rule], "water")));
        Assert.True(Fired(Evaluate([Rule(1)], "water")));
    }

    [Fact]
    public void ValidityWindow_IsFromInclusive_UntilExclusive()
    {
        Assert.True(Fired(Evaluate([Rule(1) with { ValidFromUtc = Now }])));
        Assert.False(Fired(Evaluate([Rule(1) with { ValidFromUtc = Now.AddTicks(1) }])));
        Assert.False(Fired(Evaluate([Rule(1) with { ValidUntilUtc = Now }])));
        Assert.True(Fired(Evaluate([Rule(1) with { ValidUntilUtc = Now.AddTicks(1) }])));
    }

    [Fact]
    public void DisabledRulesAndOtherIndexes_DoNotFire_IndexIsCaseInsensitive()
    {
        Assert.False(Fired(Evaluate([Rule(1) with { Enabled = false }])));
        Assert.False(Fired(Evaluate([Rule(1) with { IndexName = "products" }])));
        Assert.True(Fired(Evaluate([Rule(1) with { IndexName = "ARTICLES" }])));
    }

    [Fact]
    public void FiredRules_AreOrderedByPriorityThenId()
    {
        var outcome = Evaluate([Rule(1, priority: 2), Rule(3, priority: 1), Rule(2, priority: 1)]);

        Assert.Equal([2, 3, 1], outcome.FiredRuleIds);
    }

    [Fact]
    public void Hide_BeatsPin_AcrossRules()
    {
        var outcome = Evaluate([Rule(1, priority: 1, pins: [new(A, 1)]), Rule(2, priority: 2, hides: [A])]);

        Assert.Empty(outcome.Pins);
        Assert.Equal([A], outcome.Hides);
    }

    [Fact]
    public void ItemPinnedByAnEarlierRule_KeepsThatPlacement()
    {
        var outcome = Evaluate([Rule(1, priority: 1, pins: [new(A, 5)]), Rule(2, priority: 2, pins: [new(A, 1)])]);

        Assert.Equal([new ResolvedPin(A, 5, true, 1)], outcome.Pins);
    }

    [Fact]
    public void TakenSlot_ShiftsTheLaterPinDown_AndPinsCarryTheirRulesFilterFlag()
    {
        var outcome = Evaluate(
        [
            Rule(1, priority: 1, pins: [new(A, 1), new(C, 2)]),
            Rule(2, priority: 2, pins: [new(B, 1)], respectFilters: false),
        ]);

        Assert.Equal([new ResolvedPin(A, 1, true, 1), new ResolvedPin(C, 2, true, 1), new ResolvedPin(B, 3, false, 2)], outcome.Pins);
    }

    [Fact]
    public void PinShiftedPastTheLastPosition_IsDropped()
    {
        var outcome = Evaluate([Rule(1, priority: 1, pins: [new(A, RuleLimits.MaxPosition)]), Rule(2, priority: 2, pins: [new(B, RuleLimits.MaxPosition)])]);

        Assert.Equal([A], outcome.Pins.Select(pin => pin.ItemGuid));
    }

    [Fact]
    public void Boosts_AreSummedClampedFilteredAndOrdered()
    {
        var outcome = Evaluate(
        [
            Rule(1, boosts: [new("Title", "sale", 80), new("Brand", "x", 10), new("Title", "old", -80)]),
            Rule(2, boosts: [new("Title", "sale", 50), new("Brand", "x", -10), new("Title", "old", -80)]),
        ]);

        Assert.Equal([new RuleBoost("Title", "old", -100), new RuleBoost("Title", "sale", 100)], outcome.Boosts);
    }

    [Fact]
    public void UserData_IsReturnedInRunOrder_TaggedWithTheRule()
    {
        var outcome = Evaluate([Rule(1, priority: 2, userData: """{"n":1}"""), Rule(2, priority: 1, userData: """{"n":2}"""), Rule(3, priority: 3)]);

        Assert.Equal([2, 1], outcome.UserData.Select(data => data.RuleId));
        Assert.Equal(2, outcome.UserData[0].Data.GetProperty("n").GetInt32());
    }

    [Fact]
    public void Outcome_IsIdentical_ForAnyInputOrder()
    {
        RuleDefinition[] rules =
        [
            Rule(1, priority: 1, pins: [new(A, 1)], hides: [C]),
            Rule(2, priority: 1, pins: [new(B, 1)], boosts: [new("Title", "x", 30)]),
            Rule(3, priority: 2, pins: [new(A, 2), new(C, 3)], hides: [B], userData: """{"r":3}"""),
            Rule(4, priority: 3, boosts: [new("Title", "x", -10), new("Body", "y", 5)]),
        ];
        string expected = Describe(Evaluate(rules));

        var random = new Random(42);
        for (int run = 0; run < 50; run++)
        {
            Assert.Equal(expected, Describe(Evaluate(rules.OrderBy(_ => random.Next()).ToArray())));
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void FiveHundredRules_EvaluateUnderOneMillisecondMedian()
    {
        var rules = Enumerable.Range(1, 500)
            .Select(i => Rule(
                i,
                priority: i % 10 + 1,
                conditions:
                [
                    Condition(RuleAnchoring.Contains, $"term{i} coffee"),
                    Condition(RuleAnchoring.StartsWith, "coffee", facets: [new("type", "Article")]),
                    Condition(RuleAnchoring.Is, $"exact {i}", contexts: ["vip"]),
                ],
                pins: [new(Guid.NewGuid(), i % 20 + 1)],
                boosts: [new("Title", $"v{i % 7}", 5)]))
            .ToArray();
        var input = Input("coffee grinder deals", new() { ["type"] = ["Article"] }, ["vip"]);

        for (int i = 0; i < 20; i++)
        {
            RuleEngine.Evaluate(rules, input);
        }

        var timings = new double[200];
        for (int i = 0; i < timings.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            RuleEngine.Evaluate(rules, input);
            timings[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        Array.Sort(timings);
        double median = timings[timings.Length / 2];
        if (!Debugger.IsAttached)
        {
            Assert.True(median < 1, $"Median {median:F3} ms");
        }
    }

    private static RuleDefinition Rule(
        int id,
        int priority = 1,
        RuleCondition[]? conditions = null,
        RulePin[]? pins = null,
        Guid[]? hides = null,
        RuleBoost[]? boosts = null,
        string? userData = null,
        bool respectFilters = true) =>
        new(
            id,
            Index,
            priority,
            true,
            null,
            null,
            conditions ?? [],
            new RuleActions(pins ?? [], hides ?? [], boosts ?? [], userData is null ? null : JsonDocument.Parse(userData).RootElement.Clone(), respectFilters));

    private static RuleCondition Condition(RuleAnchoring? anchoring = null, string? pattern = null, RuleFacet[]? facets = null, string[]? contexts = null) =>
        new(anchoring, pattern, facets ?? [], contexts ?? []);

    private static RuleInput Input(string? query, Dictionary<string, IReadOnlyList<string>>? facets = null, string[]? contexts = null) =>
        new(Index, query, facets ?? [], new HashSet<string>(contexts ?? [], StringComparer.Ordinal), Now);

    private static RuleOutcome Evaluate(RuleDefinition[] rules, string? query = "coffee", Dictionary<string, IReadOnlyList<string>>? facets = null, string[]? contexts = null) =>
        RuleEngine.Evaluate(rules, Input(query, facets, contexts));

    private static bool Fired(RuleOutcome outcome) => outcome.FiredRuleIds.Count > 0;

    private static string Describe(RuleOutcome outcome) => string.Join(
        " | ",
        string.Join(',', outcome.FiredRuleIds),
        string.Join(',', outcome.Pins),
        string.Join(',', outcome.Hides),
        string.Join(',', outcome.Boosts),
        string.Join(',', outcome.UserData.Select(data => $"{data.RuleId}:{data.Data.GetRawText()}")));
}
