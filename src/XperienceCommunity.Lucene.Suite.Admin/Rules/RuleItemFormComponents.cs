using System.Text.Json;
using System.Text.Json.Nodes;

using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.FormAnnotations;
using Kentico.Xperience.Admin.Base.Forms;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>Client properties of the pins and hides components.</summary>
public sealed class RuleItemsClientProperties : FormComponentClientProperties<string>
{
    /// <summary>Most pins per rule.</summary>
    public int MaxPins { get; set; } = RuleLimits.Pins;

    /// <summary>Highest pin position.</summary>
    public int MaxPosition { get; set; } = RuleLimits.MaxPosition;

    /// <summary>Most hides per rule.</summary>
    public int MaxHides { get; set; } = RuleLimits.Hides;
}

/// <summary>An indexed item as the picker and the rows show it.</summary>
/// <param name="ItemGuid">The ItemGuid stored in the index.</param>
/// <param name="Title">Stored title, or the ItemGuid.</param>
/// <param name="ContentType">Stored content type name, when the index stores one.</param>
public sealed record RuleItem(Guid ItemGuid, string Title, string? ContentType);

/// <summary>Result of the Search and Describe commands.</summary>
/// <param name="Items">Found items.</param>
/// <param name="Error">Why nothing was searched, or null.</param>
public sealed record RuleItemsResult(IReadOnlyList<RuleItem> Items, string? Error);

/// <summary>Arguments of the Search command.</summary>
public sealed class RuleItemSearchArguments
{
    /// <summary>Query text; empty lists the first documents.</summary>
    public string? Query { get; set; }
}

/// <summary>Arguments of the Describe command.</summary>
public sealed class RuleItemDescribeArguments
{
    /// <summary>ItemGuids to resolve.</summary>
    public List<Guid>? Items { get; set; }
}

/// <summary>Shared commands of the pins and hides components: search this index for items, and resolve stored ItemGuids to titles.</summary>
public abstract class RuleItemsFormComponent(
    ILocalizationService localizationService,
    ILuceneIndexManager indexManager,
    ISuiteSearchService searchService,
    IServiceProvider services)
    : FormComponent<FormComponentProperties, RuleItemsClientProperties, string>(localizationService)
{
    internal const int SearchPageSize = 20;
    internal const string IndexMissingMessage = "This index no longer exists.";

    /// <summary>The page's bound index; set by the rule edit pages on every request, never by the client.</summary>
    internal int IndexIdentifier { get; set; }

    /// <summary>Searches this index with rules off and without analytics; each ItemGuid is listed once.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleItemsResult>> Search(RuleItemSearchArguments arguments)
    {
        if (IndexName() is not { } indexName)
        {
            return Error(IndexMissingMessage);
        }

        try
        {
            var response = searchService.Search(new SuiteSearchRequest
            {
                IndexName = indexName,
                Query = arguments.Query,
                PageSize = SearchPageSize,
                ApplyRules = false,
                TrackAnalytics = false,
            });
            return Items(SuiteItemLookup.FromHits(response.Hits, Lookup.TitleField(indexName)));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Error(QueryTesterPage.ValidationMessage(exception));
        }
    }

    /// <summary>Resolves ItemGuids to their stored titles; items no longer in the index are left out.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleItemsResult>> Describe(RuleItemDescribeArguments arguments)
    {
        var guids = (arguments.Items ?? []).Distinct().ToList();
        if (guids.Count > SuiteItemLookup.MaxItems)
        {
            return Error($"At most {SuiteItemLookup.MaxItems} items can be shown.");
        }

        if (IndexName() is not { } indexName)
        {
            return Error(IndexMissingMessage);
        }

        try
        {
            return Items(Lookup.Describe(indexName, guids));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Error(QueryTesterPage.ValidationMessage(exception));
        }
    }

    // SuiteItemLookup is internal to the package, so it cannot appear in this public constructor.
    private SuiteItemLookup Lookup => services.GetRequiredService<SuiteItemLookup>();

    private string? IndexName() => IndexIdentifier > 0 ? indexManager.GetIndex(IndexIdentifier)?.IndexName : null;

    private Task<ICommandResponse<RuleItemsResult>> Items(IReadOnlyList<SuiteItem> items) =>
        Task.FromResult(ResponseFrom(new RuleItemsResult([.. items.Select(item => new RuleItem(item.ItemGuid, item.Title, item.ContentType))], null)));

    private Task<ICommandResponse<RuleItemsResult>> Error(string message) =>
        Task.FromResult(ResponseFrom(new RuleItemsResult([], message)));
}

/// <summary>Editor of a rule's pinned items.</summary>
[ComponentAttribute(typeof(RulePinsComponentAttribute))]
public sealed class RulePinsFormComponent(ILocalizationService localizationService, ILuceneIndexManager indexManager, ISuiteSearchService searchService, IServiceProvider services)
    : RuleItemsFormComponent(localizationService, indexManager, searchService, services)
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RulePins";
}

/// <summary>Editor of a rule's hidden items.</summary>
[ComponentAttribute(typeof(RuleHidesComponentAttribute))]
public sealed class RuleHidesFormComponent(ILocalizationService localizationService, ILuceneIndexManager indexManager, ISuiteSearchService searchService, IServiceProvider services)
    : RuleItemsFormComponent(localizationService, indexManager, searchService, services)
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RuleHides";
}

/// <summary>Edits the property with <see cref="RulePinsFormComponent"/>.</summary>
public sealed class RulePinsComponentAttribute : FormComponentAttribute;

/// <summary>Edits the property with <see cref="RuleHidesFormComponent"/>.</summary>
public sealed class RuleHidesComponentAttribute : FormComponentAttribute;

/// <summary>Refuses items that are both pinned and hidden, on the Hidden items field.</summary>
[ValidationRuleAttribute(typeof(RuleHidesConflictValidationRuleAttribute))]
public sealed class RuleHidesConflictValidationRule : ValidationRule<RuleHidesConflictValidationRuleProperties, ServerValidatedClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientRuleName => RuleJsonValidationRule.ServerOnlyClientRule;

    /// <inheritdoc />
    public override Task<ValidationResult> Validate(string value, IFormFieldValueProvider formFieldValueProvider)
    {
        formFieldValueProvider.TryGet<string>(nameof(RuleEditModel.Pins), out var pins);
        return Conflict(value, pins) is { } message ? ValidationResult.FailResult(message) : ValidationResult.SuccessResult();
    }

    /// <summary>The error for hidden items that are also pinned, or null. Malformed JSON is left to <see cref="RuleJsonValidationRule"/>.</summary>
    internal static string? Conflict(string? hidesJson, string? pinsJson)
    {
        var hides = Guids(hidesJson, node => node);
        var pinned = Guids(pinsJson, node => node?["itemGuid"]).ToHashSet();
        var both = hides.Where(pinned.Contains).Distinct().ToList();
        return both.Count == 0 ? null : $"These items are also pinned: {string.Join(", ", both)}. Remove them from one list.";
    }

    private static List<Guid> Guids(string? json, Func<JsonNode?, JsonNode?> select)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) || JsonNode.Parse(json) is not JsonArray array
                ? []
                : [.. array.Select(node => select(node) is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParse(text, out var guid) ? guid : Guid.Empty)
                    .Where(guid => guid != Guid.Empty)];
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return [];
        }
    }
}

/// <summary>Properties of <see cref="RuleHidesConflictValidationRule"/>.</summary>
public sealed class RuleHidesConflictValidationRuleProperties : ValidationRuleProperties
{
    /// <inheritdoc />
    public override string GetDescriptionText(ILocalizationService localizationService) => "No item is both pinned and hidden";
}

/// <summary>Applies <see cref="RuleHidesConflictValidationRule"/> to a model property.</summary>
public sealed class RuleHidesConflictValidationRuleAttribute : ValidationRuleAttribute;
