using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using CMS.Core;

using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Membership;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;

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

    [Fact]
    public void Status_IsNeverMappedToOrFromTheStoredRule()
    {
        var stored = Stored($$$"""{"pins":[{{{Pin}}}],"userData":{"a":1}}""");
        var model = RuleEditMapping.FromRule(stored);
        var plain = RuleEditMapping.FromRule(stored);
        model.Status = "Expired";

        Assert.Null(plain.Status);
        Assert.Equal(RuleEditMapping.ActionsJson(plain), RuleEditMapping.ActionsJson(model));
        Assert.Equal(
            RuleEditMapping.Apply(plain, RuleEditMapping.Validate(plain, stored, "articles").Definition!, stored),
            RuleEditMapping.Apply(model, RuleEditMapping.Validate(model, stored, "articles").Definition!, stored));
        Assert.DoesNotContain(typeof(StoredRule).GetProperties(), property => property.Name == nameof(RuleEditModel.Status));
    }

    [Fact]
    public void OpenAndSave_StoresIdenticalJson()
    {
        string conditions = """[{"anchoring":"contains","pattern":"coffee","facets":[{"dimension":"ContentType","value":"Article"}],"contexts":["VIPs","mobile"]}]""";
        string actions = $$"""{"pins":[{{Pin}}],"hides":["{{Hide}}"],"boosts":[{"field":"Type","value":"Article","strength":40}],"userData":{"banner":"x"},"pinsRespectFilters":false}""";
        var first = Save(Stored(actions) with { Conditions = conditions });
        var second = Save(first);

        Assert.Equal(conditions, first.Conditions);
        Assert.Equal((first.Conditions, first.Actions), (second.Conditions, second.Actions));

        static StoredRule Save(StoredRule stored)
        {
            var model = RuleEditMapping.FromRule(stored);
            return RuleEditMapping.Apply(model, RuleEditMapping.Validate(model, stored, "articles").Definition!, stored);
        }
    }

    [Theory]
    [InlineData(true, null, null, "Active", "fires on matching searches")]
    [InlineData(true, "2026-10-13T00:00:00", null, "Scheduled", "starts 2026-10-13 00:00 UTC")]
    [InlineData(true, null, "2026-10-12T09:00:00Z", "Expired", "ended 2026-10-12 09:00 UTC")]
    [InlineData(true, null, "2026-10-12T05:00:00-04:00", "Expired", "ended 2026-10-12 09:00 UTC")]
    [InlineData(true, "2026-10-12T09:00:00", "2026-10-12T09:00:01", "Active", "fires on matching searches")]
    [InlineData(false, "2026-10-13T00:00:00", null, "Disabled", null)]
    public void StatusCommand_UsesTheListingRule_OnUtcInstants(bool enabled, string? from, string? until, string status, string? detail)
    {
        var now = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
        var fromUtc = RuleStatusFormComponent.ParseUtc(from);
        var untilUtc = RuleStatusFormComponent.ParseUtc(until);

        var result = RuleStatusFormComponent.Describe(enabled, fromUtc, untilUtc, now);

        Assert.Equal((status, detail), (result.Status, result.Detail));
        Assert.Equal(RuleListing.Of(enabled, fromUtc, untilUtc, now).ToString(), result.Status);
    }

    [Theory]
    [InlineData("Conditions", "[{\"anchoring\":\"contains\",\"pattern\":\"\"}]", "conditions[0]")]
    [InlineData("Boosts", "[{\"field\":\"\",\"value\":\"x\",\"factor\":2}]", "actions.boosts[0]")]
    [InlineData("Pins", "[{\"itemGuid\":\"not-a-guid\",\"position\":1}]", "actions.pins[0]")]
    [InlineData("Hides", "[\"not-a-guid\"]", "actions.hides[0]")]
    [InlineData("UserData", "{}", "section")]
    [InlineData("1", "[]", "section")]
    [InlineData(null, "[]", "section")]
    public async Task ValidateSectionCommand_UsesTheSectionValidator_AndRejectsOtherSections(string? section, string json, string pathPrefix)
    {
        // The commands need no page state; the Kentico base constructor would need its IoC container.
        var page = (RuleCreatePage)RuntimeHelpers.GetUninitializedObject(typeof(RuleCreatePage));

        var result = await page.ValidateSection(new RuleJsonValidationArguments { Section = section, Value = json });

        Assert.Contains(result.Result.Errors, error => error.Path.StartsWith(pathPrefix, StringComparison.Ordinal));
        if (pathPrefix == "section")
        {
            Assert.Equal(RuleEditPageBase.UnknownSectionMessage, Assert.Single(result.Result.Errors).Message);
        }
    }

    [Fact]
    public async Task RuleStatusCommand_DescribesTheFormValues()
    {
        // The commands need no page state; the Kentico base constructor would need its IoC container.
        var page = (RuleCreatePage)RuntimeHelpers.GetUninitializedObject(typeof(RuleCreatePage));

        var result = await page.RuleStatus(new RuleStatusArguments { Enabled = false, ValidFromUtc = "2999-01-01T00:00:00" });

        Assert.Equal(("Disabled", null), (result.Result.Status, result.Result.Detail));
    }

    [Theory]
    [InlineData("2026-10-13T00:00:00", "2026-10-13T00:00:00.0000000Z")]
    [InlineData("2026-10-13T04:00:00.000Z", "2026-10-13T04:00:00.0000000Z")]
    [InlineData("2026-10-13T00:00:00-04:00", "2026-10-13T04:00:00.0000000Z")]
    [InlineData("", null)]
    [InlineData("not a date", null)]
    [InlineData(null, null)]
    public void StatusCommand_ReadsDatesAsUtc(string? value, string? expected) =>
        Assert.Equal(expected, RuleStatusFormComponent.ParseUtc(value)?.ToString("O", CultureInfo.InvariantCulture));

    [Fact]
    public void FacetValuesRequest_SearchesTheIndexAlone_WithoutRulesSynonymsContextsOrAnalytics()
    {
        var request = RuleConditionsFormComponent.FacetValuesRequest("articles");

        Assert.Equal(("articles", "", 1, false, false, false), (request.IndexName, request.Query, request.PageSize, request.ApplyRules, request.ApplySynonyms, request.TrackAnalytics));
        Assert.Empty(request.Contexts);
        Assert.Empty(request.Facets);
    }

    [Fact]
    public async Task FacetValues_UseThePagesIndex_AndFailClosed()
    {
        var search = new RecordingSearch(null);
        var log = new RecordingEventLog();

        var facets = (await Conditions(search, log, indexIdentifier: 7).FacetValues()).Result;
        Assert.Null(facets.Error);
        Assert.Equal("ContentType", Assert.Single(facets.Facets).Dimension);
        Assert.Equal("articles", Assert.Single(search.Requests).IndexName);

        Assert.Equal(RuleItemsFormComponent.IndexMissingMessage, (await Conditions(search, log, indexIdentifier: 8).FacetValues()).Result.Error);
        Assert.Equal(RuleItemsFormComponent.IndexMissingMessage, (await Conditions(search, log, indexIdentifier: 0).FacetValues()).Result.Error);
        Assert.Single(search.Requests);

        var failed = (await Conditions(new RecordingSearch(new InvalidOperationException("boom")), log, indexIdentifier: 7).FacetValues()).Result;
        Assert.Equal((RuleConditionsFormComponent.FacetValuesError, 0), (failed.Error, failed.Facets.Count));
        Assert.Single(log.Events);
    }

    [Fact]
    public async Task ContactGroups_ReturnNothing_WithoutASignedInUserWithContactGroupsView()
    {
        // A null user short-circuits before the permission evaluator and the contact group provider (both null here).
        var result = (await Conditions(new RecordingSearch(null), new RecordingEventLog(), indexIdentifier: 7).ContactGroups()).Result;

        Assert.False(result.Allowed);
        Assert.Empty(result.Groups);
    }

    private static RuleConditionsFormComponent Conditions(ISuiteSearchService search, IEventLogService log, int indexIdentifier) =>
        new(new NoLocalization(), new OneIndexManager(), search, null!, new NoUser(), null!, log) { IndexIdentifier = indexIdentifier };

    private sealed class OneIndexManager : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(int identifier)
        {
            if (identifier != 7)
            {
                return null;
            }

            // LuceneIndex has only an internal constructor; the command reads nothing but the name.
            var index = (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex));
            typeof(LuceneIndex).GetField("<IndexName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(index, "articles");
            return index;
        }

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex? GetIndex(string indexName) => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }

    private sealed class RecordingSearch(Exception? failure) : ISuiteSearchService
    {
        public List<SuiteSearchRequest> Requests { get; } = [];

        public SuiteSearchResponse Search(SuiteSearchRequest request)
        {
            Requests.Add(request);
            return failure is not null
                ? throw failure
                : new SuiteSearchResponse([], 15, [new SuiteFacet("ContentType", [new SuiteFacetValue("Article", 12), new SuiteFacetValue("Cafe", 3)])], TimeSpan.Zero);
        }
    }

    private sealed class RecordingEventLog : IEventLogService
    {
        public List<EventLogData> Events { get; } = [];

        public void LogEvent(EventLogData eventLogData) => Events.Add(eventLogData);
    }

    private sealed class NoLocalization : ILocalizationService
    {
        public string GetString(string resourceKey, string? culture = null, bool useDefaultCulture = true) => resourceKey;

        public string LocalizeExpression(string expression, string? culture = null, bool encode = false, Func<string, string, bool, string>? getStringMethod = null, bool useDefaultCulture = true) => expression;

        public string LocalizeString(string inputText, string? culture = null, bool encode = false, bool useDefaultCulture = true) => inputText;
    }

    private sealed class NoUser : IAuthenticatedUserAccessor
    {
        public Task<AdminApplicationUser> Get() => Task.FromResult<AdminApplicationUser>(null!);
    }
}
