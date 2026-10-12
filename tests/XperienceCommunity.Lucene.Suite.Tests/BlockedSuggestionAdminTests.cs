using XperienceCommunity.Lucene.Suite.Admin.Suggestions;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Suggestions;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class BlockedSuggestionAdminTests
{
    [Fact]
    public void Validate_TrimsTheText_AndComputesTheMatchKey()
    {
        var (entry, error) = BlockedSuggestionValidation.Validate("  T-Shirt  Black! ", BlockedSuggestionInfo.MatchModeWord);

        Assert.Null(error);
        Assert.Equal(new BlockedSuggestionEntry("T-Shirt  Black!", "t-shirt black", BlockedSuggestionInfo.MatchModeWord), entry);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" ?! ... ")]
    public void Validate_RefusesTextWithoutAMatchKey(string? text) =>
        AssertRefused(text, BlockedSuggestionInfo.MatchModeExact);

    [Theory]
    [InlineData("coffee {email}")]
    [InlineData("{NUMBER} beans")]
    public void Validate_RefusesPlaceholders(string text) =>
        AssertRefused(text, BlockedSuggestionInfo.MatchModeExact);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Exact")]
    [InlineData("contains")]
    public void Validate_RefusesUnknownModes(string? mode) =>
        AssertRefused("coffee", mode);

    [Fact]
    public void Validate_AllowsExactly100Characters_AfterTrimming()
    {
        Assert.NotNull(BlockedSuggestionValidation.Validate("  " + new string('a', 100) + "  ", BlockedSuggestionInfo.MatchModeExact).Entry);
        AssertRefused(new string('a', 101), BlockedSuggestionInfo.MatchModeExact);
    }

    [Fact]
    public void Validate_KeepsTheMatchKeyWithinTheColumn_ForUnicodeText()
    {
        // .NET lower-cases one char to one char, so the key never outgrows the text; the validator still checks both.
        string text = new string('İ', 100);

        var (entry, _) = BlockedSuggestionValidation.Validate(text, BlockedSuggestionInfo.MatchModeExact);

        Assert.NotNull(entry);
        Assert.True(entry.MatchKey.Length <= BlockedSuggestionInfo.TextMaxLength);
        AssertRefused(text + "İ", BlockedSuggestionInfo.MatchModeExact);
    }

    [Theory]
    [InlineData("word", "Contains words")]
    [InlineData("exact", "Exact query")]
    [InlineData("Word", "Contains words")]
    [InlineData("words", "Exact query")]
    [InlineData("", "Exact query")]
    [InlineData(null, "Exact query")]
    public void Listing_ShowsEveryModeButWordAsExact_LikeTheEngine(string? mode, string label) =>
        Assert.Equal(label, BlockedSuggestionListingPage.MatchLabel(mode));

    private static void AssertRefused(string? text, string? mode)
    {
        var (entry, error) = BlockedSuggestionValidation.Validate(text, mode);
        Assert.Null(entry);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
