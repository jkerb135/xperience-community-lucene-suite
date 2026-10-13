using System.Globalization;

using CMS.ContactManagement;
using CMS.Core;
using CMS.DataEngine;
using CMS.Membership.Internal;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Admin.Base.FormAnnotations;
using Kentico.Xperience.Admin.Base.Forms;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>Client properties shared by the rule JSON components: the limits the client enforces on "Add".</summary>
public sealed class RuleJsonClientProperties : FormComponentClientProperties<string>
{
    /// <summary>Most conditions per rule.</summary>
    public int MaxConditions { get; set; } = RuleLimits.Conditions;

    /// <summary>Most facet pairs per condition.</summary>
    public int MaxFacets { get; set; } = RuleLimits.Facets;

    /// <summary>Most contexts per condition.</summary>
    public int MaxContexts { get; set; } = RuleLimits.Contexts;

    /// <summary>Most boosts per rule.</summary>
    public int MaxBoosts { get; set; } = RuleLimits.Boosts;
}

/// <summary>Errors of a component's JSON, as returned by its Validate command.</summary>
/// <param name="Errors">Errors with JSON paths such as <c>conditions[1].facets[0].value</c>.</param>
public sealed record RuleJsonValidationResult(IReadOnlyList<RuleError> Errors);

/// <summary>Arguments of the Validate component command.</summary>
public sealed class RuleJsonValidationArguments
{
    /// <summary>The component's current JSON.</summary>
    public string? Value { get; set; }
}

/// <summary>Structured editor of a rule's conditions, with contact groups and the index's facet values to pick from.</summary>
[ComponentAttribute(typeof(RuleConditionsComponentAttribute))]
public sealed class RuleConditionsFormComponent(
    ILocalizationService localizationService,
    ILuceneIndexManager indexManager,
    ISuiteSearchService searchService,
    IInfoProvider<ContactGroupInfo> contactGroups,
    IAuthenticatedUserAccessor userAccessor,
    IApplicationPermissionEvaluator permissionEvaluator,
    IEventLogService eventLog)
    : FormComponent<FormComponentProperties, RuleJsonClientProperties, string>(localizationService)
{
    internal const string FacetValuesError = "Facet values could not be loaded.";

    /// <summary>The page's bound index; set by the rule edit pages on every request, never by the client.</summary>
    internal int IndexIdentifier { get; set; }

    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RuleConditions";

    /// <summary>Validates the conditions JSON with the server rules; changes nothing.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleJsonValidationResult>> Validate(RuleJsonValidationArguments arguments) =>
        Task.FromResult(ResponseFrom(new RuleJsonValidationResult(RuleSectionValidator.Validate(RuleJsonSection.Conditions, arguments.Value))));

    /// <summary>Contact groups offered as contexts; none unless the user may view Kentico's Contact groups application.</summary>
    [FormComponentCommand]
    public async Task<ICommandResponse<QueryTesterContactGroups>> ContactGroups() =>
        ResponseFrom(await ContactGroupAccess.Get(userAccessor, permissionEvaluator, contactGroups));

    /// <summary>The page's index's facet dimensions and values with their own counts; an error message instead of an exception.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleFacetValuesResult>> FacetValues()
    {
        string? indexName = IndexIdentifier > 0 ? indexManager.GetIndex(IndexIdentifier)?.IndexName : null;
        if (indexName is null)
        {
            return Task.FromResult(ResponseFrom(new RuleFacetValuesResult { Error = RuleItemsFormComponent.IndexMissingMessage }));
        }

        try
        {
            return Task.FromResult(ResponseFrom(new RuleFacetValuesResult { Facets = searchService.Search(FacetValuesRequest(indexName)).Facets }));
        }
        catch (Exception exception)
        {
            eventLog.LogException("LuceneSuite", "RULE_FACET_VALUES", exception);
            return Task.FromResult(ResponseFrom(new RuleFacetValuesResult { Error = FacetValuesError }));
        }
    }

    /// <summary>An empty query with rules, synonyms, contexts and analytics off, so the counts are the index's own.</summary>
    internal static SuiteSearchRequest FacetValuesRequest(string indexName) => new()
    {
        IndexName = indexName,
        Query = string.Empty,
        PageSize = 1,
        ApplyRules = false,
        ApplySynonyms = false,
        Contexts = [],
        TrackAnalytics = false,
    };
}

/// <summary>Result of the conditions component's FacetValues command.</summary>
public sealed class RuleFacetValuesResult
{
    /// <summary>Dimensions with their top values and counts; empty for a non-faceted index.</summary>
    public IReadOnlyList<SuiteFacet> Facets { get; init; } = [];

    /// <summary>Why nothing was loaded, or null.</summary>
    public string? Error { get; init; }
}

/// <summary>Display-only status of the edited rule, worked out on the server like the rules listing; never stored.</summary>
[ComponentAttribute(typeof(RuleStatusComponentAttribute))]
public sealed class RuleStatusFormComponent : FormComponent<RuleStatusClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RuleStatus";

    /// <summary>Set by the rule edit page when a save was refused because someone else changed the rule.</summary>
    internal bool Conflict { get; set; }

    /// <inheritdoc />
    protected override Task ConfigureClientProperties(RuleStatusClientProperties clientProperties)
    {
        clientProperties.Conflict = Conflict;
        return base.ConfigureClientProperties(clientProperties);
    }

    /// <summary>The status of the form's current (unsaved) Enabled and validity values, now.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleStatusResult>> Status(RuleStatusArguments arguments) =>
        Task.FromResult(ResponseFrom(Describe(arguments.Enabled, ParseUtc(arguments.ValidFromUtc), ParseUtc(arguments.ValidUntilUtc), DateTime.UtcNow)));

    internal static RuleStatusResult Describe(bool enabled, DateTime? fromUtc, DateTime? untilUtc, DateTime nowUtc)
    {
        var status = RuleListing.Of(enabled, fromUtc, untilUtc, nowUtc);
        return new RuleStatusResult
        {
            Status = status.ToString(),
            Detail = status switch
            {
                RuleStatus.Active => "fires on matching searches",
                RuleStatus.Scheduled => $"starts {RuleListing.Date(fromUtc)} UTC",
                RuleStatus.Expired => $"ended {RuleListing.Date(untilUtc)} UTC",
                _ => null,
            },
        };
    }

    /// <summary>A form date as a UTC instant: without an offset it is already UTC (as the editor stores it); empty or unreadable is no date.</summary>
    internal static DateTime? ParseUtc(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
}

/// <summary>Arguments of the Status command: the form's current values as the client holds them.</summary>
public sealed class RuleStatusArguments
{
    /// <summary>Whether the rule is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Valid from as an ISO 8601 string; empty for none.</summary>
    public string? ValidFromUtc { get; set; }

    /// <summary>Valid until as an ISO 8601 string; empty for none.</summary>
    public string? ValidUntilUtc { get; set; }
}

/// <summary>Result of the Status command.</summary>
public sealed class RuleStatusResult
{
    /// <summary>Active, Scheduled, Expired or Disabled.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>What the status means for this rule, or null.</summary>
    public string? Detail { get; init; }
}

/// <summary>Structured editor of a rule's boosts and buries.</summary>
[ComponentAttribute(typeof(RuleBoostsComponentAttribute))]
public sealed class RuleBoostsFormComponent : FormComponent<RuleJsonClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RuleBoosts";

    /// <summary>Validates the boosts JSON with the server rules; changes nothing.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleJsonValidationResult>> Validate(RuleJsonValidationArguments arguments) =>
        Task.FromResult(ResponseFrom(new RuleJsonValidationResult(RuleSectionValidator.Validate(RuleJsonSection.Boosts, arguments.Value))));
}

/// <summary>Carries a value through the form without rendering anything.</summary>
[ComponentAttribute(typeof(RuleHiddenComponentAttribute))]
public sealed class RuleHiddenFormComponent : FormComponent<RuleHiddenClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RuleHidden";
}

/// <summary>Client properties of <see cref="RuleHiddenFormComponent"/>.</summary>
public sealed class RuleHiddenClientProperties : FormComponentClientProperties<string>;

/// <summary>Client properties of <see cref="RuleStatusFormComponent"/>.</summary>
public sealed class RuleStatusClientProperties : FormComponentClientProperties<string>
{
    /// <summary>Whether the last save was refused because someone else changed the rule.</summary>
    public bool Conflict { get; set; }
}

/// <summary>Edits the property with <see cref="RuleConditionsFormComponent"/>.</summary>
public sealed class RuleConditionsComponentAttribute : FormComponentAttribute;

/// <summary>Edits the property with <see cref="RuleBoostsFormComponent"/>.</summary>
public sealed class RuleBoostsComponentAttribute : FormComponentAttribute;

/// <summary>Carries the property with <see cref="RuleHiddenFormComponent"/>.</summary>
public sealed class RuleHiddenComponentAttribute : FormComponentAttribute;

/// <summary>Shows the property with <see cref="RuleStatusFormComponent"/>.</summary>
public sealed class RuleStatusComponentAttribute : FormComponentAttribute;

/// <summary>Client properties of the server-only rules below.</summary>
public sealed class ServerValidatedClientProperties : ValidationRuleClientProperties;

/// <summary>Validates one section of the rule JSON with <see cref="RuleSerializer"/> on the server.</summary>
[ValidationRuleAttribute(typeof(RuleJsonValidationRuleAttribute))]
public sealed class RuleJsonValidationRule : ValidationRule<RuleJsonValidationRuleProperties, ServerValidatedClientProperties, string>
{
    /// <summary>A client rule that always passes: these rules are evaluated on the server only.</summary>
    internal const string ServerOnlyClientRule = "@xperience-community/lucene-suite/ServerValidatedValidationRule";

    /// <inheritdoc />
    public override string ClientRuleName => ServerOnlyClientRule;

    /// <inheritdoc />
    public override Task<ValidationResult> Validate(string value, IFormFieldValueProvider formFieldValueProvider)
    {
        var errors = RuleSectionValidator.Validate(Properties.Section, value);
        return errors.Count == 0
            ? ValidationResult.SuccessResult()
            : ValidationResult.FailResult(Message(Properties.Section, errors));
    }

    // The structured components show each error on its control; the field-level message only summarizes.
    internal static string Message(RuleJsonSection section, IReadOnlyList<RuleError> errors) => section switch
    {
        RuleJsonSection.UserData => errors[0].Message,
        _ => errors.Count == 1 ? "Fix the highlighted problem." : $"Fix the {errors.Count} highlighted problems.",
    };
}

/// <summary>Properties of <see cref="RuleJsonValidationRule"/>.</summary>
public sealed class RuleJsonValidationRuleProperties : ValidationRuleProperties
{
    /// <summary>The section the field holds.</summary>
    public RuleJsonSection Section { get; set; }

    /// <inheritdoc />
    public override string GetDescriptionText(ILocalizationService localizationService) => "Valid rule JSON";
}

/// <summary>Applies <see cref="RuleJsonValidationRule"/> to a model property.</summary>
/// <param name="section">The section the property holds.</param>
public sealed class RuleJsonValidationRuleAttribute(RuleJsonSection section) : ValidationRuleAttribute
{
    /// <summary>The section the property holds.</summary>
    public RuleJsonSection Section { get; } = section;
}

/// <summary>Requires Valid until to be later than Valid from.</summary>
[ValidationRuleAttribute(typeof(RuleValidityValidationRuleAttribute))]
public sealed class RuleValidityValidationRule : ValidationRule<RuleValidityValidationRuleProperties, ServerValidatedClientProperties, DateTime?>
{
    /// <inheritdoc />
    public override string ClientRuleName => RuleJsonValidationRule.ServerOnlyClientRule;

    /// <inheritdoc />
    public override Task<ValidationResult> Validate(DateTime? value, IFormFieldValueProvider formFieldValueProvider)
    {
        bool valid = value is null
            || !formFieldValueProvider.TryGet<DateTime?>(nameof(RuleEditModel.ValidFromUtc), out var from)
            || from is null
            || from < value;
        return valid ? ValidationResult.SuccessResult() : ValidationResult.FailResult("Must be later than Valid from.");
    }
}

/// <summary>Properties of <see cref="RuleValidityValidationRule"/>.</summary>
public sealed class RuleValidityValidationRuleProperties : ValidationRuleProperties
{
    /// <inheritdoc />
    public override string GetDescriptionText(ILocalizationService localizationService) => "Valid until is later than Valid from";
}

/// <summary>Applies <see cref="RuleValidityValidationRule"/> to a model property.</summary>
public sealed class RuleValidityValidationRuleAttribute : ValidationRuleAttribute;
