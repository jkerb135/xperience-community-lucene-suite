using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.Synonyms;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SynonymEditModelTests
{
    private static StoredSynonym Stored(string kind, string input, string terms) =>
        new(4, kind, input, terms, true, new DateTime(2026, 10, 11, 9, 0, 0, 250, DateTimeKind.Utc));

    [Fact]
    public void OneWay_RoundTripsThroughTheForm()
    {
        var stored = Stored(SynonymSerializer.OneWay, "tee", """["t-shirt","tees"]""");

        var model = SynonymEditMapping.FromSynonym(stored);
        var parsed = SynonymEditMapping.Validate(model.Kind, model.Input, model.Terms, stored.SynonymId, "articles");
        var saved = SynonymEditMapping.Apply(parsed.Definition!, model.Enabled, stored);

        Assert.Equal(stored, saved);
        Assert.False(SynonymEditMapping.IsStale(model.LoadedVersion, stored));
        Assert.True(SynonymEditMapping.IsStale(model.LoadedVersion, stored with { LastModified = stored.LastModified.AddMilliseconds(1) }));
    }

    [Fact]
    public void SwitchingToTwoWay_IgnoresAndClearsTheInput_AndNormalizesTerms()
    {
        var stored = Stored(SynonymSerializer.OneWay, "tee", """["t-shirt"]""");

        var parsed = SynonymEditMapping.Validate(SynonymSerializer.TwoWay, "tee", """[" Grinder ","Mill","café"]""", stored.SynonymId, "articles");
        var saved = SynonymEditMapping.Apply(parsed.Definition!, enabled: false, stored);

        Assert.Equal((SynonymSerializer.TwoWay, string.Empty, """["grinder","mill","café"]""", false), (saved.Kind, saved.Input, saved.Terms, saved.Enabled));
        Assert.Null(SynonymEditMapping.FromSynonym(saved).Input);
    }

    [Fact]
    public void FieldErrors_ReachTheFieldThatShowsThem()
    {
        Assert.Equal(["terms[1]"], Paths(SynonymFieldErrors.Terms(SynonymSerializer.TwoWay, null, """["mill","Mill."]""")));
        Assert.Equal(["terms[0]"], Paths(SynonymFieldErrors.Terms(SynonymSerializer.OneWay, "tee", """["Tee"]""")));
        Assert.Equal(["terms"], Paths(SynonymFieldErrors.Terms(SynonymSerializer.TwoWay, null, """["only"]""")));
        Assert.Equal(["input"], Paths(SynonymFieldErrors.Input(SynonymSerializer.OneWay, " ", """["t-shirt"]""")));
        Assert.Empty(SynonymFieldErrors.Input(SynonymSerializer.TwoWay, " ", """["a","b"]"""));
        Assert.Equal(["kind"], Paths(SynonymFieldErrors.Kind("replace")));
        Assert.Empty(SynonymFieldErrors.Kind(SynonymSerializer.OneWay));
    }

    [Fact]
    public void FieldMessage_SummarizesTermErrors_AndShowsListErrorsDirectly()
    {
        Assert.Equal("Fix the highlighted term.", SynonymFieldErrors.Message([new("terms[1]", "Duplicate term.")]));
        Assert.Equal("Fix the 2 highlighted problems.", SynonymFieldErrors.Message([new("terms[0]", "a"), new("terms[1]", "b")]));
        Assert.Equal("Must have 2–20 terms.", SynonymFieldErrors.Message([new("terms", "Must have 2–20 terms.")]));
    }

    [Theory]
    [InlineData("twoWay", "", """["grinder","mill","burr"]""", "grinder ⇔ mill ⇔ burr")]
    [InlineData("oneWay", "Tee", """["t-shirt","tees"]""", "tee → t-shirt, tees")]
    [InlineData("oneWay", "", """["t-shirt"]""", RuleSummaries.Invalid)]
    [InlineData("twoWay", "", "[", RuleSummaries.Invalid)]
    public void TermsSummary(string kind, string input, string terms, string expected) =>
        Assert.Equal(expected, SynonymSummaries.Terms(kind, input, terms));

    [Theory]
    [InlineData("twoWay", "Two-way")]
    [InlineData("oneWay", "One-way")]
    [InlineData("replace", RuleSummaries.Invalid)]
    public void KindSummary(string kind, string expected) => Assert.Equal(expected, SynonymSummaries.Kind(kind));

    private static string[] Paths(IEnumerable<Suite.Rules.RuleError> errors) => [.. errors.Select(error => error.Path)];
}
