using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Forms;
using Kentico.Xperience.Admin.Base.Forms.Internal;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;

[assembly: UIPage(
    parentType: typeof(RuleListingPage),
    slug: "create",
    uiPageType: typeof(RuleCreatePage),
    name: "Create rule",
    templateName: TemplateNames.EDIT,
    order: UIPageOrder.NoOrder)]

[assembly: UIPage(
    parentType: typeof(RuleListingPage),
    slug: PageParameterConstants.PARAMETERIZED_SLUG,
    uiPageType: typeof(RuleEditPage),
    name: "Edit rule",
    templateName: TemplateNames.EDIT,
    order: UIPageOrder.NoOrder)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Creates a rule in the section's index.</summary>
[UIEvaluatePermission(SiteSearchPermissions.MANAGE_RULES)]
public sealed class RuleCreatePage(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<RuleInfo> rules,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator)
    : RuleEditPageBase(formItemCollectionProvider, formDataBinder, indexManager, rules, permissionEvaluator, pageLinkGenerator)
{
    /// <inheritdoc />
    protected override int? EditedRuleId => null;
}

/// <summary>Edits one rule of the section's index; read-only without Manage rules.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class RuleEditPage(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<RuleInfo> rules,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator)
    : RuleEditPageBase(formItemCollectionProvider, formDataBinder, indexManager, rules, permissionEvaluator, pageLinkGenerator)
{
    /// <summary>Identifier of the edited rule.</summary>
    [PageParameter(typeof(IntPageModelBinder))]
    public int RuleId { get; set; }

    /// <inheritdoc />
    protected override int? EditedRuleId => RuleId;
}

/// <summary>Shared create/edit logic: permission gates, index ownership, server validation and concurrency.</summary>
public abstract class RuleEditPageBase(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<RuleInfo> rules,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator)
    : ModelEditPage<RuleEditModel>(formItemCollectionProvider, formDataBinder)
{
    internal const string ConcurrencyMessage = "This rule was changed by someone else. Reload to see the changes.";
    internal const string MissingMessage = "The rule no longer exists in this index.";

    private RuleEditModel? model;

    /// <summary>Identifier of the index, bound from the section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <summary>The edited rule, or null on the create page.</summary>
    protected abstract int? EditedRuleId { get; }

    /// <inheritdoc />
    protected override RuleEditModel Model =>
        model ??= EditedRuleId is { } id && FindRule(id) is { } rule ? RuleEditMapping.FromRule(StoredRule.From(rule)) : new RuleEditModel();

    /// <summary>The index must exist and, when editing, the rule must belong to it.</summary>
    public override Task<PageValidationResult> ValidatePage()
    {
        var result = SearchTuningSectionPage.Validate(indexManager, IndexIdentifier);
        if (result.IsValid && EditedRuleId is { } id && FindRule(id) is null)
        {
            result = new PageValidationResult { IsValid = false, ErrorMessageKey = SearchTuningSectionPage.IndexMissingMessageKey };
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        if (!(await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_RULES)).Succeeded)
        {
            PageConfiguration.EditMode = FormEditMode.ReadOnly;
            PageConfiguration.SubmitConfiguration.Visible = false;
        }

        await base.ConfigurePage();
    }

    /// <summary>Gives the conditions, pins and hides components this page's index; Kentico also builds the items this way for component commands.</summary>
    protected override async Task<ICollection<IFormItem>> GetFormItems()
    {
        var items = await base.GetFormItems();
        foreach (var component in items.OfType<RuleItemsFormComponent>())
        {
            component.IndexIdentifier = IndexIdentifier;
        }

        foreach (var component in items.OfType<RuleConditionsFormComponent>())
        {
            component.IndexIdentifier = IndexIdentifier;
        }

        return items;
    }

    /// <summary>Saving needs Manage rules; the base command would require the system Update permission, which Site search does not declare.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public override Task<ICommandResponse> Submit(FormSubmissionCommandArguments args) => base.Submit(args);

    /// <summary>Form change round trips need Manage rules, like saving.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public override Task<ICommandResponse<FormChangeResult>> Change(FormChangeCommandArguments args) => base.Change(args);

    /// <inheritdoc />
    protected override async Task<ICommandResponse> ProcessFormData(RuleEditModel model, ICollection<IFormItem> formItems)
    {
        string indexName = indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;

        // Re-read the row for the ownership and concurrency checks.
        var rule = EditedRuleId is { } id ? FindRule(id) : new RuleInfo { IndexName = indexName };
        if (rule is null)
        {
            return await Failure(formItems, MissingMessage);
        }

        var stored = EditedRuleId is null ? StoredRule.New : StoredRule.From(rule);
        if (EditedRuleId is not null && RuleEditMapping.IsStale(model.LoadedVersion, stored))
        {
            formItems.OfType<RuleStatusFormComponent>().Single().Conflict = true;
            return await Failure(formItems, ConcurrencyMessage);
        }

        var parsed = RuleEditMapping.Validate(model, stored, indexName);
        if (parsed.Definition is not { } definition)
        {
            return await Failure(formItems, string.Join(" ", parsed.Errors.Select(error => $"{error.Path}: {error.Message}")));
        }

        RuleEditMapping.Apply(model, definition, stored).CopyTo(rule);
        rules.Set(rule);

        // Kentico's client ignores the items of a successful submit, so an edit reloads the page (refetched even at the same URL) to get the new version token.
        string url = pageLinkGenerator.GetPath<RuleEditPage>(new PageParameterValues
        {
            { typeof(SearchTuningSectionPage), IndexIdentifier },
            { typeof(RuleEditPage), rule.RuleID },
        });
        return NavigateTo(url, refetchAllTemplates: EditedRuleId is not null).AddSuccessMessage(EditedRuleId is null ? "Rule created." : "Rule saved.");
    }

    private RuleInfo? FindRule(int ruleId) =>
        RuleOwnership.Find(rules, ruleId, indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty);

    private async Task<ICommandResponse> Failure(ICollection<IFormItem> formItems, string message) =>
        ResponseFrom(new FormSubmissionResult(FormSubmissionStatus.ValidationFailure) { Items = await formItems.OnlyVisible().GetClientProperties() })
            .AddErrorMessage(message);
}
