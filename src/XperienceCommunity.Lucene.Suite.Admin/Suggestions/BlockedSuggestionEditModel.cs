using CMS.DataEngine;

using Kentico.Xperience.Admin.Base.FormAnnotations;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Suggestions;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Admin.Suggestions;

/// <summary>The Block suggestion form.</summary>
public sealed class BlockedSuggestionEditModel
{
    /// <summary>The blocked text as entered.</summary>
    [RequiredValidationRule]
    [TextInputComponent(
        Label = "Blocked text",
        ExplanationText = "At most 100 characters. Letter case and punctuation are ignored.",
        Order = 1)]
    public string? Text { get; set; }

    /// <summary><see cref="BlockedSuggestionInfo.MatchModeExact"/> or <see cref="BlockedSuggestionInfo.MatchModeWord"/>.</summary>
    [RadioGroupComponent(
        Label = "Match",
        Options = BlockedSuggestionInfo.MatchModeExact + ";Exact query: hide this suggestion only\r\n"
            + BlockedSuggestionInfo.MatchModeWord + ";Contains words: hide every suggestion containing these words",
        Order = 2)]
    public string MatchMode { get; set; } = BlockedSuggestionInfo.MatchModeExact;
}

/// <summary>A validated blocklist entry.</summary>
internal sealed record BlockedSuggestionEntry(string Text, string MatchKey, string MatchMode);

/// <summary>Shared by the create page and the report's Block suggestion command.</summary>
internal static class BlockedSuggestionValidation
{
    internal const string DuplicateMessage = "This text is already blocked in this index with the same match.";
    internal const string IndexMissingMessage = "This index no longer exists. Reload the page.";

    /// <summary>Trims the text and checks it; exactly one of the results is set.</summary>
    public static (BlockedSuggestionEntry? Entry, string? Error) Validate(string? text, string? mode)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        string key = SynonymEngine.MatchKey(trimmed);
        if (key.Length == 0)
        {
            return (null, "Enter a text that contains at least one letter or digit.");
        }

        if (trimmed.Length > BlockedSuggestionInfo.TextMaxLength || key.Length > BlockedSuggestionInfo.TextMaxLength)
        {
            return (null, $"The text can have at most {BlockedSuggestionInfo.TextMaxLength} characters.");
        }

        if (trimmed.Contains(QueryScrubber.EmailPlaceholder, StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(QueryScrubber.NumberPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "The text contains a removed value ({email} or {number}); it is never suggested.");
        }

        if (mode is not (BlockedSuggestionInfo.MatchModeExact or BlockedSuggestionInfo.MatchModeWord))
        {
            return (null, "Choose Exact query or Contains words.");
        }

        return (new BlockedSuggestionEntry(trimmed, key, mode), null);
    }

    public static bool Exists(IInfoProvider<BlockedSuggestionInfo> provider, string indexName, string matchKey, string mode) =>
        provider.Get()
            .WhereEquals(nameof(BlockedSuggestionInfo.IndexName), indexName)
            .WhereEquals(nameof(BlockedSuggestionInfo.MatchKey), matchKey)
            .WhereEquals(nameof(BlockedSuggestionInfo.MatchMode), mode)
            .TopN(1)
            .Column(nameof(BlockedSuggestionInfo.BlockedSuggestionID))
            .Any();

    public static BlockedSuggestionInfo Add(IInfoProvider<BlockedSuggestionInfo> provider, string indexName, BlockedSuggestionEntry entry)
    {
        var info = new BlockedSuggestionInfo
        {
            IndexName = indexName,
            Text = entry.Text,
            MatchKey = entry.MatchKey,
            MatchMode = entry.MatchMode,
        };
        provider.Set(info);
        return info;
    }
}
