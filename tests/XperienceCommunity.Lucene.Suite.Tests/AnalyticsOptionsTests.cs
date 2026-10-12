using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class AnalyticsOptionsTests
{
    [Fact]
    public void Defaults_AreValid_AndMatchTheDocumentedValues()
    {
        var options = Resolve([]);
        var a = options.Analytics;

        Assert.Equal(
            (true, 10_000, TimeSpan.FromSeconds(2), 500),
            (a.Enabled, a.QueueCapacity, a.FlushInterval, a.BatchSize));
        Assert.Equal(
            (TimeSpan.FromSeconds(30), 4, 8, 0.5),
            (a.Merge.TextChangeWindow, a.Merge.TypoOneEditMinLength, a.Merge.TypoTwoEditMinLength, a.Merge.DeepBackspaceRatio));
        Assert.Equal(TimeSpan.FromHours(1), a.Attribution.AttributionWindow);
        Assert.Equal(
            (TimeSpan.FromHours(48), TimeSpan.FromDays(762), TimeSpan.FromDays(30), TimeSpan.FromDays(7)),
            (a.Retention.RawRetention, a.Retention.DailyRetention, a.Retention.SubThresholdPruneAfter, a.Retention.RawHardLimit));
        Assert.Equal((5, 2), (a.Surfacing.MinSearches, a.Surfacing.MinDistinctDays));
        Assert.Equal((TimeSpan.FromMinutes(15), 7), (a.Job.RollupInterval, a.Job.MaxDaysPerRun));
        Assert.Equal((10d, 60), (options.Rules.MaxBoostFactor, options.Rules.CacheMinutes));
    }

    [Fact]
    public void EverySetting_BindsFromTheLuceneSuiteSection()
    {
        var options = Resolve(new()
        {
            ["LuceneSuite:FacetValuesPerDimension"] = "7",
            ["LuceneSuite:SearchFields:dancinggoat:title"] = "2.5",
            ["LuceneSuite:Analytics:Enabled"] = "false",
            ["LuceneSuite:Analytics:QueueCapacity"] = "11",
            ["LuceneSuite:Analytics:FlushInterval"] = "00:00:03",
            ["LuceneSuite:Analytics:BatchSize"] = "12",
            ["LuceneSuite:Analytics:Merge:TextChangeWindow"] = "00:00:45",
            ["LuceneSuite:Analytics:Merge:TypoOneEditMinLength"] = "5",
            ["LuceneSuite:Analytics:Merge:TypoTwoEditMinLength"] = "9",
            ["LuceneSuite:Analytics:Merge:DeepBackspaceRatio"] = "0.4",
            ["LuceneSuite:Analytics:Attribution:AttributionWindow"] = "02:00:00",
            ["LuceneSuite:Analytics:Retention:RawRetention"] = "3.00:00:00",
            ["LuceneSuite:Analytics:Retention:DailyRetention"] = "400.00:00:00",
            ["LuceneSuite:Analytics:Retention:SubThresholdPruneAfter"] = "10.00:00:00",
            ["LuceneSuite:Analytics:Retention:RawHardLimit"] = "9.00:00:00",
            ["LuceneSuite:Analytics:Surfacing:MinSearches"] = "6",
            ["LuceneSuite:Analytics:Surfacing:MinDistinctDays"] = "3",
            ["LuceneSuite:Analytics:Job:RollupInterval"] = "00:05:00",
            ["LuceneSuite:Analytics:Job:MaxDaysPerRun"] = "3",
            ["LuceneSuite:Rules:MaxBoostFactor"] = "4.5",
            ["LuceneSuite:Rules:CacheMinutes"] = "0",
        });
        var a = options.Analytics;

        Assert.Equal(7, options.FacetValuesPerDimension);
        Assert.Equal(2.5f, options.SearchFields["DancingGoat"]["title"]);
        Assert.Equal(
            (false, 11, TimeSpan.FromSeconds(3), 12),
            (a.Enabled, a.QueueCapacity, a.FlushInterval, a.BatchSize));
        Assert.Equal(
            (TimeSpan.FromSeconds(45), 5, 9, 0.4),
            (a.Merge.TextChangeWindow, a.Merge.TypoOneEditMinLength, a.Merge.TypoTwoEditMinLength, a.Merge.DeepBackspaceRatio));
        Assert.Equal(TimeSpan.FromHours(2), a.Attribution.AttributionWindow);
        Assert.Equal(
            (TimeSpan.FromDays(3), TimeSpan.FromDays(400), TimeSpan.FromDays(10), TimeSpan.FromDays(9)),
            (a.Retention.RawRetention, a.Retention.DailyRetention, a.Retention.SubThresholdPruneAfter, a.Retention.RawHardLimit));
        Assert.Equal((6, 3), (a.Surfacing.MinSearches, a.Surfacing.MinDistinctDays));
        Assert.Equal((TimeSpan.FromMinutes(5), 3), (a.Job.RollupInterval, a.Job.MaxDaysPerRun));
        Assert.Equal((4.5, 0), (options.Rules.MaxBoostFactor, options.Rules.CacheMinutes));
    }

    [Fact]
    public void CodeDelegate_AppliesAfterConfiguration()
    {
        var options = Resolve(
            new() { ["LuceneSuite:Analytics:BatchSize"] = "12", ["LuceneSuite:Analytics:QueueCapacity"] = "11" },
            o => o.Analytics.BatchSize = 99);

        Assert.Equal((99, 11), (options.Analytics.BatchSize, options.Analytics.QueueCapacity));
    }

    [Theory]
    [InlineData("QueueCapacity", "0")]
    [InlineData("BatchSize", "-1")]
    [InlineData("FlushInterval", "00:00:00")]
    [InlineData("Merge:TextChangeWindow", "-00:00:01")]
    [InlineData("Merge:TypoOneEditMinLength", "0")]
    [InlineData("Merge:TypoTwoEditMinLength", "4")]
    [InlineData("Merge:DeepBackspaceRatio", "0")]
    [InlineData("Merge:DeepBackspaceRatio", "1")]
    [InlineData("Merge:DeepBackspaceRatio", "NaN")]
    [InlineData("Attribution:AttributionWindow", "00:00:00")]
    [InlineData("Retention:RawRetention", "1.02:14:59")]
    [InlineData("Retention:DailyRetention", "23:59:59")]
    [InlineData("Retention:SubThresholdPruneAfter", "00:00:00")]
    [InlineData("Surfacing:MinSearches", "0")]
    [InlineData("Surfacing:MinDistinctDays", "0")]
    [InlineData("Job:RollupInterval", "00:00:00")]
    [InlineData("Job:MaxDaysPerRun", "0")]
    [InlineData("Retention:RawHardLimit", "1.23:59:59")]
    [InlineData("Insights:MinSearches", "0")]
    [InlineData("Insights:ContentGapMinNoResultRate", "-0.01")]
    [InlineData("Insights:ContentGapMinNoResultRate", "1.01")]
    [InlineData("Insights:ContentGapMinNoResultRate", "NaN")]
    [InlineData("Insights:RelevanceMissMaxCtr", "-0.01")]
    [InlineData("Insights:RelevanceMissMaxCtr", "1.01")]
    [InlineData("Insights:BuriedMinClickedSearches", "0")]
    [InlineData("Insights:BuriedMinAvgPosition", "1")]
    [InlineData("Insights:BuriedMinAvgPosition", "NaN")]
    [InlineData("Insights:SlowMinP95Ms", "0")]
    [InlineData("Insights:RegressionMinPoints", "-0.01")]
    [InlineData("Insights:RegressionMinPoints", "1.01")]
    [InlineData("Insights:MinAffected", "0")]
    [InlineData("Insights:MaxQueries", "0")]
    [InlineData("Insights:MaxQueries", "10001")]
    [InlineData("Insights:MaxFindings", "0")]
    [InlineData("Insights:MaxFindings", "1001")]
    public void InvalidSetting_FailsNamingTheSetting(string key, string value)
    {
        var error = Assert.Throws<OptionsValidationException>(() => Resolve(new() { ["LuceneSuite:Analytics:" + key] = value }));

        Assert.Contains(error.Failures, failure => failure.StartsWith("LuceneSuite:Analytics:" + key + " ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("MaxBoostFactor", "1")]
    [InlineData("MaxBoostFactor", "0.5")]
    [InlineData("MaxBoostFactor", "1000.01")]
    [InlineData("MaxBoostFactor", "NaN")]
    [InlineData("CacheMinutes", "-1")]
    [InlineData("CacheMinutes", "1441")]
    public void InvalidRuleSetting_FailsNamingTheSetting(string key, string value)
    {
        var error = Assert.Throws<OptionsValidationException>(() => Resolve(new() { ["LuceneSuite:Rules:" + key] = value }));

        Assert.Contains(error.Failures, failure => failure.StartsWith("LuceneSuite:Rules:" + key + " ", StringComparison.Ordinal));
    }

    [Fact]
    public void InsightDefaults_AreValid_AndASiteCanOverrideThem()
    {
        var i = Resolve([]).Analytics.Insights;
        Assert.Equal(
            (20, 0.3, 0.1, 5, 5.0, 1000, 0.10, 5, 1000, 200),
            (i.MinSearches, i.ContentGapMinNoResultRate, i.RelevanceMissMaxCtr, i.BuriedMinClickedSearches, i.BuriedMinAvgPosition, i.SlowMinP95Ms, i.RegressionMinPoints, i.MinAffected, i.MaxQueries, i.MaxFindings));

        Assert.Equal(5, Resolve(new() { ["LuceneSuite:Analytics:Insights:MinSearches"] = "5" }).Analytics.Insights.MinSearches);
        i = Resolve(new()
        {
            ["LuceneSuite:Analytics:Insights:MinSearches"] = "200",
            ["LuceneSuite:Analytics:Insights:ContentGapMinNoResultRate"] = "1",
            ["LuceneSuite:Analytics:Insights:RelevanceMissMaxCtr"] = "0",
            ["LuceneSuite:Analytics:Insights:BuriedMinClickedSearches"] = "20",
            ["LuceneSuite:Analytics:Insights:BuriedMinAvgPosition"] = "1.5",
            ["LuceneSuite:Analytics:Insights:SlowMinP95Ms"] = "1",
            ["LuceneSuite:Analytics:Insights:RegressionMinPoints"] = "0",
            ["LuceneSuite:Analytics:Insights:MinAffected"] = "25",
            ["LuceneSuite:Analytics:Insights:MaxQueries"] = "10000",
            ["LuceneSuite:Analytics:Insights:MaxFindings"] = "1000",
        }).Analytics.Insights;
        Assert.Equal(
            (200, 1.0, 0.0, 20, 1.5, 1, 0.0, 25, 10_000, 1_000),
            (i.MinSearches, i.ContentGapMinNoResultRate, i.RelevanceMissMaxCtr, i.BuriedMinClickedSearches, i.BuriedMinAvgPosition, i.SlowMinP95Ms, i.RegressionMinPoints, i.MinAffected, i.MaxQueries, i.MaxFindings));
    }

    [Fact]
    public void SuggestionDefaults_AreValid_AndBindFromTheSuggestionsSection()
    {
        var s = Resolve([]).Suggestions;
        Assert.Equal((90, 0.5, 5, 2000, 2, 8, 60), (s.WindowDays, s.MaxNoResultRate, s.MinSearches, s.MaxCandidates, s.MinInputLength, s.MaxResults, s.CacheMinutes));

        s = Resolve(new() { ["LuceneSuite:Suggestions:WindowDays"] = "762", ["LuceneSuite:Suggestions:MaxNoResultRate"] = "1", ["LuceneSuite:Suggestions:CacheMinutes"] = "0" }).Suggestions;
        Assert.Equal((762, 1.0, 0), (s.WindowDays, s.MaxNoResultRate, s.CacheMinutes));
    }

    [Theory]
    [InlineData("WindowDays", "0")]
    [InlineData("WindowDays", "763")]
    [InlineData("MaxNoResultRate", "-0.1")]
    [InlineData("MaxNoResultRate", "1.1")]
    [InlineData("MaxNoResultRate", "NaN")]
    [InlineData("MinSearches", "0")]
    [InlineData("MaxCandidates", "0")]
    [InlineData("MaxCandidates", "20001")]
    [InlineData("MinInputLength", "0")]
    [InlineData("MinInputLength", "21")]
    [InlineData("MaxResults", "0")]
    [InlineData("MaxResults", "21")]
    [InlineData("CacheMinutes", "-1")]
    [InlineData("CacheMinutes", "1441")]
    public void InvalidSuggestionSetting_FailsNamingTheSetting(string key, string value)
    {
        var error = Assert.Throws<OptionsValidationException>(() => Resolve(new() { ["LuceneSuite:Suggestions:" + key] = value }));

        Assert.Contains(error.Failures, failure => failure.StartsWith("LuceneSuite:Suggestions:" + key + " ", StringComparison.Ordinal));
    }

    [Fact]
    public void SuggestionWindow_IsCappedByDailyRetention()
    {
        Dictionary<string, string?> settings = new()
        {
            ["LuceneSuite:Analytics:Retention:DailyRetention"] = "30.00:00:00",
            ["LuceneSuite:Suggestions:WindowDays"] = "30",
        };
        Assert.Equal(30, Resolve(settings).Suggestions.WindowDays);

        settings["LuceneSuite:Suggestions:WindowDays"] = "31";
        Assert.Throws<OptionsValidationException>(() => Resolve(settings));
    }

    [Fact]
    public void RawRetention_MustKeepEveryDayRollableForOneIntervalPlusOneHour()
    {
        // 1 day + 3 × 2 h + 2 s flush + 5 min margin + 30 min + 1 h = 1.07:35:02.
        Dictionary<string, string?> settings = new()
        {
            ["LuceneSuite:Analytics:Attribution:AttributionWindow"] = "02:00:00",
            ["LuceneSuite:Analytics:Job:RollupInterval"] = "00:30:00",
            ["LuceneSuite:Analytics:Retention:RawRetention"] = "1.07:35:02",
        };
        Assert.Equal(TimeSpan.Parse("1.07:35:02"), Resolve(settings).Analytics.Retention.RawRetention);

        settings["LuceneSuite:Analytics:Retention:RawRetention"] = "1.07:35:01";
        Assert.Throws<OptionsValidationException>(() => Resolve(settings));
    }

    [Fact]
    public async Task InvalidSetting_FailsHostStartup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["LuceneSuite:Analytics:Merge:TypoTwoEditMinLength"] = "3" });
        builder.Services.AddLuceneSuite();
        // Only the options pipeline is under test; drop the writer, which needs Kentico.
        builder.Services.RemoveAll<IHostedService>();
        using var host = builder.Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(error.Failures, failure => failure.Contains("Merge:TypoTwoEditMinLength", StringComparison.Ordinal));
    }

    private static SuiteSearchOptions Resolve(Dictionary<string, string?> settings, Action<SuiteSearchOptions>? configure = null)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .AddLuceneSuite(configure)
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<SuiteSearchOptions>>().Value;
    }
}
