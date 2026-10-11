using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.FormAnnotations;
using Kentico.Xperience.Admin.Base.Forms;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Admin.Synonyms;

/// <summary>Client properties of <see cref="SynonymTermsFormComponent"/>: the limits the chip editor enforces before the server does.</summary>
public sealed class SynonymTermsClientProperties : FormComponentClientProperties<string>
{
    /// <summary>Most terms per synonym.</summary>
    public int MaxTerms { get; set; } = SynonymLimits.Terms;

    /// <summary>Most characters per term.</summary>
    public int MaxTermLength { get; set; } = SynonymLimits.TermLength;
}

/// <summary>Arguments of the terms component's Validate command.</summary>
public sealed class SynonymTermsValidationArguments
{
    /// <summary>The component's terms JSON.</summary>
    public string? Value { get; set; }

    /// <summary>The form's current Kind.</summary>
    public string? Kind { get; set; }

    /// <summary>The form's current Input.</summary>
    public string? Input { get; set; }
}

/// <summary>Chip editor of a synonym's terms.</summary>
[ComponentAttribute(typeof(SynonymTermsComponentAttribute))]
public sealed class SynonymTermsFormComponent : FormComponent<SynonymTermsClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientComponentName => "@xperience-community/lucene-suite/SynonymTerms";

    /// <summary>Validates the terms with the server rules; changes nothing.</summary>
    [FormComponentCommand]
    public Task<ICommandResponse<RuleJsonValidationResult>> Validate(SynonymTermsValidationArguments arguments) =>
        Task.FromResult(ResponseFrom(new RuleJsonValidationResult(SynonymFieldErrors.Terms(arguments.Kind, arguments.Input, arguments.Value))));
}

/// <summary>Edits the property with <see cref="SynonymTermsFormComponent"/>.</summary>
public sealed class SynonymTermsComponentAttribute : FormComponentAttribute;

/// <summary>Splits <see cref="SynonymSerializer"/> errors by the field that shows them.</summary>
internal static class SynonymFieldErrors
{
    // Index ownership is checked by the pages; validation only needs a well-formed name.
    private const string ValidationIndex = "validation";

    public static IReadOnlyList<RuleError> Terms(string? kind, string? input, string? terms) =>
        [.. All(kind, input, terms).Where(error => error.Path == "terms" || error.Path.StartsWith("terms[", StringComparison.Ordinal))];

    public static IReadOnlyList<RuleError> Input(string? kind, string? input, string? terms) =>
        kind == SynonymSerializer.OneWay ? [.. All(kind, input, terms).Where(error => error.Path == "input")] : [];

    public static IReadOnlyList<RuleError> Kind(string? kind) =>
        [.. SynonymSerializer.Parse(0, ValidationIndex, kind, "x", """["x","y"]""").Errors.Where(error => error.Path == "kind")];

    /// <summary>The field-level message: a term-specific summary for chips, the error itself otherwise.</summary>
    public static string Message(IReadOnlyList<RuleError> errors) =>
        errors.Count == 1 && !errors[0].Path.StartsWith("terms[", StringComparison.Ordinal)
            ? errors[0].Message
            : errors.Count == 1 ? "Fix the highlighted term." : $"Fix the {errors.Count} highlighted problems.";

    private static IReadOnlyList<RuleError> All(string? kind, string? input, string? terms) =>
        SynonymEditMapping.Validate(kind, input, terms, 0, ValidationIndex).Errors;
}

/// <summary>Validates the terms with <see cref="SynonymSerializer"/> on the server, reading Kind and Input from the form.</summary>
[ValidationRuleAttribute(typeof(SynonymTermsValidationRuleAttribute))]
public sealed class SynonymTermsValidationRule : ValidationRule<SynonymValidationRuleProperties, ServerValidatedClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientRuleName => RuleJsonValidationRule.ServerOnlyClientRule;

    /// <inheritdoc />
    public override Task<ValidationResult> Validate(string value, IFormFieldValueProvider formFieldValueProvider)
    {
        // No terms is the required rule's case: it is skipped while the form loads and enforced at save.
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "[]")
        {
            return ValidationResult.SuccessResult();
        }

        formFieldValueProvider.TryGet<string>(nameof(SynonymEditModel.Kind), out var kind);
        formFieldValueProvider.TryGet<string>(nameof(SynonymEditModel.Input), out var input);
        var errors = SynonymFieldErrors.Terms(kind, input, value);
        return errors.Count == 0 ? ValidationResult.SuccessResult() : ValidationResult.FailResult(SynonymFieldErrors.Message(errors));
    }
}

/// <summary>Validates a one-way synonym's input term on the server.</summary>
[ValidationRuleAttribute(typeof(SynonymInputValidationRuleAttribute))]
public sealed class SynonymInputValidationRule : ValidationRule<SynonymValidationRuleProperties, ServerValidatedClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientRuleName => RuleJsonValidationRule.ServerOnlyClientRule;

    /// <inheritdoc />
    public override Task<ValidationResult> Validate(string value, IFormFieldValueProvider formFieldValueProvider)
    {
        // An empty input is the required rule's case: it is skipped until save.
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.SuccessResult();
        }

        formFieldValueProvider.TryGet<string>(nameof(SynonymEditModel.Kind), out var kind);
        formFieldValueProvider.TryGet<string>(nameof(SynonymEditModel.Terms), out var terms);
        var errors = SynonymFieldErrors.Input(kind, value, terms);
        return errors.Count == 0 ? ValidationResult.SuccessResult() : ValidationResult.FailResult(errors[0].Message);
    }
}

/// <summary>Validates the synonym kind on the server.</summary>
[ValidationRuleAttribute(typeof(SynonymKindValidationRuleAttribute))]
public sealed class SynonymKindValidationRule : ValidationRule<SynonymValidationRuleProperties, ServerValidatedClientProperties, string>
{
    /// <inheritdoc />
    public override string ClientRuleName => RuleJsonValidationRule.ServerOnlyClientRule;

    /// <inheritdoc />
    public override Task<ValidationResult> Validate(string value, IFormFieldValueProvider formFieldValueProvider)
    {
        var errors = SynonymFieldErrors.Kind(value);
        return errors.Count == 0 ? ValidationResult.SuccessResult() : ValidationResult.FailResult(errors[0].Message);
    }
}

/// <summary>Properties of the synonym validation rules.</summary>
public sealed class SynonymValidationRuleProperties : ValidationRuleProperties
{
    /// <inheritdoc />
    public override string GetDescriptionText(ILocalizationService localizationService) => "Valid synonym";
}

/// <summary>Applies <see cref="SynonymTermsValidationRule"/> to a model property.</summary>
public sealed class SynonymTermsValidationRuleAttribute : ValidationRuleAttribute;

/// <summary>Applies <see cref="SynonymInputValidationRule"/> to a model property.</summary>
public sealed class SynonymInputValidationRuleAttribute : ValidationRuleAttribute;

/// <summary>Applies <see cref="SynonymKindValidationRule"/> to a model property.</summary>
public sealed class SynonymKindValidationRuleAttribute : ValidationRuleAttribute;
