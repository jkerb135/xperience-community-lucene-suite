using System.Runtime.CompilerServices;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

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

    [Fact]
    public async Task Validate_WithoutSiteSearchView_DeniesAccessBeforeLookingUpTheIndex()
    {
        var indexes = new FakeIndexManager();

        var result = await SearchTuningSectionPage.Validate(new FakePermissions(false), indexes, ExistingIndex);

        Assert.False(result.IsValid);
        Assert.Equal(SearchTuningSectionPage.AccessDeniedMessageKey, result.ErrorMessageKey);
        Assert.Equal(0, indexes.Lookups);
    }

    [Theory]
    [InlineData(ExistingIndex, true)]
    [InlineData(404, false)]
    public async Task Validate_WithSiteSearchView_RequiresAnExistingIndex(int indexIdentifier, bool valid)
    {
        var result = await SearchTuningSectionPage.Validate(new FakePermissions(true), new FakeIndexManager(), indexIdentifier);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(SearchTuningSectionPage.IndexMissingMessageKey, result.ErrorMessageKey);
    }

    [Fact]
    public async Task SectionAndQueryTester_BothDenyAccessOnTheirOwnRoute()
    {
        // Kentico validates only the requested page, so the child route must not rely on the section.
        var permissions = new FakePermissions(false);
        var indexes = new FakeIndexManager();
        Func<Task<PageValidationResult>>[] validations =
        [
            new SearchTuningSectionPage(permissions, indexes) { IndexIdentifier = ExistingIndex }.ValidatePage,
            new QueryTesterPage(permissions, indexes, null!, null!, null!) { IndexIdentifier = ExistingIndex }.ValidatePage,
        ];

        foreach (var validate in validations)
        {
            var result = await validate();
            Assert.False(result.IsValid);
            Assert.Equal(SearchTuningSectionPage.AccessDeniedMessageKey, result.ErrorMessageKey);
        }
    }

    private sealed class FakePermissions(bool granted) : IUIPermissionEvaluator
    {
        public Task<UIPermissionEvaluationResult> Evaluate(string permission) =>
            Task.FromResult(new UIPermissionEvaluationResult(granted && permission == CMS.Membership.SystemPermissions.VIEW));
    }

    private sealed class FakeIndexManager : ILuceneIndexManager
    {
        public int Lookups { get; private set; }

        public LuceneIndex? GetIndex(int identifier)
        {
            Lookups++;
            // LuceneIndex has only an internal constructor; existence is all the validation reads.
            return identifier == ExistingIndex ? (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex)) : null;
        }

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex? GetIndex(string indexName) => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }
}
