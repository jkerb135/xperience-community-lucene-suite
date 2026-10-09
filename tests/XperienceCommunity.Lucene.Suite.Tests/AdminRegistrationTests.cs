using System.Reflection;

using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Lucene.Admin;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;

namespace XperienceCommunity.Lucene.Suite.Tests;

public class AdminRegistrationTests
{
    private static readonly Assembly AdminAssembly = typeof(SiteSearchApplication).Assembly;

    private static IEnumerable<UIPageAttribute> Pages =>
        AdminAssembly.GetCustomAttributes<UIPageAttribute>().Where(p => p is not UIApplicationAttribute);

    [Fact]
    public void Application_IsSiteSearchInDigitalMarketing()
    {
        var application = Assert.Single(AdminAssembly.GetCustomAttributes<UIApplicationAttribute>());

        Assert.Equal(SiteSearchApplication.IDENTIFIER, application.Identifier);
        Assert.Equal(typeof(SiteSearchApplication), application.Type);
        Assert.Equal("site-search", application.Slug);
        Assert.Equal("Site Search", application.Name);
        Assert.Equal(BaseApplicationCategories.DIGITAL_MARKETING, application.Category);
    }

    [Fact]
    public void Application_DeclaresViewAndTheThreeCustomPermissions()
    {
        var permissions = typeof(SiteSearchApplication).GetCustomAttributes<UIPermissionAttribute>()
            .ToDictionary(p => p.Name, p => p.DisplayName);

        Assert.Equal(
            new HashSet<string> { SystemPermissions.VIEW, SiteSearchPermissions.MANAGE_RULES, SiteSearchPermissions.MANAGE_SYNONYMS, SiteSearchPermissions.MANAGE_EXPERIMENTS },
            permissions.Keys.ToHashSet());
        Assert.Equal("Manage rules", permissions[SiteSearchPermissions.MANAGE_RULES]);
        Assert.Equal("Manage synonyms", permissions[SiteSearchPermissions.MANAGE_SYNONYMS]);
        Assert.Equal("Manage experiments", permissions[SiteSearchPermissions.MANAGE_EXPERIMENTS]);
        Assert.All(permissions.Keys.Where(k => k != SystemPermissions.VIEW), k => Assert.StartsWith("LuceneSuite.", k, StringComparison.Ordinal));
    }

    [Fact]
    public void Pages_FormTheChainApplicationListingSectionQueryTester()
    {
        var parents = Pages.ToDictionary(p => p.Type, p => p.ParentType);

        Assert.Equal(typeof(SiteSearchApplication), parents[typeof(SiteSearchIndexListingPage)]);
        Assert.Equal(typeof(SiteSearchIndexListingPage), parents[typeof(SearchTuningSectionPage)]);
        Assert.Equal(typeof(SearchTuningSectionPage), parents[typeof(QueryTesterPage)]);
        Assert.Equal(3, parents.Count);
    }

    [Fact]
    public void Pages_AreNeverParentedToLuceneAdminPages()
    {
        var luceneAdmin = typeof(IndexListingPage).Assembly;

        Assert.All(Pages, p => Assert.NotEqual(luceneAdmin, p.ParentType.Assembly));
    }

    [Fact]
    public void Pages_EvaluateViewAndTheSearchCommandRequiresView()
    {
        Assert.All(Pages, p => Assert.Equal(SystemPermissions.VIEW, p.Type.GetCustomAttribute<UIEvaluatePermissionAttribute>()?.Permission));
        Assert.Equal(SystemPermissions.VIEW, typeof(QueryTesterPage).GetMethod(nameof(QueryTesterPage.Search))!.GetCustomAttribute<PageCommandAttribute>()!.Permission);
    }
}
