using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.FormAnnotations;
using Kentico.Xperience.Admin.Base.Forms;

using XperienceCommunity.Lucene.Suite.Rules;

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

/// <summary>Structured editor of a rule's conditions.</summary>
[ComponentAttribute(typeof(RuleConditionsComponentAttribute))]
public sealed class RuleConditionsFormComponent : FormComponent<RuleJsonClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/RuleConditions";

    /// <summary>Validates the conditions JSON with the server rules; changes nothing.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleJsonValidationResult>> Validate(RuleJsonValidationArguments arguments) =>
        Task.FromResult(ResponseFrom(new RuleJsonValidationResult(RuleSectionValidator.Validate(RuleJsonSection.Conditions, arguments.Value))));
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

/// <summary>Edits the property with <see cref="RuleConditionsFormComponent"/>.</summary>
public sealed class RuleConditionsComponentAttribute : FormComponentAttribute;

/// <summary>Edits the property with <see cref="RuleBoostsFormComponent"/>.</summary>
public sealed class RuleBoostsComponentAttribute : FormComponentAttribute;

/// <summary>Carries the property with <see cref="RuleHiddenFormComponent"/>.</summary>
public sealed class RuleHiddenComponentAttribute : FormComponentAttribute;

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
