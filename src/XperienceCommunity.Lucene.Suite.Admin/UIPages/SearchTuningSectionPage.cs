using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Admin;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

[assembly: UIPage(
    parentType: typeof(IndexListingPage),
    slug: SearchTuningRootPage.SLUG,
    uiPageType: typeof(SearchTuningRootPage),
    name: "Search tuning",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: UIPageOrder.NoOrder)]

[assembly: UIPage(
    parentType: typeof(SearchTuningRootPage),
    slug: PageParameterConstants.PARAMETERIZED_SLUG,
    uiPageType: typeof(SearchTuningSectionPage),
    name: "Search tuning",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: UIPageOrder.NoOrder)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Static URL segment under the index listing that holds the per-index Search tuning sections.</summary>
[UIBreadcrumbs(false)]
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SearchTuningRootPage : SecondaryMenuSectionPage
{
    internal const string SLUG = "search-tuning";
}

/// <summary>The Search tuning section of one Lucene index; its children are the tuning pages.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SearchTuningSectionPage(ILuceneIndexManager indexManager) : SecondaryMenuSectionPage
{
    /// <summary>Identifier of the tuned Lucene index (the index listing's row identifier).</summary>
    [PageParameter(typeof(IntPageModelBinder))]
    public int IndexIdentifier { get; set; }

    /// <inheritdoc />
    public override Task<PageValidationResult> ValidatePage() => Task.FromResult(IndexExists(indexManager, IndexIdentifier));

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

    internal static PageValidationResult IndexExists(ILuceneIndexManager indexManager, int indexIdentifier) => new()
    {
        IsValid = indexManager.GetIndex(indexIdentifier) is not null,
        ErrorMessageKey = "base.forms.error.objectnotinitialized",
    };
}
