using System.Runtime.CompilerServices;
using System.Text.Json;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

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
            new QueryTesterPage(indexes, null!, null!, null!, null!, null!) { IndexIdentifier = 404 }.ValidatePage,
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

        var rules = QueryTesterPage.FiredRules(response, new Dictionary<int, string> { [3] = "Coffee" }, id => $"/rules/{id}");

        Assert.Equal([new QueryTesterFiredRule(9, null, null, null)], rules.Take(1));
        Assert.Equal((3, "Coffee", "/rules/3", "x"), (rules[1].RuleId, rules[1].Name, rules[1].EditUrl, rules[1].UserData!.Value.GetProperty("banner").GetString()));
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
