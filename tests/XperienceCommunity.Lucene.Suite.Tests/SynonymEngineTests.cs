using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SynonymEngineTests
{
    private static SynonymDefinition TwoWay(int id, params string[] terms) => new(id, "i", SynonymKind.TwoWay, null, terms);

    private static SynonymDefinition OneWay(int id, string input, params string[] terms) => new(id, "i", SynonymKind.OneWay, input, terms);

    private static SynonymExpansion Expand(string query, int max = 20, params SynonymDefinition[] synonyms) =>
        SynonymEngine.Expand(new SynonymSet(synonyms), query, max);

    [Fact]
    public void TwoWay_WorksInBothDirections()
    {
        var set = new[] { TwoWay(1, "grinder", "mill") };

        Assert.Equal(["coffee mill"], Expand("coffee grinder", synonyms: set).Variants);
        Assert.Equal(["coffee grinder"], Expand("coffee mill", synonyms: set).Variants);
        Assert.Equal(new SynonymSubstitution(1, "grinder", "mill"), Assert.Single(Expand("coffee grinder", synonyms: set).Applied));
    }

    [Fact]
    public void OneWay_IsNeverReversed()
    {
        var set = new[] { OneWay(1, "tee", "t-shirt") };

        Assert.Equal(["black t-shirt"], Expand("black tee", synonyms: set).Variants);
        Assert.Empty(Expand("black t-shirt", synonyms: set).Variants);
    }

    [Fact]
    public void Synonyms_DoNotCascade()
    {
        var set = new[] { TwoWay(1, "a", "b"), TwoWay(2, "b", "c") };

        Assert.Equal(["b"], Expand("a", synonyms: set).Variants);
    }

    [Fact]
    public void MultiWordRuns_MatchContiguousAndInOrder_WithPunctuationIgnored()
    {
        var set = new[] { TwoWay(1, "coffee grinder", "burr mill"), OneWay(2, "t-shirt", "tee") };

        Assert.Equal(["best burr mill"], Expand("best Coffee Grinder!", synonyms: set).Variants);
        Assert.Equal(["black tee"], Expand("black T-Shirt.", synonyms: set).Variants);
        Assert.Empty(Expand("grinder coffee", synonyms: set).Variants);
        Assert.Empty(Expand("coffee red grinder", synonyms: set).Variants);
    }

    [Fact]
    public void UnchangedWords_KeepTheirTypedForm()
    {
        var set = new[] { TwoWay(1, "grinder", "mill") };

        Assert.Equal(["Cheap mill"], Expand("Cheap  grinder", synonyms: set).Variants);
    }

    [Fact]
    public void Variants_AreOrderedByPositionThenSynonym_DeduplicatedAndCapped()
    {
        var set = new[] { TwoWay(2, "grinder", "mill"), TwoWay(1, "coffee", "java", "joe"), TwoWay(3, "mill", "grinder") };

        var all = Expand("coffee grinder", synonyms: set);
        Assert.Equal(["java grinder", "joe grinder", "coffee mill"], all.Variants);
        Assert.Equal([1, 1, 2], all.Applied.Select(applied => applied.SynonymId));
        Assert.Equal(["java grinder", "joe grinder"], Expand("coffee grinder", max: 2, synonyms: set).Variants);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("espresso")]
    public void NoMatch_YieldsNoVariants(string? query) =>
        Assert.Same(SynonymExpansion.None, SynonymEngine.Expand(new SynonymSet([TwoWay(1, "grinder", "mill")]), query, 20));

    [Fact]
    public void MatchKey_NormalizesCaseWhitespaceAndEdgePunctuation() =>
        Assert.Equal("t-shirt black", SynonymEngine.MatchKey("  \"T-Shirt,\"   BLACK. "));
}
