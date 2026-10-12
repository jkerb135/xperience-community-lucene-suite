using System.Globalization;

using CMS.Base;
using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite.Analytics.Reports.Insights;

/// <summary>Ranked findings computed on read from <see cref="SearchDailyInfo"/>; writes nothing. Performs no authorization: callers gate on Site Search View and validate the index.</summary>
internal sealed class SearchInsightService(
    IInfoProvider<SearchDailyInfo> daily,
    IInfoProvider<RuleInfo> ruleInfos,
    SearchReportService reports,
    DailyRollupStore store,
    RuleProvider ruleProvider,
    SynonymProvider synonymProvider,
    IOptions<SuiteSearchOptions> options,
    TimeProvider clock,
    ILogger<SearchInsightService> logger)
{
    private const string FromParameter = "@InsightFrom";
    private const string PreviousPrefix = "Previous";

    private static readonly string[] CurrentColumns =
    [
        nameof(SearchDailyInfo.Searches), nameof(SearchDailyInfo.ZeroResultSearches), nameof(SearchDailyInfo.ClickedSearches),
        nameof(SearchDailyInfo.Clicks), nameof(SearchDailyInfo.ClickPositionSum),
        .. Enumerable.Range(0, SearchDailyInfo.LatencyBucketCount).Select(i => $"LatencyBucket{i}"),
    ];

    private static readonly string[] PreviousColumns =
        [nameof(SearchDailyInfo.Searches), nameof(SearchDailyInfo.ZeroResultSearches), nameof(SearchDailyInfo.ClickedSearches)];

    private readonly LogThrottle annotationThrottle = new(clock, TimeSpan.FromMinutes(1));

    public InsightReport Insights(string indexName, DateOnly from, DateOnly to)
    {
        var range = reports.Validate(indexName, from, to);
        var settings = options.Value.Analytics.Insights;
        var (_, previousIsPartial) = reports.PreviousPeriod(range);
        var (rows, truncated) = Aggregate(range);
        var (findings, omitted) = InsightEngine.Evaluate(rows, settings, !previousIsPartial);
        var (annotated, failed) = Annotate(range.IndexName, findings);
        return new InsightReport(annotated, omitted, rows.Count, truncated, store.ReadWatermark(), previousIsPartial, failed);
    }

    /// <summary>Surfaced, non-browse queries with at least MinSearches in the range, most searched first, at most MaxQueries.</summary>
    internal (IReadOnlyList<InsightRow> Rows, bool Truncated) Aggregate(ReportRange range)
    {
        int maxQueries = options.Value.Analytics.Insights.MaxQueries;
        var rows = AggregateQuery(range).GetDataContainerResult().Select(row => ToRow(row)).ToList();
        bool truncated = rows.Count > maxQueries;
        return (truncated ? rows[..maxQueries] : rows, truncated);
    }

    /// <summary>One grouped query over the range and the previous period, split by conditional sums on <c>[Day] &gt;= @InsightFrom</c>.</summary>
    internal ObjectQuery<SearchDailyInfo> AggregateQuery(ReportRange range)
    {
        var settings = options.Value.Analytics.Insights;
        const string searches = nameof(SearchDailyInfo.Searches);
        // Column expressions are SQL text: they hold only constant column names, the parameter name and validated ints.
        var query = daily.Get()
            .TopN(settings.MaxQueries + 1)
            .Columns(
            [
                new QueryColumn(nameof(SearchDailyInfo.QueryText)),
                .. CurrentColumns.Select(column => Sum($"CASE WHEN [Day] >= {FromParameter} THEN [{column}] ELSE 0 END", column)),
                .. PreviousColumns.Select(column => Sum($"CASE WHEN [Day] < {FromParameter} THEN [{column}] ELSE 0 END", PreviousPrefix + column)),
            ])
            .WhereEquals(nameof(SearchDailyInfo.IndexName), range.IndexName)
            .WhereGreaterOrEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(range.Previous.From))
            .WhereLessOrEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(range.To))
            .WhereNotEmpty(nameof(SearchDailyInfo.QueryText))
            .WhereIn(nameof(SearchDailyInfo.QueryText), Surfacing.Query(daily, range.IndexName, options.Value.Analytics.Surfacing))
            .GroupBy(nameof(SearchDailyInfo.QueryText))
            .Having(string.Create(CultureInfo.InvariantCulture, $"SUM(CASE WHEN [Day] >= {FromParameter} THEN [{searches}] ELSE 0 END) >= {settings.MinSearches}"))
            .OrderByDescending(searches)
            .OrderByAscending(nameof(SearchDailyInfo.QueryText));
        query.EnsureParameters().Add(FromParameter, RollupScheduler.Midnight(range.From));
        return query;
    }

    private static IQueryColumn Sum(string expression, string alias) => new AggregatedColumn(AggregationType.Sum, expression).As(alias);

    private static InsightRow ToRow(IDataContainer row)
    {
        long Get(string column) => ValidationHelper.GetLong(row.GetValue(column), 0, CultureInfo.InvariantCulture);
        var buckets = Enumerable.Range(0, SearchDailyInfo.LatencyBucketCount).Select(i => Get($"LatencyBucket{i}")).ToArray();
        return new InsightRow(
            ValidationHelper.GetString(row.GetValue(nameof(SearchDailyInfo.QueryText)), string.Empty, CultureInfo.InvariantCulture),
            Get(nameof(SearchDailyInfo.Searches)),
            Get(nameof(SearchDailyInfo.ZeroResultSearches)),
            Get(nameof(SearchDailyInfo.ClickedSearches)),
            Get(nameof(SearchDailyInfo.Clicks)),
            Get(nameof(SearchDailyInfo.ClickPositionSum)),
            LatencyPercentiles.Of(buckets, 0.95m),
            Get(PreviousPrefix + nameof(SearchDailyInfo.Searches)),
            Get(PreviousPrefix + nameof(SearchDailyInfo.ZeroResultSearches)),
            Get(PreviousPrefix + nameof(SearchDailyInfo.ClickedSearches)),
            buckets);
    }

    private (IReadOnlyList<InsightFinding> Findings, bool Failed) Annotate(string indexName, IReadOnlyList<InsightFinding> findings)
    {
        if (findings.Count == 0)
        {
            return (findings, false);
        }

        try
        {
            var synonyms = synonymProvider.GetSynonyms(indexName);
            var rules = ruleProvider.GetRules(indexName);
            int maxExpansions = options.Value.Synonyms.MaxExpansions;
            var now = clock.GetUtcNow().UtcDateTime;
            var empty = new Dictionary<string, IReadOnlyList<string>>();
            var noContexts = new HashSet<string>();
            // Facet and context conditions cannot match without facets or contexts, so such rules never show as firing.
            var byText = findings.Select(finding => finding.QueryText).Distinct(StringComparer.Ordinal).ToDictionary(
                text => text,
                text => (
                    Synonyms: SynonymEngine.Expand(synonyms, text, maxExpansions).Applied.Select(applied => applied.SynonymId).Distinct().Order().ToList(),
                    Rules: RuleEngine.Evaluate(rules, new RuleInput(indexName, text, empty, noContexts, now)).FiredRuleIds),
                StringComparer.Ordinal);

            var firedIds = byText.Values.SelectMany(value => value.Rules).Distinct().ToList();
            var names = firedIds.Count == 0
                ? []
                : ruleInfos.Get()
                    .WhereIn(nameof(RuleInfo.RuleID), firedIds)
                    .Columns(nameof(RuleInfo.RuleID), nameof(RuleInfo.DisplayName))
                    .GetEnumerableTypedResult()
                    .ToDictionary(rule => rule.RuleID, rule => rule.DisplayName);

            return (findings.Select(finding =>
            {
                var (synonymIds, ruleIds) = byText[finding.QueryText];
                return finding with
                {
                    AppliedSynonymIds = synonymIds,
                    FiredRules = ruleIds.Select(id => new InsightRule(id, names.GetValueOrDefault(id, string.Empty))).ToList(),
                };
            }).ToList(), false);
        }
        catch (Exception exception)
        {
            if (annotationThrottle.ShouldLog())
            {
                logger.LogWarning(exception, "Lucene Suite could not load or evaluate rules or synonyms for the insights of index {Index}; findings are listed without them (further failures suppressed for a minute).", indexName);
            }

            return (findings, true);
        }
    }
}
