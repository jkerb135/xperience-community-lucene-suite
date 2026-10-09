using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;

[assembly: UIApplication(
    identifier: SiteSearchApplication.IDENTIFIER,
    type: typeof(SiteSearchApplication),
    slug: SiteSearchApplication.SLUG,
    name: "Site search",
    category: BaseApplicationCategories.DIGITAL_MARKETING,
    icon: Icons.Magnifier,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Root of the Site search application; owns the permissions every Suite admin page evaluates.</summary>
[UIPermission(SystemPermissions.VIEW)]
[UIPermission(SiteSearchPermissions.MANAGE_RULES, SiteSearchPermissions.MANAGE_RULES_DISPLAY_NAME)]
[UIPermission(SiteSearchPermissions.MANAGE_SYNONYMS, SiteSearchPermissions.MANAGE_SYNONYMS_DISPLAY_NAME)]
[UIPermission(SiteSearchPermissions.MANAGE_EXPERIMENTS, SiteSearchPermissions.MANAGE_EXPERIMENTS_DISPLAY_NAME)]
public sealed class SiteSearchApplication : ApplicationPage
{
    /// <summary>Application identifier; also the application name that role permissions are stored under.</summary>
    public const string IDENTIFIER = "XperienceCommunity.Lucene.Suite.SiteSearch";

    internal const string SLUG = "site-search";
}
