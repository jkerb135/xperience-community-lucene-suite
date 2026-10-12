using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Suggestions;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SuggestionEngineTests
{
    private static readonly SuggestionBlock[] NoBlocks = [];

    private static IReadOnlyList<SuggestionCandidate> Candidates(params (string? Text, long Searches, long Zero)[] rows) =>
        SuggestionEngine.Candidates(rows.Select(row => new SuggestionRow(row.Text, row.Searches, row.Zero)), new SuiteSuggestionOptions());

    private static IReadOnlyList<string> Match(IReadOnlyList<SuggestionCandidate> candidates, string? input, int max = 8, params SuggestionBlock[] blocks) =>
        SuggestionEngine.Match(candidates, blocks, input, 2, max);

    [Fact]
    public void Candidates_DropBrowseOtherPlaceholdersRareAndMostlyEmptyTexts()
    {
        var candidates = Candidates(
            ("coffee", 10, 4),
            ("", 50, 0),
            (null, 50, 0),
            ("mail {email}", 20, 0),
            ("order {number}", 20, 0),
            ("rare", 4, 0),
            ("half empty", 10, 5),
            ("!!!", 10, 0));

        Assert.Equal(["coffee"], candidates.Select(c => c.Text));
    }

    [Fact]
    public void Candidates_MergeTextsWithOneMatchKey_UnderTheMostSearchedText_AndCapByMaxCandidates()
    {
        var options = new SuiteSuggestionOptions { MaxCandidates = 2 };
        var rows = new[] { new SuggestionRow("coffee!", 6, 0), new SuggestionRow("Coffee", 9, 0), new SuggestionRow("tea", 10, 0), new SuggestionRow("milk", 5, 0) };

        var candidates = SuggestionEngine.Candidates(rows, options);

        Assert.Equal([new SuggestionCandidate("Coffee", "coffee", 15), new SuggestionCandidate("tea", "tea", 10)], candidates);
    }

    [Fact]
    public void PrefixMatchesRankFirst_ThenLaterWordMatches_EachBySearchesThenText()
    {
        var candidates = Candidates(
            ("best coffee grinder", 50, 0),
            ("coffee grinder", 10, 0),
            ("coffee green", 10, 0),
            ("coffee great", 30, 0),
            ("cheap coffee grinders", 50, 0),
            ("grinder coffee", 99, 0),
            ("coffeegrinder", 99, 0));

        Assert.Equal(
            ["coffee great", "coffee green", "coffee grinder", "best coffee grinder", "cheap coffee grinders"],
            Match(candidates, "Coffee  GR"));
    }

    [Fact]
    public void LaterWordMatch_NeedsTheEarlierWordsWhole()
    {
        var candidates = Candidates(("best coffee grinder", 10, 0), ("decaf coffee", 10, 0), ("coffeehouse menu", 10, 0));

        Assert.Equal(["coffeehouse menu", "best coffee grinder", "decaf coffee"], Match(candidates, "coffee"));
        Assert.Equal(["best coffee grinder"], Match(candidates, "coffee g"));
        Assert.Empty(Match(candidates, "offee"));
    }

    [Fact]
    public void Diacritics_AndCase_FollowTheMatchKey()
    {
        var candidates = Candidates(("Crème brûlée", 10, 0), ("creme caramel", 10, 0));

        Assert.Equal(["Crème brûlée"], Match(candidates, "CRÈME"));
        Assert.Equal(["creme caramel"], Match(candidates, "creme"));
    }

    [Fact]
    public void ShortOrNullInput_MatchesNothing()
    {
        var candidates = Candidates(("coffee", 10, 0));

        Assert.Empty(Match(candidates, null));
        Assert.Empty(Match(candidates, "c"));
        Assert.Empty(Match(candidates, " c! "));
        Assert.Equal(["coffee"], Match(candidates, "co"));
        Assert.Equal(["coffee"], SuggestionEngine.Match(candidates, NoBlocks, "c", 1, 8));
    }

    [Fact]
    public void ExactBlock_RemovesOnlyAnEqualKey()
    {
        var candidates = Candidates(("coffee", 30, 0), ("coffee grinder", 20, 0));

        Assert.Equal(["coffee grinder"], Match(candidates, "cof", blocks: new SuggestionBlock("coffee", false)));
    }

    [Fact]
    public void WordBlock_RemovesEveryCandidateWithTheWholeWord()
    {
        var candidates = Candidates(("acme grinder", 30, 0), ("grinder acme deal", 20, 0), ("grinder acmeco", 10, 0), ("grinder", 5, 0));

        Assert.Equal(["grinder acmeco", "grinder"], Match(candidates, "grin", blocks: new SuggestionBlock("acme", true)));
        Assert.Equal(["grinder acme deal", "grinder"], Match(candidates, "grin", blocks: [new SuggestionBlock("grinder acmeco", true), new SuggestionBlock("acme grinder", true)]));
    }

    [Fact]
    public void Ties_AreOrderedByText_AndMaxCapsTheResult()
    {
        var candidates = Candidates(("tea b", 5, 0), ("tea a", 5, 0), ("tea c", 5, 0));

        Assert.Equal(["tea a", "tea b"], Match(candidates, "tea", max: 2));
    }
}
