using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Reports.Insights;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class InsightEngineTests
{
    private static readonly LatencyPercentile Fast = new(100, false);

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    public void ContentGap_StartsAtTheNoResultRateCutoff(int zero, bool expected)
    {
        var findings = Kinds(Row("q", 20, zero: zero, clicked: 10));

        Assert.Equal(expected, findings.Contains("ContentGap q " + zero));
    }

    [Theory]
    [InlineData(1, "RelevanceMiss q 19")]
    [InlineData(2, "RelevanceMiss q 18")]
    [InlineData(3, null)]
    public void RelevanceMiss_EndsAtTheCtrCutoff_AndCountsSearchesWithResultsButNoClick(int clicked, string? expected)
    {
        var findings = Kinds(Row("q", 20, clicked: clicked, clicks: clicked, positions: clicked));

        Assert.Equal(expected is null ? [] : [expected], findings);
    }

    [Fact]
    public void ContentGapAndRelevanceMiss_AreExclusive()
    {
        Assert.Equal(["ContentGap q 6"], Kinds(Row("q", 20, zero: 6)));
        // Just below the content-gap cutoff with no clicks: a relevance miss over the 15 searches with results.
        Assert.Equal(["RelevanceMiss q 15"], Kinds(Row("q", 20, zero: 5)));
    }

    [Theory]
    [InlineData(5, 25, true)]
    [InlineData(5, 24, false)]
    [InlineData(5, 26, true)]
    [InlineData(4, 40, false)]
    [InlineData(6, 30, true)]
    public void BuriedAnswer_NeedsTheClickedSearchesAndTheAveragePosition(int clicked, int positions, bool expected)
    {
        var findings = Kinds(Row("q", 20, clicked: clicked, clicks: clicked, positions: positions));

        Assert.Equal(expected ? ["BuriedAnswer q " + clicked] : [], findings);
    }

    [Theory]
    // p95 buckets "<= 1 s" (500-1000 ms) and lower are not slow at a 1000 ms cutoff; "<= 2.5 s" (1000-2500 ms) is.
    [InlineData(500, false, false)]
    [InlineData(1000, false, false)]
    [InlineData(2500, false, true)]
    [InlineData(null, true, true)]
    public void SlowQuery_StartsWhenTheWholeP95BucketIsAtTheCutoff_AndIncludesTheOverflowBucket(int? ms, bool overflow, bool expected)
    {
        var findings = Kinds(Row("q", 20, clicked: 10, p95: new LatencyPercentile(ms, overflow)));

        Assert.Equal(expected ? ["SlowQuery q 20"] : [], findings);
    }

    [Fact]
    public void SlowQuery_Affected_CountsTheSearchesInBucketsAtOrAboveTheCutoff()
    {
        long[] buckets = [0, 0, 0, 0, 10, 4, 0, 3, 2, 1];
        var row = Row("q", 20, clicked: 10) with { LatencyBuckets = buckets, LatencyP95 = LatencyPercentiles.Of(buckets, 0.95m) };

        // Buckets 1000-2500, 2500-5000 and overflow: 3 + 2 + 1.
        Assert.Equal(["SlowQuery q 6"], Kinds(row));
    }

    [Fact]
    public void SlowQuery_NeedsAPercentile()
    {
        Assert.Empty(Kinds(Row("q", 20, clicked: 10) with { LatencyP95 = null }));
    }

    [Theory]
    // No-result rate 0.2 → 0.25 / 0.3 / 0.35: 0.3 − 0.2 is 0.0999… in doubles, still a 0.10 rise.
    // Affected = searches x the rise: 20 x 0.10 = 2, 20 x 0.15 = 3.
    [InlineData(5, null)]
    [InlineData(6, "Regression q 2")]
    [InlineData(7, "Regression q 3")]
    public void Regression_OnARisingNoResultRate_StartsAtTheCutoff_AndCountsTheSearchesLostToIt(int zero, string? expected)
    {
        var row = Row("q", 20, zero: zero, clicked: 10, previousSearches: 20, previousZero: 4, previousClicked: 10);

        Assert.Equal(expected, Kinds(row).SingleOrDefault(f => f.StartsWith("Regression", StringComparison.Ordinal)));
    }

    [Theory]
    // Click-through rate 0.5 → 0.45 / 0.4 / 0.35.
    [InlineData(9, null)]
    [InlineData(8, "Regression q 2")]
    [InlineData(7, "Regression q 3")]
    public void Regression_OnAFallingCtr_StartsAtTheCutoff_AndCountsTheSearchesLostToIt(int clicked, string? expected)
    {
        var row = Row("q", 20, clicked: clicked, previousSearches: 20, previousClicked: 10);

        Assert.Equal(expected is null ? [] : [expected], Kinds(row));
    }

    [Fact]
    public void Regression_IsSkipped_WhenThePreviousPeriodIsPartialOrTooSmall()
    {
        var row = Row("q", 20, clicked: 2 + 10, previousSearches: 20, previousClicked: 20);
        Assert.Equal(["Regression q 8"], Kinds(row));

        Assert.Empty(Kinds([row], previousComplete: false));
        Assert.Empty(Kinds(row with { PreviousSearches = 19, PreviousClickedSearches = 19 }));
    }

    [Fact]
    public void MinAffected_SuppressesSmallSamples_IncludingATwoInTwentyRegression()
    {
        var options = new SuiteInsightsOptions();
        Assert.Equal(5, options.MinAffected);

        Assert.Empty(Kinds([Row("q", 10, zero: 4, clicked: 6)], options: options));
        Assert.Equal(["ContentGap q 5"], Kinds([Row("q", 10, zero: 5, clicked: 5)], options: options));

        // 0 → 2 no-result searches of 20 is a 0.10 rise but only 2 searches.
        Assert.Empty(Kinds([Row("q", 20, zero: 2, clicked: 10, previousSearches: 20, previousClicked: 10)], options: options));
        // 2 → 0 clicked searches of 20: a 0.10 fall of 2 searches (the relevance miss is real, the regression is noise).
        Assert.DoesNotContain(
            Kinds([Row("q", 20, clicked: 0, previousSearches: 20, previousClicked: 2)], options: options),
            finding => finding.StartsWith("Regression", StringComparison.Ordinal));
        // 0 → 5 no-result searches of 20: 5 over both periods, at the cutoff.
        Assert.Contains("Regression q 5", Kinds([Row("q", 20, zero: 5, clicked: 10, previousSearches: 20, previousZero: 0, previousClicked: 10)], options: options));
    }

    [Fact]
    public void AQuery_CanHaveSeveralFindings()
    {
        Assert.Equal(
            ["SlowQuery q 20", "ContentGap q 10", "Regression q 10"],
            Kinds(Row("q", 20, zero: 10, clicked: 10, p95: new LatencyPercentile(null, true), previousSearches: 20, previousClicked: 10)));
    }

    [Fact]
    public void Findings_AreOrderedByAffectedThenKindThenText_AndCappedWithACountOfTheRest()
    {
        var rows = new[]
        {
            Row("b", 20, zero: 10, clicked: 10),
            Row("a", 20, zero: 10, clicked: 10),
            Row("c", 40, zero: 20, clicked: 20, p95: new LatencyPercentile(null, true)),
            Row("d", 30, clicked: 10, clicks: 10, positions: 100),
        };

        Assert.Equal(["SlowQuery c 40", "ContentGap c 20", "ContentGap a 10", "ContentGap b 10", "BuriedAnswer d 10"], Kinds(rows));

        var (findings, omitted) = InsightEngine.Evaluate(rows, new SuiteInsightsOptions { MinAffected = 1, MaxFindings = 2 }, true);
        Assert.Equal((2, 3), (findings.Count, omitted));
    }

    [Fact]
    public void NullRates_NeverProduceFindings()
    {
        Assert.Empty(Kinds(Row("q", 0, p95: new LatencyPercentile(null, true), previousSearches: 20, previousZero: 0)));
    }

    [Fact]
    public void Findings_CarryTheEvidence()
    {
        var finding = InsightEngine.Evaluate([Row("q", 20, zero: 10, clicked: 5, clicks: 6, positions: 18, previousSearches: 40, previousZero: 4, previousClicked: 20)], new SuiteInsightsOptions(), true)
            .Findings.Single(f => f.Kind == InsightKind.Regression);

        Assert.Equal((20L, 0.5, 0.25, 3.0, Fast, 40L, 0.1, 0.5), (finding.Searches, finding.NoResultRate, finding.Ctr, finding.AvgClickPosition, finding.LatencyP95, finding.PreviousSearches, finding.PreviousNoResultRate, finding.PreviousCtr));
        Assert.Empty(finding.AppliedSynonymIds);
        Assert.Empty(finding.FiredRules);
    }

    private static List<string> Kinds(InsightRow row) => Kinds([row]);

    private static List<string> Kinds(IEnumerable<InsightRow> rows, bool previousComplete = true, SuiteInsightsOptions? options = null) =>
        InsightEngine.Evaluate(rows, options ?? new SuiteInsightsOptions { MinAffected = 1 }, previousComplete).Findings
            .Select(f => $"{f.Kind} {f.QueryText} {f.Affected}")
            .ToList();

    private static InsightRow Row(
        string text, long searches, long zero = 0, long clicked = 0, long clicks = 0, long positions = 0, LatencyPercentile? p95 = null,
        long previousSearches = 0, long previousZero = 0, long previousClicked = 0) =>
        new(text, searches, zero, clicked, clicks, positions, p95 ?? Fast, previousSearches, previousZero, previousClicked);
}
