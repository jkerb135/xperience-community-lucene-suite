using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Forms;
using Kentico.Xperience.Admin.Base.Forms.Internal;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Synonyms;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Synonyms;

[assembly: UIPage(
    parentType: typeof(SynonymListingPage),
    slug: "create",
    uiPageType: typeof(SynonymCreatePage),
    name: "Create synonym",
    templateName: TemplateNames.EDIT,
    order: UIPageOrder.NoOrder)]

[assembly: UIPage(
    parentType: typeof(SynonymListingPage),
    slug: PageParameterConstants.PARAMETERIZED_SLUG,
    uiPageType: typeof(SynonymEditPage),
    name: "Edit synonym",
    templateName: TemplateNames.EDIT,
    order: UIPageOrder.NoOrder)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Creates a synonym in the section's index.</summary>
[UIEvaluatePermission(SiteSearchPermissions.MANAGE_SYNONYMS)]
public sealed class SynonymCreatePage(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<SynonymInfo> synonyms,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator)
    : SynonymEditPageBase(formItemCollectionProvider, formDataBinder, indexManager, synonyms, permissionEvaluator, pageLinkGenerator)
{
    /// <inheritdoc />
    protected override int? EditedSynonymId => null;
}

/// <summary>Edits one synonym of the section's index; read-only without Manage synonyms.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SynonymEditPage(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<SynonymInfo> synonyms,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator)
    : SynonymEditPageBase(formItemCollectionProvider, formDataBinder, indexManager, synonyms, permissionEvaluator, pageLinkGenerator)
{
    /// <summary>Identifier of the edited synonym.</summary>
    [PageParameter(typeof(IntPageModelBinder))]
    public int SynonymId { get; set; }

    /// <inheritdoc />
    protected override int? EditedSynonymId => SynonymId;
}

/// <summary>Shared create/edit logic: permission gates, index ownership, server validation and concurrency.</summary>
public abstract class SynonymEditPageBase(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<SynonymInfo> synonyms,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator)
    : ModelEditPage<SynonymEditModel>(formItemCollectionProvider, formDataBinder)
{
    internal const string ConcurrencyMessage = "This synonym was changed by someone else. Reload to see the changes.";
    internal const string MissingMessage = "The synonym no longer exists in this index.";

    private SynonymEditModel? model;

    /// <summary>Identifier of the index, bound from the section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <summary>The edited synonym, or null on the create page.</summary>
    protected abstract int? EditedSynonymId { get; }

    /// <inheritdoc />
    protected override SynonymEditModel Model =>
        model ??= EditedSynonymId is { } id && FindSynonym(id) is { } synonym ? SynonymEditMapping.FromSynonym(StoredSynonym.From(synonym)) : new SynonymEditModel();

    /// <summary>The index must exist and, when editing, the synonym must belong to it.</summary>
    public override Task<PageValidationResult> ValidatePage()
    {
        var result = SearchTuningSectionPage.Validate(indexManager, IndexIdentifier);
        if (result.IsValid && EditedSynonymId is { } id && FindSynonym(id) is null)
        {
            result = new PageValidationResult { IsValid = false, ErrorMessageKey = SearchTuningSectionPage.IndexMissingMessageKey };
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        if (!(await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_SYNONYMS)).Succeeded)
        {
            PageConfiguration.EditMode = FormEditMode.ReadOnly;
            PageConfiguration.SubmitConfiguration.Visible = false;
        }

        await base.ConfigurePage();
    }

    /// <summary>Saving needs Manage synonyms; the base command would require the system Update permission, which Site search does not declare.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public override Task<ICommandResponse> Submit(FormSubmissionCommandArguments args) => base.Submit(args);

    /// <summary>Form change round trips (Kind switches the Input field) need Manage synonyms, like saving.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public override Task<ICommandResponse<FormChangeResult>> Change(FormChangeCommandArguments args) => base.Change(args);

    /// <inheritdoc />
    protected override async Task<ICommandResponse> ProcessFormData(SynonymEditModel model, ICollection<IFormItem> formItems)
    {
        string indexName = indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;

        // Re-read the row for the ownership and concurrency checks.
        var synonym = EditedSynonymId is { } id ? FindSynonym(id) : new SynonymInfo { IndexName = indexName };
        if (synonym is null)
        {
            return await Failure(formItems, MissingMessage);
        }

        var stored = EditedSynonymId is null ? StoredSynonym.New : StoredSynonym.From(synonym);
        if (EditedSynonymId is not null && SynonymEditMapping.IsStale(model.LoadedVersion, stored))
        {
            return await Failure(formItems, ConcurrencyMessage);
        }

        var parsed = SynonymEditMapping.Validate(model.Kind, model.Input, model.Terms, stored.SynonymId, indexName);
        if (parsed.Definition is not { } definition)
        {
            return await Failure(formItems, string.Join(" ", parsed.Errors.Select(error => $"{error.Path}: {error.Message}")));
        }

        SynonymEditMapping.Apply(definition, model.Enabled, stored).CopyTo(synonym);
        synonyms.Set(synonym);

        if (EditedSynonymId is null)
        {
            string url = pageLinkGenerator.GetPath<SynonymEditPage>(new PageParameterValues
            {
                { typeof(SearchTuningSectionPage), IndexIdentifier },
                { typeof(SynonymEditPage), synonym.SynonymID },
            });
            return NavigateTo(url).AddSuccessMessage("Synonym created.");
        }

        // The next save compares against the version just written.
        model.LoadedVersion = SynonymEditMapping.VersionOf(StoredSynonym.From(synonym));
        var items = await formItems.OnlyVisible().GetClientProperties();
        return ResponseFrom(new FormSubmissionResult(FormSubmissionStatus.ValidationSuccess) { Items = items })
            .AddSuccessMessage("Synonym saved.");
    }

    private SynonymInfo? FindSynonym(int synonymId) =>
        SynonymOwnership.Find(synonyms, synonymId, indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty);

    private async Task<ICommandResponse> Failure(ICollection<IFormItem> formItems, string message) =>
        ResponseFrom(new FormSubmissionResult(FormSubmissionStatus.ValidationFailure) { Items = await formItems.OnlyVisible().GetClientProperties() })
            .AddErrorMessage(message);
}
