using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public class SiteSearchAccessTests
{
    private const int ExistingIndex = 7;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shortcut_IsVisibleOnlyWithSiteSearchView_EvaluatedOncePerRequest(bool permitted)
    {
        int evaluations = 0;
        var evaluator = IndexListingPageExtender.VisibleWhen(new Lazy<Task<bool>>(() =>
        {
            evaluations++;
            return Task.FromResult(permitted);
        }));
        var rows = Enumerable.Range(0, 3).Select(_ => new ActionConfiguration()).ToList();

        foreach (var row in rows)
        {
            await evaluator(row, null!, CancellationToken.None);
        }

        Assert.All(rows, row => Assert.Equal(permitted, row.Visible));
        Assert.Equal(1, evaluations);
    }

    [Theory]
    [InlineData(ExistingIndex, true)]
    [InlineData(404, false)]
    public void Validate_RequiresAnExistingIndex(int indexIdentifier, bool valid)
    {
        var result = SearchTuningSectionPage.Validate(new FakeIndexManager(), indexIdentifier);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(SearchTuningSectionPage.IndexMissingMessageKey, result.ErrorMessageKey);
    }

    [Fact]
    public async Task SectionAndQueryTester_BothRejectAMissingIndexOnTheirOwnRoute()
    {
        // Kentico validates only the requested page, so the child route must not rely on the section.
        var indexes = new FakeIndexManager();
        Func<Task<PageValidationResult>>[] validations =
        [
            new SearchTuningSectionPage(indexes) { IndexIdentifier = 404 }.ValidatePage,
            new QueryTesterPage(indexes, null!, null!, null!, null!, null!, null!, null!, null!, null!) { IndexIdentifier = 404 }.ValidatePage,
        ];

        foreach (var validate in validations)
        {
            var result = await validate();
            Assert.False(result.IsValid);
            Assert.Equal(SearchTuningSectionPage.IndexMissingMessageKey, result.ErrorMessageKey);
        }
    }

    [Fact]
    public void QueryTester_ListsFiredRulesInRunOrder_WithoutALinkForDeletedRules()
    {
        var response = new SuiteSearchResponse([], 0, [], TimeSpan.Zero)
        {
            FiredRuleIds = [9, 3],
            UserData = [new SuiteRuleUserData(3, JsonDocument.Parse("""{"banner":"x"}""").RootElement)],
        };

        var stored = new Dictionary<int, (string, string?)> { [3] = ("Coffee", """{"pins":[{"itemGuid":"x","position":1},{"itemGuid":"y","position":2}],"boosts":[{"field":"a","value":"b","strength":1}]}""") };
        var rules = QueryTesterPage.FiredRules(response, stored, id => $"/rules/{id}");

        Assert.Equal([new QueryTesterFiredRule(9, null, null, null, null, null)], rules.Take(1));
        Assert.Equal((3, "Coffee", "/rules/3", "x"), (rules[1].RuleId, rules[1].Name, rules[1].EditUrl, rules[1].UserData!.Value.GetProperty("banner").GetString()));
        Assert.Equal(("2 pins, 1 boost", "pin"), (rules[1].Actions, rules[1].PrimaryAction));
    }

    [Fact]
    public void QueryTester_ListsAppliedSynonymsInOrder_WithoutALinkForDeletedSynonyms()
    {
        var response = new SuiteSearchResponse([], 0, [], TimeSpan.Zero)
        {
            AppliedSynonyms = [new SuiteAppliedSynonym(4, "mill", "grinder"), new SuiteAppliedSynonym(9, "tee", "t-shirt")],
        };

        var applied = QueryTesterPage.AppliedSynonyms(response, new HashSet<int> { 4 }, id => $"/synonyms/{id}");

        Assert.Equal(
            [new QueryTesterAppliedSynonym(4, "mill", "grinder", "/synonyms/4"), new QueryTesterAppliedSynonym(9, "tee", "t-shirt", null)],
            applied);
    }

    [Fact]
    public void QueryTester_RequestCarriesContextsAndPageSize_AndNeverTracksAnalytics()
    {
        var request = QueryTesterPage.Request(new QueryTesterArguments { Query = "coffee", Contexts = ["vip", "mobile"], PageSize = 20, ApplyRules = false }, "dg");

        Assert.Equal(("dg", "coffee", 20, false, false), (request.IndexName, request.Query, request.PageSize, request.TrackAnalytics, request.ApplyRules));
        Assert.Equal(["vip", "mobile"], request.Contexts);
        Assert.Empty(QueryTesterPage.Request(new QueryTesterArguments(), "dg").Contexts);
        Assert.Equal(10, QueryTesterPage.Request(new QueryTesterArguments(), "dg").PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void QueryTester_RejectsPageSizesOutsideTheCompareRange(int pageSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QueryTesterPage.Request(new QueryTesterArguments { PageSize = pageSize }, "dg"));

    [Fact]
    public void QueryTester_ContactGroups_RequireView_ReportTruncation_AndReturnNothingWhenNotAllowed()
    {
        Assert.Equal(SystemPermissions.VIEW, typeof(QueryTesterPage).GetMethod(nameof(QueryTesterPage.ContactGroups))!.GetCustomAttribute<PageCommandAttribute>()!.Permission);

        var many = Enumerable.Range(0, ContactGroupAccess.MaxGroups + 1).Select(i => new QueryTesterContactGroup($"g{i}", $"Group {i}")).ToList();
        var truncated = ContactGroupAccess.Result(allowed: true, many);
        Assert.True(truncated.Truncated);
        Assert.Equal(ContactGroupAccess.MaxGroups, truncated.Groups.Count);
        Assert.False(ContactGroupAccess.Result(allowed: true, [.. many.Take(3)]).Truncated);
        var none = ContactGroupAccess.Result(allowed: false, []);
        Assert.False(none.Allowed);
        Assert.Empty(none.Groups);
    }

    private sealed class FakeIndexManager : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(int identifier) =>
            // LuceneIndex has only an internal constructor; existence is all the validation reads.
            identifier == ExistingIndex ? (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex)) : null;

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex? GetIndex(string indexName) => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }
}
