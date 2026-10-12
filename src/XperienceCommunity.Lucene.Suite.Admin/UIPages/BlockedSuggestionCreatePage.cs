using CMS.DataEngine;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Forms;
using Kentico.Xperience.Admin.Base.Forms.Internal;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Suggestions;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Suggestions;

[assembly: UIPage(
    parentType: typeof(BlockedSuggestionListingPage),
    slug: "create",
    uiPageType: typeof(BlockedSuggestionCreatePage),
    name: "Block suggestion",
    templateName: TemplateNames.EDIT,
    order: UIPageOrder.NoOrder)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Adds a blocklist entry to the section's index.</summary>
[UIEvaluatePermission(SiteSearchPermissions.MANAGE_SYNONYMS)]
public sealed class BlockedSuggestionCreatePage(
    IFormItemCollectionProvider formItemCollectionProvider,
    IFormDataBinder formDataBinder,
    ILuceneIndexManager indexManager,
    IInfoProvider<BlockedSuggestionInfo> blockedSuggestions,
    IPageLinkGenerator pageLinkGenerator)
    : ModelEditPage<BlockedSuggestionEditModel>(formItemCollectionProvider, formDataBinder)
{
    private BlockedSuggestionEditModel? model;

    /// <summary>Identifier of the index, bound from the section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <inheritdoc />
    protected override BlockedSuggestionEditModel Model => model ??= new BlockedSuggestionEditModel();

    /// <summary>Repeats the section's index check (Kentico validates only the requested page).</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <summary>Saving needs Manage synonyms; the base command would require the system Update permission, which Site search does not declare.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public override Task<ICommandResponse> Submit(FormSubmissionCommandArguments args) => base.Submit(args);

    /// <summary>Form change round trips need Manage synonyms, like saving.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public override Task<ICommandResponse<FormChangeResult>> Change(FormChangeCommandArguments args) => base.Change(args);

    /// <inheritdoc />
    protected override async Task<ICommandResponse> ProcessFormData(BlockedSuggestionEditModel model, ICollection<IFormItem> formItems)
    {
        string indexName = indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;
        var (entry, error) = BlockedSuggestionValidation.Validate(model.Text, model.MatchMode);
        if (indexName.Length == 0)
        {
            error = BlockedSuggestionValidation.IndexMissingMessage;
        }
        else if (entry is not null && BlockedSuggestionValidation.Exists(blockedSuggestions, indexName, entry.MatchKey, entry.MatchMode))
        {
            error = BlockedSuggestionValidation.DuplicateMessage;
        }

        if (entry is null || error is not null)
        {
            return ResponseFrom(new FormSubmissionResult(FormSubmissionStatus.ValidationFailure) { Items = await formItems.OnlyVisible().GetClientProperties() })
                .AddErrorMessage(error!);
        }

        BlockedSuggestionValidation.Add(blockedSuggestions, indexName, entry);
        string url = pageLinkGenerator.GetPath<BlockedSuggestionListingPage>(new PageParameterValues { { typeof(SearchTuningSectionPage), IndexIdentifier } });
        return NavigateTo(url).AddSuccessMessage("Suggestion blocked.");
    }
}
