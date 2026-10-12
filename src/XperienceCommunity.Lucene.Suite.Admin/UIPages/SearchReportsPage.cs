using System.Globalization;

using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "reports",
    uiPageType: typeof(SearchReportsPage),
    name: "Search reports",
    templateName: "@xperience-community/lucene-suite/SearchReports",
    order: 50,
    Icon = Icons.Graph)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Search analytics of one index over a UTC day range, read from the daily rollups.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SearchReportsPage(
    ILuceneIndexManager indexManager,
    IServiceProvider serviceProvider,
    IOptions<SuiteSearchOptions> options,
    TimeProvider clock) : Page<SearchReportsClientProperties>
{
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>Identifier of the reported Lucene index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <summary>Repeats the section's index check: Kentico validates only the requested page.</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override Task<SearchReportsClientProperties> ConfigureTemplateProperties(SearchReportsClientProperties properties)
    {
        properties.Today = Day(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        properties.MaxDays = (int)options.Value.Analytics.Retention.DailyRetention.TotalDays;
        return Task.FromResult(properties);
    }

    /// <summary>Overview, trend and latency of the page's index; invalid ranges come back as <see cref="SearchReportsResult.Error"/>.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public Task<ICommandResponse<SearchReportsResult>> Report(SearchReportsArguments arguments)
    {
        string indexName = (indexManager.GetIndex(IndexIdentifier) ?? throw new InvalidOperationException($"Lucene index {IndexIdentifier} does not exist.")).IndexName;
        return Task.FromResult(ResponseFrom(Build(serviceProvider.GetRequiredService<SearchReportService>(), indexName, arguments)));
    }

    /// <summary>Runs the three report sections and maps them to the client shape.</summary>
    internal static SearchReportsResult Build(SearchReportService reports, string indexName, SearchReportsArguments arguments)
    {
        if (!TryDay(arguments.From, out var from) || !TryDay(arguments.To, out var to))
        {
            return new SearchReportsResult { Error = "From and To must be dates in the yyyy-MM-dd format." };
        }

        try
        {
            var overview = reports.Overview(indexName, from, to);
            var trend = reports.Trend(indexName, from, to);
            var latency = reports.Latency(indexName, from, to);
            var previous = new ReportRange(indexName, from, to).Previous;
            return new SearchReportsResult
            {
                From = Day(from),
                To = Day(to),
                PreviousFrom = Day(previous.From),
                PreviousTo = Day(previous.To),
                Current = Totals(overview.Current),
                Previous = Totals(overview.Previous),
                Deltas = new SearchReportsDeltas
                {
                    Searches = overview.Deltas.Searches,
                    ZeroResultSearches = overview.Deltas.ZeroResultSearches,
                    ClickedSearches = overview.Deltas.ClickedSearches,
                    Clicks = overview.Deltas.Clicks,
                    Conversions = overview.Deltas.Conversions,
                    SessionDays = overview.Deltas.SessionDays,
                },
                PreviousIsPartial = overview.PreviousIsPartial,
                FirstDataDay = overview.FirstDataDay is { } first ? Day(first) : null,
                CompleteThrough = overview.CompleteThrough is { } complete ? Day(complete) : null,
                Days = trend.Days.Select(day => new SearchReportsDay
                {
                    Day = Day(day.Day),
                    Searches = day.Searches,
                    ZeroResultSearches = day.ZeroResultSearches,
                    ClickedSearches = day.ClickedSearches,
                    Ctr = day.Ctr,
                    IsComplete = day.IsComplete,
                }).ToList(),
                Latency = new SearchReportsLatency
                {
                    P50 = Percentile(latency.P50),
                    P95 = Percentile(latency.P95),
                    P99 = Percentile(latency.P99),
                    Buckets = latency.Buckets,
                    BucketUpperBoundsMs = DailyKpi.LatencyBucketUpperBoundsMs,
                },
            };
        }
        catch (ArgumentException exception)
        {
            return new SearchReportsResult { Error = QueryTesterPage.ValidationMessage(exception) };
        }
    }

    private static SearchReportsTotals Totals(ReportTotals totals) => new()
    {
        Searches = totals.Searches,
        ZeroResultSearches = totals.ZeroResultSearches,
        SubmittedSearches = totals.SubmittedSearches,
        ClickedSearches = totals.ClickedSearches,
        Clicks = totals.Clicks,
        ConvertedSearches = totals.ConvertedSearches,
        Conversions = totals.Conversions,
        SessionDays = totals.SessionDays,
        NoResultRate = totals.NoResultRate,
        Ctr = totals.Ctr,
        ConversionRate = totals.ConversionRate,
        AvgClickPosition = totals.AvgClickPosition,
    };

    private static SearchReportsPercentile? Percentile(LatencyPercentile? percentile) =>
        percentile is null ? null : new SearchReportsPercentile { Ms = percentile.Ms, IsOverflow = percentile.IsOverflow };

    private static bool TryDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static string Day(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);
}

/// <summary>Template properties of the reports page.</summary>
public sealed class SearchReportsClientProperties : TemplateClientProperties
{
    /// <summary>Today's UTC day (yyyy-MM-dd) on the server; the latest selectable day.</summary>
    public string Today { get; set; } = string.Empty;

    /// <summary>Longest selectable range in days (Retention:DailyRetention).</summary>
    public int MaxDays { get; set; }
}

/// <summary>Requested inclusive UTC day range (yyyy-MM-dd).</summary>
public sealed class SearchReportsArguments
{
    /// <summary>First day (yyyy-MM-dd, UTC).</summary>
    public string? From { get; set; }

    /// <summary>Last day (yyyy-MM-dd, UTC), inclusive.</summary>
    public string? To { get; set; }
}

/// <summary>Report sections for one range; only <see cref="Error"/> is set when the request is invalid.</summary>
public sealed class SearchReportsResult
{
    /// <summary>Validation message for an invalid range; nothing else is set.</summary>
    public string? Error { get; set; }

    /// <summary>First reported day.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Last reported day.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>First day of the equal-length previous period.</summary>
    public string PreviousFrom { get; set; } = string.Empty;

    /// <summary>Last day of the previous period.</summary>
    public string PreviousTo { get; set; } = string.Empty;

    /// <summary>Totals of the range.</summary>
    public SearchReportsTotals? Current { get; set; }

    /// <summary>Totals of the previous period.</summary>
    public SearchReportsTotals? Previous { get; set; }

    /// <summary>Relative changes of the counts; rate changes are percentage points, computed by the client from Current and Previous.</summary>
    public SearchReportsDeltas? Deltas { get; set; }

    /// <summary>The previous period starts before the first data; no comparison is meaningful.</summary>
    public bool PreviousIsPartial { get; set; }

    /// <summary>Earliest day with data for the index.</summary>
    public string? FirstDataDay { get; set; }

    /// <summary>Last rolled-up day; later days are not final.</summary>
    public string? CompleteThrough { get; set; }

    /// <summary>One entry per day of the range.</summary>
    public IReadOnlyList<SearchReportsDay> Days { get; set; } = [];

    /// <summary>Latency percentiles and histogram of the range.</summary>
    public SearchReportsLatency? Latency { get; set; }
}

/// <summary>Summed counts and their rates (null when the denominator is zero).</summary>
public sealed class SearchReportsTotals
{
    /// <summary>Logical searches.</summary>
    public long Searches { get; set; }

    /// <summary>Searches without results.</summary>
    public long ZeroResultSearches { get; set; }

    /// <summary>Explicitly submitted searches.</summary>
    public long SubmittedSearches { get; set; }

    /// <summary>Searches with at least one click.</summary>
    public long ClickedSearches { get; set; }

    /// <summary>Attributed clicks.</summary>
    public long Clicks { get; set; }

    /// <summary>Searches with at least one conversion.</summary>
    public long ConvertedSearches { get; set; }

    /// <summary>Attributed conversions.</summary>
    public long Conversions { get; set; }

    /// <summary>Sum of daily distinct sessions.</summary>
    public long SessionDays { get; set; }

    /// <summary>Zero-result searches ÷ searches.</summary>
    public double? NoResultRate { get; set; }

    /// <summary>Clicked searches ÷ searches.</summary>
    public double? Ctr { get; set; }

    /// <summary>Converted searches ÷ searches.</summary>
    public double? ConversionRate { get; set; }

    /// <summary>Sum of click positions ÷ clicks.</summary>
    public double? AvgClickPosition { get; set; }
}

/// <summary>Relative change (current − previous) ÷ previous per count; null without a usable previous value.</summary>
public sealed class SearchReportsDeltas
{
    /// <summary>Change of searches.</summary>
    public double? Searches { get; set; }

    /// <summary>Change of zero-result searches.</summary>
    public double? ZeroResultSearches { get; set; }

    /// <summary>Change of clicked searches.</summary>
    public double? ClickedSearches { get; set; }

    /// <summary>Change of clicks.</summary>
    public double? Clicks { get; set; }

    /// <summary>Change of conversions.</summary>
    public double? Conversions { get; set; }

    /// <summary>Change of session-days.</summary>
    public double? SessionDays { get; set; }
}

/// <summary>Counts of one UTC day.</summary>
public sealed class SearchReportsDay
{
    /// <summary>The day (yyyy-MM-dd).</summary>
    public string Day { get; set; } = string.Empty;

    /// <summary>Logical searches.</summary>
    public long Searches { get; set; }

    /// <summary>Searches without results.</summary>
    public long ZeroResultSearches { get; set; }

    /// <summary>Searches with at least one click.</summary>
    public long ClickedSearches { get; set; }

    /// <summary>Clicked searches ÷ searches; null without searches.</summary>
    public double? Ctr { get; set; }

    /// <summary>Whether the day is rolled up (final).</summary>
    public bool IsComplete { get; set; }
}

/// <summary>Latency of the range from the fixed histogram.</summary>
public sealed class SearchReportsLatency
{
    /// <summary>Median.</summary>
    public SearchReportsPercentile? P50 { get; set; }

    /// <summary>95th percentile.</summary>
    public SearchReportsPercentile? P95 { get; set; }

    /// <summary>99th percentile.</summary>
    public SearchReportsPercentile? P99 { get; set; }

    /// <summary>Searches per latency bucket (10 buckets).</summary>
    public IReadOnlyList<long> Buckets { get; set; } = [];

    /// <summary>Upper bounds (ms) of buckets 0–8; the last bucket is the overflow.</summary>
    public IReadOnlyList<int> BucketUpperBoundsMs { get; set; } = [];
}

/// <summary>A histogram percentile: a bucket upper bound, or overflow with no value.</summary>
public sealed class SearchReportsPercentile
{
    /// <summary>Bucket upper bound in milliseconds; null for the overflow bucket.</summary>
    public int? Ms { get; set; }

    /// <summary>Above the last bucket bound.</summary>
    public bool IsOverflow { get; set; }
}
