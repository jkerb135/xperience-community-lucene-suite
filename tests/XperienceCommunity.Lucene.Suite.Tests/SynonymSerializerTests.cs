using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SynonymSerializerTests
{
    private static SynonymParseResult Parse(string kind, string? input, string terms, string index = "articles") =>
        SynonymSerializer.Parse(3, index, kind, input, terms);

    private static string Paths(SynonymParseResult result) => string.Join(",", result.Errors.Select(error => error.Path));

    [Fact]
    public void TwoWay_IsNormalized_AndRoundTrips()
    {
        var result = Parse(SynonymSerializer.TwoWay, null, """["  Coffee  GRINDER ","Burr mill"]""");

        var synonym = Assert.IsType<SynonymDefinition>(result.Definition);
        Assert.Equal(SynonymKind.TwoWay, synonym.Kind);
        Assert.Null(synonym.Input);
        Assert.Equal(["coffee grinder", "burr mill"], synonym.Terms);
        Assert.Equal(synonym.Terms, Parse(SynonymSerializer.TwoWay, "", SynonymSerializer.SerializeTerms(synonym.Terms)).Definition!.Terms);
    }

    [Fact]
    public void OneWay_NeedsAnInput_ThatIsNotOneOfItsTerms()
    {
        Assert.Equal("tee", Parse(SynonymSerializer.OneWay, " Tee ", """["T-shirt"]""").Definition!.Input);
        Assert.Equal("input", Paths(Parse(SynonymSerializer.OneWay, " ", """["t-shirt"]""")));
        Assert.Equal("terms[1]", Paths(Parse(SynonymSerializer.OneWay, "tee", """["t-shirt","Tee."]""")));
        Assert.Equal("Must differ from the input.", Parse(SynonymSerializer.OneWay, "tee", """["t-shirt","Tee."]""").Errors[0].Message);
    }

    [Theory]
    [InlineData("twoWay", null, """["only"]""", "terms")]
    [InlineData("twoWay", "x", """["a","b"]""", "input")]
    [InlineData("twoWay", null, """["T-Shirt","t-shirt."]""", "terms[1]")]
    [InlineData("twoWay", null, """["a",""]""", "terms[1]")]
    [InlineData("twoWay", null, """["a","!!!"]""", "terms[1]")]
    [InlineData("twoWay", null, """["a","one two three four five six"]""", "terms[1]")]
    [InlineData("twoWay", null, """["a",null]""", "terms[1]")]
    [InlineData("twoWay", null, "[\"a\",", "terms")]
    [InlineData("twoWay", null, """{"a":1}""", "terms")]
    [InlineData("oneWay", "tee", "[]", "terms")]
    [InlineData("replace", null, """["a","b"]""", "kind")]
    [InlineData("TwoWay", null, """["a","b"]""", "kind")]
    public void InvalidSynonyms_ReportTheirPath(string kind, string? input, string terms, string expected) =>
        Assert.Equal(expected, Paths(Parse(kind, input, terms)));

    [Fact]
    public void Limits_AreEnforced()
    {
        string twentyOne = "[" + string.Join(",", Enumerable.Range(0, 21).Select(i => $"\"t{i}\"")) + "]";
        string longTerm = $"""["a","{new string('x', SynonymLimits.TermLength + 1)}"]""";

        Assert.Equal("terms", Paths(Parse(SynonymSerializer.TwoWay, null, twentyOne)));
        Assert.Equal("terms[1]", Paths(Parse(SynonymSerializer.TwoWay, null, longTerm)));
        Assert.Equal("indexName", Paths(Parse(SynonymSerializer.TwoWay, null, """["a","b"]""", index: " ")));
        Assert.NotNull(Parse(SynonymSerializer.TwoWay, null, """["one two three four five","b"]""").Definition);
    }
}
