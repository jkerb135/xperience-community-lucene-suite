using System.Globalization;

using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Suggestions;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "blocked-suggestions",
    uiPageType: typeof(BlockedSuggestionListingPage),
    name: "Blocked suggestions",
    templateName: TemplateNames.LISTING,
    order: 400,
    Icon = Icons.BanSign)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>The texts kept out of one Lucene index's suggestions.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class BlockedSuggestionListingPage(
    ILuceneIndexManager indexManager,
    IInfoProvider<BlockedSuggestionInfo> blockedSuggestions,
    IUIPermissionEvaluator permissionEvaluator) : ListingPage
{
    /// <summary>Identifier of the index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <inheritdoc />
    protected override string ObjectType => BlockedSuggestionInfo.OBJECT_TYPE;

    /// <summary>Repeats the section's index check (Kentico validates only the requested page).</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        string indexName = IndexName();
        bool canManage = (await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_SYNONYMS)).Succeeded;

        PageConfiguration.ColumnConfigurations
            .AddColumn(nameof(BlockedSuggestionInfo.Text), "Text", searchable: true, maxWidth: 60)
            .AddColumn(nameof(BlockedSuggestionInfo.MatchMode), "Match", formatter: (value, _) => MatchLabel(value as string))
            .AddColumn(nameof(BlockedSuggestionInfo.LastModified), "Last modified (UTC)", defaultSortDirection: SortTypeEnum.Desc, formatter: (value, _) => Date(value));

        PageConfiguration.QueryModifiers.AddModifier((query, _) => query.WhereEquals(nameof(BlockedSuggestionInfo.IndexName), indexName));

        // Shown only to roles that may use them; the commands check the permission again on the server.
        if (canManage)
        {
            var section = new PageParameterValues { { typeof(SearchTuningSectionPage), IndexIdentifier } };
            PageConfiguration.HeaderActions.AddLink<BlockedSuggestionCreatePage>("Block suggestion", Icons.Plus, parameters: section);
            PageConfiguration.TableActions.AddDeleteAction(nameof(Delete));
        }

        await base.ConfigurePage();
    }

    /// <summary>Deletes an entry of this index; an entry of another index is refused.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public override async Task<ICommandResponse<RowActionResult>> Delete(int id)
    {
        string indexName = IndexName();
        bool owned = indexName.Length > 0 && blockedSuggestions.Get()
            .WhereEquals(nameof(BlockedSuggestionInfo.BlockedSuggestionID), id)
            .WhereEquals(nameof(BlockedSuggestionInfo.IndexName), indexName)
            .TopN(1)
            .Column(nameof(BlockedSuggestionInfo.BlockedSuggestionID))
            .Any();
        if (!owned)
        {
            return ResponseFrom(new RowActionResult(false)).AddErrorMessage("The blocked suggestion does not exist in this index.");
        }

        return await base.Delete(id);
    }

    /// <summary>The engine treats every mode other than "word" as exact, so the listing does too.</summary>
    internal static string MatchLabel(string? mode) =>
        string.Equals(mode, BlockedSuggestionInfo.MatchModeWord, StringComparison.OrdinalIgnoreCase) ? "Contains words" : "Exact query";

    private string IndexName() => indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;

    private static string Date(object? value) =>
        value is DateTime date ? date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;
}
