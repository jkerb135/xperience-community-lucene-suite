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

        // Literals, not constants: role grants are stored under these names, so they must never change.
        Assert.Equal("XperienceCommunity.Lucene.Suite.SiteSearch", application.Identifier);
        Assert.Equal(typeof(SiteSearchApplication), application.Type);
        Assert.Equal("site-search", application.Slug);
        Assert.Equal("Site search", application.Name);
        Assert.Equal(Icons.Magnifier, application.Icon);
        Assert.Equal(BaseApplicationCategories.DIGITAL_MARKETING, application.Category);
    }

    [Fact]
    public void Application_DeclaresViewAndTheThreeCustomPermissions()
    {
        var permissions = typeof(SiteSearchApplication).GetCustomAttributes<UIPermissionAttribute>()
            .ToDictionary(p => p.Name, p => p.DisplayName);

        Assert.Equal(
            new HashSet<string> { SystemPermissions.VIEW, "LuceneSuite.ManageRules", "LuceneSuite.ManageSynonyms", "LuceneSuite.ManageExperiments" },
            permissions.Keys.ToHashSet());
        Assert.Equal("Manage rules", permissions[SiteSearchPermissions.MANAGE_RULES]);
        Assert.Equal("Manage synonyms", permissions[SiteSearchPermissions.MANAGE_SYNONYMS]);
        Assert.Equal("Manage experiments", permissions[SiteSearchPermissions.MANAGE_EXPERIMENTS]);
    }

    [Fact]
    public void Pages_FormTheChainApplicationListingSectionQueryTester()
    {
        var parents = Pages.ToDictionary(p => p.Type, p => p.ParentType);

        Assert.Equal(typeof(SiteSearchApplication), parents[typeof(SiteSearchIndexListingPage)]);
        Assert.Equal(typeof(SiteSearchIndexListingPage), parents[typeof(SearchTuningSectionPage)]);
        Assert.Equal(typeof(SearchTuningSectionPage), parents[typeof(QueryTesterPage)]);
        Assert.Equal(typeof(SearchTuningSectionPage), parents[typeof(RuleListingPage)]);
        Assert.Equal(typeof(RuleListingPage), parents[typeof(RuleCreatePage)]);
        Assert.Equal(typeof(RuleListingPage), parents[typeof(RuleEditPage)]);
        Assert.Equal(typeof(SearchTuningSectionPage), parents[typeof(SynonymListingPage)]);
        Assert.Equal(typeof(SynonymListingPage), parents[typeof(SynonymCreatePage)]);
        Assert.Equal(typeof(SynonymListingPage), parents[typeof(SynonymEditPage)]);
        Assert.Equal(typeof(SearchTuningSectionPage), parents[typeof(SearchReportsPage)]);
        Assert.Equal(typeof(SearchTuningSectionPage), parents[typeof(BlockedSuggestionListingPage)]);
        Assert.Equal(typeof(BlockedSuggestionListingPage), parents[typeof(BlockedSuggestionCreatePage)]);
        Assert.Equal(12, parents.Count);
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
        Type[] createPages = [typeof(RuleCreatePage), typeof(SynonymCreatePage), typeof(BlockedSuggestionCreatePage)];
        Assert.All(Pages.Where(p => !createPages.Contains(p.Type)), p => Assert.Equal(SystemPermissions.VIEW, p.Type.GetCustomAttribute<UIEvaluatePermissionAttribute>()?.Permission));
        Assert.Equal(SystemPermissions.VIEW, typeof(QueryTesterPage).GetMethod(nameof(QueryTesterPage.Search))!.GetCustomAttribute<PageCommandAttribute>()!.Permission);
        Assert.Equal(SystemPermissions.VIEW, typeof(SearchReportsPage).GetMethod(nameof(SearchReportsPage.Report))!.GetCustomAttribute<PageCommandAttribute>()!.Permission);
        Assert.Equal(SystemPermissions.VIEW, typeof(SearchReportsPage).GetMethod(nameof(SearchReportsPage.Queries))!.GetCustomAttribute<PageCommandAttribute>()!.Permission);
    }

    [Fact]
    public void ReportsPage_IsTheFirstSectionPage_WithItsClientTemplate()
    {
        var reports = Assert.Single(Pages, p => p.Type == typeof(SearchReportsPage));

        Assert.Equal(("reports", "@xperience-community/lucene-suite/SearchReports", Icons.Graph), (reports.Slug, reports.TemplateName, reports.Icon));
        Assert.True(reports.Order < Assert.Single(Pages, p => p.Type == typeof(QueryTesterPage)).Order);
    }

    [Fact]
    public void RulePages_AreRegisteredWithNativeTemplates()
    {
        var listing = Assert.Single(Pages, p => p.Type == typeof(RuleListingPage));
        Assert.Equal(("rules", TemplateNames.LISTING, Icons.ListBullets), (listing.Slug, listing.TemplateName, listing.Icon));
        Assert.True(listing.Order > Assert.Single(Pages, p => p.Type == typeof(QueryTesterPage)).Order);

        var create = Assert.Single(Pages, p => p.Type == typeof(RuleCreatePage));
        Assert.Equal(("create", TemplateNames.EDIT), (create.Slug, create.TemplateName));

        var edit = Assert.Single(Pages, p => p.Type == typeof(RuleEditPage));
        Assert.Equal((PageParameterConstants.PARAMETERIZED_SLUG, TemplateNames.EDIT), (edit.Slug, edit.TemplateName));
    }

    [Fact]
    public void RuleChanges_RequireManageRules_NotSystemPermissionsTheAppLacks()
    {
        // Literal, not the constant: role grants are stored under this name.
        const string manageRules = "LuceneSuite.ManageRules";

        Assert.Equal(manageRules, typeof(RuleCreatePage).GetCustomAttribute<UIEvaluatePermissionAttribute>()?.Permission);
        Assert.Equal(manageRules, Command(typeof(RuleListingPage), nameof(RuleListingPage.Delete)));
        foreach (var page in new[] { typeof(RuleCreatePage), typeof(RuleEditPage) })
        {
            Assert.Equal(manageRules, Command(page, nameof(RuleEditPageBase.Submit)));
            Assert.Equal(manageRules, Command(page, nameof(RuleEditPageBase.Change)));
        }

        static string? Command(Type page, string method) =>
            page.GetMethods().Single(m => m.Name == method && m.GetCustomAttribute<PageCommandAttribute>() is not null).GetCustomAttribute<PageCommandAttribute>()!.Permission;
    }

    [Fact]
    public void SynonymPages_AreRegisteredWithNativeTemplates()
    {
        var listing = Assert.Single(Pages, p => p.Type == typeof(SynonymListingPage));
        Assert.Equal(("synonyms", TemplateNames.LISTING, Icons.ArrowsH), (listing.Slug, listing.TemplateName, listing.Icon));
        Assert.True(listing.Order > Assert.Single(Pages, p => p.Type == typeof(RuleListingPage)).Order);
        Assert.Equal(("create", TemplateNames.EDIT), Shape(typeof(SynonymCreatePage)));
        Assert.Equal((PageParameterConstants.PARAMETERIZED_SLUG, TemplateNames.EDIT), Shape(typeof(SynonymEditPage)));

        static (string, string) Shape(Type page) => Assert.Single(Pages, p => p.Type == page) is var registered ? (registered.Slug, registered.TemplateName) : default;
    }

    [Fact]
    public void SynonymChanges_RequireManageSynonyms_NotSystemPermissionsTheAppLacks()
    {
        // Literal, not the constant: role grants are stored under this name.
        const string manageSynonyms = "LuceneSuite.ManageSynonyms";

        Assert.Equal(manageSynonyms, typeof(SynonymCreatePage).GetCustomAttribute<UIEvaluatePermissionAttribute>()?.Permission);
        Assert.Equal(manageSynonyms, Command(typeof(SynonymListingPage), nameof(SynonymListingPage.Delete)));
        foreach (var page in new[] { typeof(SynonymCreatePage), typeof(SynonymEditPage) })
        {
            Assert.Equal(manageSynonyms, Command(page, nameof(SynonymEditPageBase.Submit)));
            Assert.Equal(manageSynonyms, Command(page, nameof(SynonymEditPageBase.Change)));
        }

        static string? Command(Type page, string method) =>
            page.GetMethods().Single(m => m.Name == method && m.GetCustomAttribute<PageCommandAttribute>() is not null).GetCustomAttribute<PageCommandAttribute>()!.Permission;
    }

    [Fact]
    public void BlockedSuggestionPages_AreRegisteredWithNativeTemplates_AfterSynonyms()
    {
        var listing = Assert.Single(Pages, p => p.Type == typeof(BlockedSuggestionListingPage));
        Assert.Equal(("blocked-suggestions", TemplateNames.LISTING, Icons.BanSign, 400), (listing.Slug, listing.TemplateName, listing.Icon, listing.Order));
        Assert.True(listing.Order > Assert.Single(Pages, p => p.Type == typeof(SynonymListingPage)).Order);

        var create = Assert.Single(Pages, p => p.Type == typeof(BlockedSuggestionCreatePage));
        Assert.Equal(("create", TemplateNames.EDIT), (create.Slug, create.TemplateName));
        Assert.DoesNotContain(Pages, p => p.ParentType == typeof(BlockedSuggestionListingPage) && p.Type != typeof(BlockedSuggestionCreatePage));
    }

    [Fact]
    public void BlockedSuggestionChanges_RequireManageSynonyms_NotSystemPermissionsTheAppLacks()
    {
        // Literal, not the constant: role grants are stored under this name.
        const string manageSynonyms = "LuceneSuite.ManageSynonyms";

        Assert.Equal(manageSynonyms, typeof(BlockedSuggestionCreatePage).GetCustomAttribute<UIEvaluatePermissionAttribute>()?.Permission);
        Assert.Equal(manageSynonyms, Command(typeof(BlockedSuggestionListingPage), nameof(BlockedSuggestionListingPage.Delete)));
        Assert.Equal(manageSynonyms, Command(typeof(BlockedSuggestionCreatePage), nameof(BlockedSuggestionCreatePage.Submit)));
        Assert.Equal(manageSynonyms, Command(typeof(BlockedSuggestionCreatePage), nameof(BlockedSuggestionCreatePage.Change)));
        Assert.Equal(manageSynonyms, Command(typeof(SearchReportsPage), nameof(SearchReportsPage.BlockSuggestion)));

        static string? Command(Type page, string method) =>
            page.GetMethods().Single(m => m.Name == method && m.GetCustomAttribute<PageCommandAttribute>() is not null).GetCustomAttribute<PageCommandAttribute>()!.Permission;
    }

    [Fact]
    public void EveryRoutedSuitePage_ValidatesItsIndex()
    {
        // Kentico validates only the requested node, so each page below the section must repeat the index check.
        var routed = Pages.Where(p => p.Type != typeof(SiteSearchIndexListingPage)).Select(p => p.Type);

        Assert.All(routed, page => Assert.Equal(AdminAssembly, page.GetMethod("ValidatePage")!.DeclaringType!.Assembly));
    }

    [Fact]
    public void QueryTester_HasAMenuIcon() =>
        Assert.Equal(Icons.Magnifier, Assert.Single(Pages, p => p.Type == typeof(QueryTesterPage)).Icon);

    [Fact]
    public void IndexListing_FormatsEntriesByIndexNameAndHasAnEmptyStateCallout()
    {
        var entries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["dancinggoat"] = 38 };

        Assert.Equal("38", SiteSearchIndexListingPage.FormatEntries(entries, "DancingGoat"));
        Assert.Equal(string.Empty, SiteSearchIndexListingPage.FormatEntries(entries, "missing"));
        Assert.Equal(string.Empty, SiteSearchIndexListingPage.FormatEntries(entries, null));

        var callout = SiteSearchIndexListingPage.NoIndexesCallout();
        Assert.Equal(SiteSearchIndexListingPage.NoIndexesHeadline, callout.Headline);
        Assert.Equal(CalloutType.QuickTip, callout.Type);
    }
}
