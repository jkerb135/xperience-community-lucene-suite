using System.Globalization;

using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Suggestions;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Reports;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Suggestions;

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
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : Page<SearchReportsClientProperties>
{
    /// <summary>Rows per page of the query table.</summary>
    internal const int QueryPageSize = 25;

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>Identifier of the reported Lucene index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <summary>Repeats the section's index check: Kentico validates only the requested page.</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override async Task<SearchReportsClientProperties> ConfigureTemplateProperties(SearchReportsClientProperties properties)
    {
        var section = new PageParameterValues { { typeof(SearchTuningSectionPage), IndexIdentifier } };
        properties.Today = Day(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        properties.MaxDays = (int)options.Value.Analytics.Retention.DailyRetention.TotalDays;
        properties.CanManageRules = (await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_RULES)).Succeeded;
        properties.CanManageSynonyms = (await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_SYNONYMS)).Succeeded;
        properties.QueryTesterPath = pageLinkGenerator.GetPath<QueryTesterPage>(section);
        properties.RuleCreatePath = pageLinkGenerator.GetPath<RuleCreatePage>(section);
        properties.SynonymCreatePath = pageLinkGenerator.GetPath<SynonymCreatePage>(section);
        return properties;
    }

    /// <summary>Overview, trend and latency of the page's index; invalid ranges come back as <see cref="SearchReportsResult.Error"/>.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public Task<ICommandResponse<SearchReportsResult>> Report(SearchReportsArguments arguments)
    {
        string indexName = (indexManager.GetIndex(IndexIdentifier) ?? throw new InvalidOperationException($"Lucene index {IndexIdentifier} does not exist.")).IndexName;
        return Task.FromResult(ResponseFrom(Build(serviceProvider.GetRequiredService<SearchReportService>(), indexName, arguments)));
    }

    /// <summary>One page of surfaced queries plus the (other) total; invalid input comes back as <see cref="SearchReportsQueriesResult.Error"/>.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public Task<ICommandResponse<SearchReportsQueriesResult>> Queries(SearchReportsQueriesArguments arguments)
    {
        string indexName = (indexManager.GetIndex(IndexIdentifier) ?? throw new InvalidOperationException($"Lucene index {IndexIdentifier} does not exist.")).IndexName;
        return Task.FromResult(ResponseFrom(BuildQueries(serviceProvider.GetRequiredService<SearchReportService>(), indexName, arguments)));
    }

    /// <summary>Keeps a reported query out of this index's suggestions (exact match); the text is re-validated, the index never comes from the client.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_SYNONYMS)]
    public Task<ICommandResponse<BlockSuggestionResult>> BlockSuggestion(BlockSuggestionArguments arguments)
    {
        var result = Block(serviceProvider.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>(), indexManager.GetIndex(IndexIdentifier)?.IndexName, arguments.Query);
        var response = ResponseFrom(result);
        // Errors are shown once, in the page's alert region; success is a toast.
        return Task.FromResult(result.Error is null ? response.AddSuccessMessage(result.Message!) : response);
    }

    /// <summary>Adds an exact blocklist entry unless one exists; a null <paramref name="indexName"/> means the index is gone.</summary>
    internal static BlockSuggestionResult Block(IInfoProvider<BlockedSuggestionInfo> blockedSuggestions, string? indexName, string? query)
    {
        if (string.IsNullOrEmpty(indexName))
        {
            return new BlockSuggestionResult { Error = BlockedSuggestionValidation.IndexMissingMessage };
        }

        var (entry, error) = BlockedSuggestionValidation.Validate(query, BlockedSuggestionInfo.MatchModeExact);
        if (entry is null)
        {
            return new BlockSuggestionResult { Error = error };
        }

        if (BlockedSuggestionValidation.Exists(blockedSuggestions, indexName, entry.MatchKey, entry.MatchMode))
        {
            return new BlockSuggestionResult { AlreadyBlocked = true, Message = $"\"{entry.Text}\" is already blocked." };
        }

        BlockedSuggestionValidation.Add(blockedSuggestions, indexName, entry);
        return new BlockSuggestionResult
        {
            Blocked = true,
            Message = $"Suggestions for this index no longer include \"{entry.Text}\". Remove it under Blocked suggestions to undo.",
        };
    }

    /// <summary>Maps one page of <see cref="SearchReportService.Queries"/> to the client shape.</summary>
    internal static SearchReportsQueriesResult BuildQueries(SearchReportService reports, string indexName, SearchReportsQueriesArguments arguments)
    {
        if (!TryDay(arguments.From, out var from) || !TryDay(arguments.To, out var to))
        {
            return new SearchReportsQueriesResult { Error = "From and To must be dates in the yyyy-MM-dd format." };
        }

        // Enum.TryParse also accepts numbers; only the names are part of the contract.
        if (int.TryParse(arguments.Sort, out _) || !Enum.TryParse<QuerySort>(arguments.Sort, ignoreCase: true, out var sort) || !Enum.IsDefined(sort))
        {
            return new SearchReportsQueriesResult { Error = $"Unknown sort '{arguments.Sort}'." };
        }

        if (arguments.Page < 1)
        {
            return new SearchReportsQueriesResult { Error = "Page must be 1 or higher." };
        }

        try
        {
            long skip = (long)(arguments.Page - 1) * QueryPageSize;
            var page = reports.Queries(indexName, from, to, sort, (int)Math.Min(skip, int.MaxValue), QueryPageSize);
            return new SearchReportsQueriesResult
            {
                Rows = page.Rows.Select(Row).ToList(),
                Other = page.Other is { } other ? Row(other) : null,
                TotalCount = page.TotalCount,
                PageSize = QueryPageSize,
            };
        }
        catch (ArgumentException exception)
        {
            return new SearchReportsQueriesResult { Error = QueryTesterPage.ValidationMessage(exception) };
        }
    }

    private static SearchReportsQueryRow Row(ReportQueryRow row) => new()
    {
        QueryText = row.QueryText,
        IsBrowse = row.IsBrowse,
        HasPlaceholder = row.QueryText is { } text
            && (text.Contains(QueryScrubber.EmailPlaceholder, StringComparison.Ordinal) || text.Contains(QueryScrubber.NumberPlaceholder, StringComparison.Ordinal)),
        Searches = row.Searches,
        ZeroResultSearches = row.ZeroResultSearches,
        ClickedSearches = row.ClickedSearches,
        Ctr = row.Ctr,
        NoResultRate = row.NoResultRate,
        AvgClickPosition = row.AvgClickPosition,
        LatencyP50 = Percentile(row.LatencyP50),
        LatencyP95 = Percentile(row.LatencyP95),
    };

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
                    ConvertedSearches = day.ConvertedSearches,
                    Clicks = day.Clicks,
                    ClickPositionSum = day.ClickPositionSum,
                    SessionDays = day.SessionDays,
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
        ClickPositionSum = totals.ClickPositionSum,
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

    /// <summary>Whether the user may create rules (Site Search → Manage rules); only enables the row action, the target page enforces it.</summary>
    public bool CanManageRules { get; set; }

    /// <summary>Whether the user may create synonyms (Site Search → Manage synonyms); only enables the row action.</summary>
    public bool CanManageSynonyms { get; set; }

    /// <summary>Admin-root-relative path of this index's query tester.</summary>
    public string QueryTesterPath { get; set; } = string.Empty;

    /// <summary>Admin-root-relative path of this index's Create rule page.</summary>
    public string RuleCreatePath { get; set; } = string.Empty;

    /// <summary>Admin-root-relative path of this index's Create synonym page.</summary>
    public string SynonymCreatePath { get; set; } = string.Empty;
}

/// <summary>Query text to keep out of the index's suggestions.</summary>
public sealed class BlockSuggestionArguments
{
    /// <summary>The reported query text; validated again on the server.</summary>
    public string? Query { get; set; }
}

/// <summary>Outcome of <see cref="SearchReportsPage.BlockSuggestion"/>; <see cref="Error"/> is set when nothing was stored for a reason other than an existing entry.</summary>
public sealed class BlockSuggestionResult
{
    /// <summary>A new entry was stored.</summary>
    public bool Blocked { get; set; }

    /// <summary>An exact entry for the text already existed; nothing was stored.</summary>
    public bool AlreadyBlocked { get; set; }

    /// <summary>Why the text was not blocked (validation or a missing index).</summary>
    public string? Error { get; set; }

    /// <summary>Success message, also sent as the command's message.</summary>
    public string? Message { get; set; }
}

/// <summary>Requested query-table page.</summary>
public sealed class SearchReportsQueriesArguments
{
    /// <summary>First day (yyyy-MM-dd, UTC).</summary>
    public string? From { get; set; }

    /// <summary>Last day (yyyy-MM-dd, UTC), inclusive.</summary>
    public string? To { get; set; }

    /// <summary>Searches, NoResults, LowestCtr or Slowest.</summary>
    public string? Sort { get; set; }

    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One page of surfaced queries; only <see cref="Error"/> is set when the request is invalid.</summary>
public sealed class SearchReportsQueriesResult
{
    /// <summary>Validation message for an invalid request; nothing else is set.</summary>
    public string? Error { get; set; }

    /// <summary>The page's rows (surfaced query texts only).</summary>
    public IReadOnlyList<SearchReportsQueryRow> Rows { get; set; } = [];

    /// <summary>Everything in range that is not a named row; null when empty.</summary>
    public SearchReportsQueryRow? Other { get; set; }

    /// <summary>Named rows before paging.</summary>
    public int TotalCount { get; set; }

    /// <summary>Rows per page.</summary>
    public int PageSize { get; set; }
}

/// <summary>One query table row.</summary>
public sealed class SearchReportsQueryRow
{
    /// <summary>Surfaced query text; null for the (other) row.</summary>
    public string? QueryText { get; set; }

    /// <summary>The empty-text browse row.</summary>
    public bool IsBrowse { get; set; }

    /// <summary>The text contains an analytics placeholder ({email}, {number}); a rule or synonym from it would never match.</summary>
    public bool HasPlaceholder { get; set; }

    /// <summary>Logical searches.</summary>
    public long Searches { get; set; }

    /// <summary>Searches without results.</summary>
    public long ZeroResultSearches { get; set; }

    /// <summary>Searches with at least one click.</summary>
    public long ClickedSearches { get; set; }

    /// <summary>Clicked searches ÷ searches.</summary>
    public double? Ctr { get; set; }

    /// <summary>Zero-result searches ÷ searches.</summary>
    public double? NoResultRate { get; set; }

    /// <summary>Sum of click positions ÷ clicks.</summary>
    public double? AvgClickPosition { get; set; }

    /// <summary>Median latency bucket.</summary>
    public SearchReportsPercentile? LatencyP50 { get; set; }

    /// <summary>95th-percentile latency bucket.</summary>
    public SearchReportsPercentile? LatencyP95 { get; set; }
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

    /// <summary>Sum of clicked result positions (1-based); ÷ clicks = average click position.</summary>
    public long ClickPositionSum { get; set; }

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

    /// <summary>Searches with at least one conversion.</summary>
    public long ConvertedSearches { get; set; }

    /// <summary>Result clicks.</summary>
    public long Clicks { get; set; }

    /// <summary>Sum of clicked result positions (1-based); ÷ clicks = average click position.</summary>
    public long ClickPositionSum { get; set; }

    /// <summary>Distinct sessions of the day.</summary>
    public long SessionDays { get; set; }

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
