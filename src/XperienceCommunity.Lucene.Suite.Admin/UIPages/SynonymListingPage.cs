using System.Globalization;

using CMS.Base;
using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Synonyms;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Synonyms;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "synonyms",
    uiPageType: typeof(SynonymListingPage),
    name: "Synonyms",
    templateName: TemplateNames.LISTING,
    order: 300,
    Icon = Icons.ArrowsH)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>The synonyms of one Lucene index.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SynonymListingPage(
    ILuceneIndexManager indexManager,
    IInfoProvider<SynonymInfo> synonyms,
    IUIPermissionEvaluator permissionEvaluator) : ListingPage
{
    /// <summary>Identifier of the index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <inheritdoc />
    protected override string ObjectType => SynonymInfo.OBJECT_TYPE;

    /// <summary>Repeats the section's index check (Kentico validates only the requested page).</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        string indexName = IndexName();
        bool canManage = (await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_SYNONYMS)).Succeeded;

        PageConfiguration.ColumnConfigurations
            .AddColumn(nameof(SynonymInfo.Terms), "Terms", searchable: true, sortable: false, maxWidth: 60, formatter: (_, row) => Summary(row))
            .AddColumn(nameof(SynonymInfo.Input), "Input term", visible: false, searchable: true)
            .AddColumn(nameof(SynonymInfo.Kind), "Kind", formatter: (value, _) => SynonymSummaries.Kind(value as string))
            .AddColumn(nameof(SynonymInfo.Enabled), "Enabled", formatter: (value, _) => value is true ? "Yes" : "No")
            .AddColumn(nameof(SynonymInfo.LastModified), "Last modified (UTC)", defaultSortDirection: SortTypeEnum.Desc, formatter: (value, _) => Date(value));

        PageConfiguration.QueryModifiers.AddModifier((query, _) => query.WhereEquals(nameof(SynonymInfo.IndexName), indexName));
        // Links to child pages must carry the section's index parameter; Kentico does not infer ancestors.
        var section = new PageParameterValues { { typeof(SearchTuningSectionPage), IndexIdentifier } };
        PageConfiguration.AddEditRowAction<SynonymEditPage>(parameters: section);

        // Shown only to roles that may use them; the commands check the permission again on the server.
        if (canManage)
        {
            PageConfiguration.HeaderActions.AddLink<SynonymCreatePage>("Create synonym", Icons.Plus, parameters: section);
            PageConfiguration.TableActions.AddDeleteAction(nameof(Delete));
        }

        await base.ConfigurePage();
    }

    /// <summary>Deletes a synonym of this index; a synonym of another index is refused.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public override async Task<ICommandResponse<RowActionResult>> Delete(int id)
    {
        if (SynonymOwnership.Find(synonyms, id, IndexName()) is null)
        {
            return ResponseFrom(new RowActionResult(false)).AddErrorMessage("The synonym does not exist in this index.");
        }

        return await base.Delete(id);
    }

    private string IndexName() => indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;

    private static string Summary(IDataContainer row) =>
        SynonymSummaries.Terms(row.GetValue(nameof(SynonymInfo.Kind)) as string, row.GetValue(nameof(SynonymInfo.Input)) as string, row.GetValue(nameof(SynonymInfo.Terms)) as string);

    private static string Date(object? value) =>
        value is DateTime date ? date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;
}

/// <summary>Loads a synonym only when it belongs to the given index.</summary>
internal static class SynonymOwnership
{
    public static SynonymInfo? Find(IInfoProvider<SynonymInfo> synonyms, int synonymId, string indexName)
    {
        var synonym = synonyms.Get().WhereEquals(nameof(SynonymInfo.SynonymID), synonymId).FirstOrDefault();
        return synonym is not null && indexName.Length > 0 && string.Equals(synonym.IndexName, indexName, StringComparison.OrdinalIgnoreCase)
            ? synonym
            : null;
    }
}
