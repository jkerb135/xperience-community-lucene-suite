using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SearchReportTests
{
    private static readonly DateOnly Today = new(2030, 3, 15);

    [Fact]
    public void Percentile_OfAnEmptyHistogram_IsNull() =>
        Assert.Null(LatencyPercentiles.Of(new long[10], 0.95m));

    [Fact]
    public void Percentile_OfASingleBucket_IsItsUpperBound()
    {
        var buckets = Buckets((3, 7));
        Assert.Equal(new LatencyPercentile(100, false), LatencyPercentiles.Of(buckets, 0.50m));
        Assert.Equal(new LatencyPercentile(100, false), LatencyPercentiles.Of(buckets, 0.99m));
    }

    [Fact]
    public void Percentile_RankExactlyAtABucketBoundary_StaysInThatBucket()
    {
        // 19 of 20 in bucket 0 (≤ 10 ms): p95 rank = ceil(0.95 × 20) = 19 → 10 ms; p99 rank 20 → bucket 1.
        var buckets = Buckets((0, 19), (1, 1));
        Assert.Equal(new LatencyPercentile(10, false), LatencyPercentiles.Of(buckets, 0.95m));
        Assert.Equal(new LatencyPercentile(25, false), LatencyPercentiles.Of(buckets, 0.99m));
    }

    [Fact]
    public void Percentile_InTheOverflowBucket_HasNoMsValue() =>
        Assert.Equal(new LatencyPercentile(null, true), LatencyPercentiles.Of(Buckets((2, 1), (9, 1)), 0.99m));

    [Fact]
    public void Percentile_P99OfOneSample_IsThatSample() =>
        Assert.Equal(new LatencyPercentile(5000, false), LatencyPercentiles.Of(Buckets((8, 1)), 0.99m));

    [Fact]
    public void Rates_WithZeroDenominators_AreNull()
    {
        var totals = Totals(searches: 0, clicks: 0);
        Assert.Null(totals.NoResultRate);
        Assert.Null(totals.Ctr);
        Assert.Null(totals.ConversionRate);
        Assert.Null(totals.AvgClickPosition);
    }

    [Fact]
    public void Rates_AreTheDocumentedRatios()
    {
        var totals = new ReportTotals(Searches: 10, ZeroResultSearches: 2, SubmittedSearches: 5, ClickedSearches: 4, Clicks: 5, ClickPositionSum: 15, ConvertedSearches: 1, Conversions: 3, SessionDays: 7, LatencyBuckets: new long[10]);
        Assert.Equal(0.2, totals.NoResultRate);
        Assert.Equal(0.4, totals.Ctr);
        Assert.Equal(0.1, totals.ConversionRate);
        Assert.Equal(3.0, totals.AvgClickPosition);
    }

    [Fact]
    public void Deltas_AreRelative_AndNullWhenThePreviousValueIsZeroOrNull()
    {
        var deltas = SearchReportService.Deltas(Totals(searches: 15, clicks: 0), Totals(searches: 10, clicks: 0), previousIsPartial: false);

        Assert.Equal(0.5, deltas.Searches);
        Assert.Null(deltas.Clicks);
        Assert.Null(deltas.AvgClickPosition);
    }

    [Fact]
    public void Deltas_OfAPartialPreviousPeriod_AreAllNull()
    {
        var deltas = SearchReportService.Deltas(Totals(searches: 15, clicks: 3), Totals(searches: 10, clicks: 2), previousIsPartial: true);
        Assert.Equal(new ReportDeltas(null, null, null, null, null, null, null, null, null, null), deltas);
    }

    [Theory]
    [InlineData("2030-03-01", "2030-03-31", "2030-01-29", "2030-02-28")]
    [InlineData("2030-03-01", "2030-03-01", "2030-02-28", "2030-02-28")]
    [InlineData("2028-03-01", "2028-03-07", "2028-02-23", "2028-02-29")]
    [InlineData("2030-01-01", "2030-01-10", "2029-12-22", "2029-12-31")]
    public void PreviousPeriod_IsTheEqualLengthRangeEndingTheDayBeforeFrom(string from, string to, string previousFrom, string previousTo)
    {
        var previous = new ReportRange("i", DateOnly.Parse(from), DateOnly.Parse(to)).Previous;
        Assert.Equal((DateOnly.Parse(previousFrom), DateOnly.Parse(previousTo)), (previous.From, previous.To));
    }

    [Fact]
    public void Validate_RejectsEachInvalidRequest_NamingTheParameter()
    {
        var service = Service(dailyRetentionDays: 30);

        Assert.Equal("indexName", Assert.ThrowsAny<ArgumentException>(() => service.Validate(" ", Today, Today)).ParamName);
        Assert.Equal("from", Assert.Throws<ArgumentException>(() => service.Validate("i", Today, Today.AddDays(-1))).ParamName);
        Assert.Equal("to", Assert.Throws<ArgumentException>(() => service.Validate("i", Today, Today.AddDays(1))).ParamName);
        Assert.Equal("from", Assert.Throws<ArgumentException>(() => service.Validate("i", Today.AddDays(-30), Today)).ParamName);
        Assert.Equal(30, service.Validate("i", Today.AddDays(-29), Today).Days);
    }

    private static SearchReportService Service(int dailyRetentionDays)
    {
        var options = new SuiteSearchOptions();
        options.Analytics.Retention.DailyRetention = TimeSpan.FromDays(dailyRetentionDays);
        return new SearchReportService(null!, null!, Options.Create(options), new ManualClock(new DateTimeOffset(Today.ToDateTime(new TimeOnly(23, 59)), TimeSpan.Zero)));
    }

    private static ReportTotals Totals(long searches, long clicks) => new(searches, 0, 0, 0, clicks, 0, 0, 0, 0, new long[10]);

    private static long[] Buckets(params (int Index, long Count)[] counts)
    {
        var buckets = new long[10];
        foreach (var (index, count) in counts)
        {
            buckets[index] = count;
        }

        return buckets;
    }
}
