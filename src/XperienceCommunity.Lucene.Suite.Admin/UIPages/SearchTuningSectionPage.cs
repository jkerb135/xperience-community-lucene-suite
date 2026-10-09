using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

[assembly: UIPage(
    parentType: typeof(SiteSearchIndexListingPage),
    slug: PageParameterConstants.PARAMETERIZED_SLUG,
    uiPageType: typeof(SearchTuningSectionPage),
    name: "Search tuning",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: UIPageOrder.NoOrder)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>The Search tuning section of one Lucene index; its children are the tuning pages.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SearchTuningSectionPage(IUIPermissionEvaluator permissionEvaluator, ILuceneIndexManager indexManager) : SecondaryMenuSectionPage
{
    internal const string AccessDeniedMessageKey = "base.application.permission.missing.label";
    internal const string IndexMissingMessageKey = "base.forms.error.objectnotinitialized";

    /// <summary>Identifier of the tuned Lucene index (the index listing's row identifier).</summary>
    [PageParameter(typeof(IntPageModelBinder))]
    public int IndexIdentifier { get; set; }

    /// <inheritdoc />
    public override Task<PageValidationResult> ValidatePage() => Validate(permissionEvaluator, indexManager, IndexIdentifier);

    /// <inheritdoc />
    public override async Task<TemplateClientProperties> ConfigureTemplateProperties(TemplateClientProperties properties)
    {
        string indexName = indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;
        properties.Breadcrumbs.Label = indexName;
        properties.Breadcrumbs.IsSignificant = true;
        properties.Navigation.Headline = indexName;
        properties.Navigation.IsSignificant = true;
        return await base.ConfigureTemplateProperties(properties);
    }

    /// <summary>Site Search View for the current user (the current application is Site Search), then the index's existence.</summary>
    internal static async Task<PageValidationResult> Validate(IUIPermissionEvaluator permissionEvaluator, ILuceneIndexManager indexManager, int indexIdentifier)
    {
        if (!(await permissionEvaluator.Evaluate(SystemPermissions.VIEW)).Succeeded)
        {
            return new PageValidationResult { IsValid = false, ErrorMessageKey = AccessDeniedMessageKey };
        }

        return new PageValidationResult
        {
            IsValid = indexManager.GetIndex(indexIdentifier) is not null,
            ErrorMessageKey = IndexMissingMessageKey,
        };
    }
}
