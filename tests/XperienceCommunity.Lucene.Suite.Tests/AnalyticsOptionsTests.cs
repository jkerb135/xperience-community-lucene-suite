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
        var a = Resolve([]).Analytics;

        Assert.Equal(
            (true, 10_000, TimeSpan.FromSeconds(2), 500),
            (a.Enabled, a.QueueCapacity, a.FlushInterval, a.BatchSize));
        Assert.Equal(
            (TimeSpan.FromSeconds(30), 4, 8, 0.5),
            (a.Merge.TextChangeWindow, a.Merge.TypoOneEditMinLength, a.Merge.TypoTwoEditMinLength, a.Merge.DeepBackspaceRatio));
        Assert.Equal(TimeSpan.FromHours(1), a.Attribution.AttributionWindow);
        Assert.Equal(
            (TimeSpan.FromHours(48), TimeSpan.FromDays(762), TimeSpan.FromDays(30)),
            (a.Retention.RawRetention, a.Retention.DailyRetention, a.Retention.SubThresholdPruneAfter));
        Assert.Equal((5, 2), (a.Surfacing.MinSearches, a.Surfacing.MinDistinctDays));
        Assert.Equal(TimeSpan.FromMinutes(15), a.Job.RollupInterval);
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
            ["LuceneSuite:Analytics:Surfacing:MinSearches"] = "6",
            ["LuceneSuite:Analytics:Surfacing:MinDistinctDays"] = "3",
            ["LuceneSuite:Analytics:Job:RollupInterval"] = "00:05:00",
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
            (TimeSpan.FromDays(3), TimeSpan.FromDays(400), TimeSpan.FromDays(10)),
            (a.Retention.RawRetention, a.Retention.DailyRetention, a.Retention.SubThresholdPruneAfter));
        Assert.Equal((6, 3), (a.Surfacing.MinSearches, a.Surfacing.MinDistinctDays));
        Assert.Equal(TimeSpan.FromMinutes(5), a.Job.RollupInterval);
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
    public void InvalidSetting_FailsNamingTheSetting(string key, string value)
    {
        var error = Assert.Throws<OptionsValidationException>(() => Resolve(new() { ["LuceneSuite:Analytics:" + key] = value }));

        Assert.Contains(error.Failures, failure => failure.StartsWith("LuceneSuite:Analytics:" + key + " ", StringComparison.Ordinal));
    }

    [Fact]
    public void RawRetention_MustCoverADayPlusAttributionPlusRollupIntervalPlusOneHour()
    {
        // 1 day + 2 h + 30 min + 1 h = 1.03:30:00.
        Dictionary<string, string?> settings = new()
        {
            ["LuceneSuite:Analytics:Attribution:AttributionWindow"] = "02:00:00",
            ["LuceneSuite:Analytics:Job:RollupInterval"] = "00:30:00",
            ["LuceneSuite:Analytics:Retention:RawRetention"] = "1.03:30:00",
        };
        Assert.Equal(TimeSpan.Parse("1.03:30:00"), Resolve(settings).Analytics.Retention.RawRetention);

        settings["LuceneSuite:Analytics:Retention:RawRetention"] = "1.03:29:59";
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
