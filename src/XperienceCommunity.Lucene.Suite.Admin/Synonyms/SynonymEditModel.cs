using System.Globalization;

using Kentico.Xperience.Admin.Base.FormAnnotations;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Admin.Synonyms;

/// <summary>The synonym editor form.</summary>
public sealed class SynonymEditModel
{
    /// <summary>Stored kind: <see cref="SynonymSerializer.TwoWay"/> or <see cref="SynonymSerializer.OneWay"/>.</summary>
    [SynonymKindValidationRule]
    [RadioGroupComponent(
        Label = "Kind",
        Options = SynonymSerializer.TwoWay + ";Two-way: every term finds the others\r\n" + SynonymSerializer.OneWay + ";One-way: the input term finds the terms",
        Order = 1)]
    public string Kind { get; set; } = SynonymSerializer.TwoWay;

    /// <summary>The term a one-way synonym expands; Kentico validates it only while visible (one-way).</summary>
    [RequiredValidationRule]
    [SynonymInputValidationRule]
    [VisibleIfEqualTo(nameof(Kind), SynonymSerializer.OneWay)]
    [TextInputComponent(Label = "Input term", ExplanationText = "Searches for this term also find the terms below; searches for those terms do not find it.", Order = 2)]
    public string? Input { get; set; }

    /// <summary>Terms JSON (array of strings); empty when there are no terms, so Kentico's required check (skipped until save) reports it.</summary>
    [RequiredValidationRule]
    [SynonymTermsValidationRule]
    [SynonymTermsComponent(Label = "Terms", Order = 3)]
    public string Terms { get; set; } = string.Empty;

    /// <summary>Whether searches use the synonym.</summary>
    [CheckBoxComponent(Label = "Enabled", Order = 4)]
    public bool Enabled { get; set; } = true;

    /// <summary>The stored row's LastModified when the form was loaded; a mismatch at save means someone else changed the synonym.</summary>
    [RuleHiddenComponent(Order = 5)]
    public string? LoadedVersion { get; set; }
}

/// <summary>The stored fields of a synonym, decoupled from <see cref="SynonymInfo"/> (which needs a running Kentico instance).</summary>
internal sealed record StoredSynonym(int SynonymId, string Kind, string Input, string Terms, bool Enabled, DateTime LastModified)
{
    /// <summary>An unsaved synonym.</summary>
    public static StoredSynonym New { get; } = new(0, SynonymSerializer.TwoWay, string.Empty, "[]", true, default);

    public static StoredSynonym From(SynonymInfo synonym) =>
        new(synonym.SynonymID, synonym.Kind, synonym.Input, synonym.Terms, synonym.Enabled, synonym.LastModified);

    public void CopyTo(SynonymInfo synonym)
    {
        synonym.Kind = Kind;
        synonym.Input = Input;
        synonym.Terms = Terms;
        synonym.Enabled = Enabled;
    }
}

/// <summary>Maps between <see cref="StoredSynonym"/> and <see cref="SynonymEditModel"/>.</summary>
internal static class SynonymEditMapping
{
    /// <summary>Version token compared at save (round-trip format of LastModified).</summary>
    public static string VersionOf(StoredSynonym synonym) => synonym.LastModified.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>True when the row changed after the form loaded it (or the form carries no version).</summary>
    public static bool IsStale(string? loadedVersion, StoredSynonym stored) => loadedVersion != VersionOf(stored);

    public static SynonymEditModel FromSynonym(StoredSynonym synonym) => new()
    {
        Kind = synonym.Kind,
        Input = string.IsNullOrEmpty(synonym.Input) ? null : synonym.Input,
        Terms = synonym.Terms is null or "" or "[]" ? string.Empty : synonym.Terms,
        Enabled = synonym.Enabled,
        LoadedVersion = VersionOf(synonym),
    };

    /// <summary>Validates the edited synonym as the search will read it; Input is ignored for two-way groups.</summary>
    public static SynonymParseResult Validate(string? kind, string? input, string? terms, int id, string indexName) =>
        SynonymSerializer.Parse(id, indexName, kind, kind == SynonymSerializer.OneWay ? input : null, terms);

    /// <summary>The row's new values: normalized terms, and Input only for one-way synonyms.</summary>
    public static StoredSynonym Apply(SynonymDefinition definition, bool enabled, StoredSynonym stored) => stored with
    {
        Kind = definition.Kind == SynonymKind.OneWay ? SynonymSerializer.OneWay : SynonymSerializer.TwoWay,
        Input = definition.Input ?? string.Empty,
        Terms = SynonymSerializer.SerializeTerms(definition.Terms),
        Enabled = enabled,
    };
}
