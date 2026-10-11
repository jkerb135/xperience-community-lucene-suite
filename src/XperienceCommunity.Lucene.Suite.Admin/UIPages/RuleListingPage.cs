using System.Globalization;

using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "rules",
    uiPageType: typeof(RuleListingPage),
    name: "Rules",
    templateName: TemplateNames.LISTING,
    order: 200,
    Icon = Icons.ListBullets)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>The search rules of one Lucene index.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class RuleListingPage(
    ILuceneIndexManager indexManager,
    IInfoProvider<RuleInfo> rules,
    IUIPermissionEvaluator permissionEvaluator) : ListingPage
{
    /// <summary>Identifier of the index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <inheritdoc />
    protected override string ObjectType => RuleInfo.OBJECT_TYPE;

    /// <summary>Repeats the section's index check (Kentico validates only the requested page).</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        string indexName = IndexName();
        bool canManage = (await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_RULES)).Succeeded;

        PageConfiguration.ColumnConfigurations
            .AddColumn(nameof(RuleInfo.DisplayName), "Name", searchable: true, maxWidth: 40)
            .AddColumn(nameof(RuleInfo.Priority), "Priority", defaultSortDirection: SortTypeEnum.Asc)
            .AddColumn(nameof(RuleInfo.Enabled), "Enabled", formatter: (value, _) => value is true ? "Yes" : "No")
            .AddColumn(nameof(RuleInfo.ValidFromUtc), "Valid from (UTC)", formatter: (value, _) => Date(value))
            .AddColumn(nameof(RuleInfo.ValidUntilUtc), "Valid until (UTC)", formatter: (value, _) => Date(value))
            .AddColumn(nameof(RuleInfo.Conditions), "Conditions", sortable: false, formatter: (value, _) => RuleSummaries.Conditions(value as string))
            .AddColumn(nameof(RuleInfo.Actions), "Actions", sortable: false, formatter: (value, _) => RuleSummaries.Actions(value as string));

        PageConfiguration.QueryModifiers.AddModifier((query, _) => query.WhereEquals(nameof(RuleInfo.IndexName), indexName));
        // Links to child pages must carry the section's index parameter; Kentico does not infer ancestors.
        var section = new PageParameterValues { { typeof(SearchTuningSectionPage), IndexIdentifier } };
        PageConfiguration.AddEditRowAction<RuleEditPage>(parameters: section);

        // Shown only to roles that may use them; the commands check the permission again on the server.
        if (canManage)
        {
            PageConfiguration.HeaderActions.AddLink<RuleCreatePage>("Create rule", Icons.Plus, parameters: section);
            PageConfiguration.TableActions.AddDeleteAction(nameof(Delete));
        }

        await base.ConfigurePage();
    }

    /// <summary>Deletes a rule of this index; a rule of another index is refused.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public override async Task<ICommandResponse<RowActionResult>> Delete(int id)
    {
        if (RuleOwnership.Find(rules, id, IndexName()) is null)
        {
            return ResponseFrom(new RowActionResult(false)).AddErrorMessage("The rule does not exist in this index.");
        }

        return await base.Delete(id);
    }

    private string IndexName() => indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;

    private static string Date(object? value) =>
        value is DateTime date ? date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;
}

/// <summary>Loads a rule only when it belongs to the given index.</summary>
internal static class RuleOwnership
{
    public static RuleInfo? Find(IInfoProvider<RuleInfo> rules, int ruleId, string indexName)
    {
        var rule = rules.Get().WhereEquals(nameof(RuleInfo.RuleID), ruleId).FirstOrDefault();
        return rule is not null && indexName.Length > 0 && string.Equals(rule.IndexName, indexName, StringComparison.OrdinalIgnoreCase)
            ? rule
            : null;
    }
}
