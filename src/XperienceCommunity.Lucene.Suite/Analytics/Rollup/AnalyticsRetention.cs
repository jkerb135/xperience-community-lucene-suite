using System.Globalization;

using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Deletes expired raw and daily rows in bounded batches and folds sub-threshold queries into each day's "(other)" row.</summary>
internal sealed class AnalyticsRetention(
    IInfoProvider<SearchEventInfo> searchEvents,
    IInfoProvider<SearchInteractionInfo> interactions,
    IInfoProvider<SearchDailyInfo> daily,
    IInfoProvider<ItemDailyInfo> itemDaily,
    ILogger<AnalyticsRetention> logger)
{
    // Two parameters per folded group; SQL Server allows 2100 per command.
    private const int MaxFoldGroupsPerBatch = 1000;

    private static readonly string[] IntCountColumns =
    [
        nameof(SearchDailyInfo.Searches), nameof(SearchDailyInfo.ZeroResultSearches), nameof(SearchDailyInfo.SubmittedSearches),
        nameof(SearchDailyInfo.ClickedSearches), nameof(SearchDailyInfo.Clicks), nameof(SearchDailyInfo.ConvertedSearches),
        nameof(SearchDailyInfo.Conversions), nameof(SearchDailyInfo.SessionCount),
        .. Enumerable.Range(0, SearchDailyInfo.LatencyBucketCount).Select(i => $"LatencyBucket{i}"),
    ];

    /// <summary>Runs raw purge, daily (query and item) purge and the fold, in that order; <paramref name="watermark"/> is the last rolled day.</summary>
    public void Run(DateTimeOffset now, DateOnly? watermark, SuiteAnalyticsOptions options)
    {
        PurgeRaw(now, watermark, options);
        var today = RollupScheduler.Midnight(DateOnly.FromDateTime(now.UtcDateTime));
        var dailyCut = RollupScheduler.Midnight(RollupScheduler.CeilingDay(today - options.Retention.DailyRetention));
        DeleteBatched(daily, nameof(SearchDailyInfo.SearchDailyID), new WhereCondition().WhereLessThan(nameof(SearchDailyInfo.Day), dailyCut), options.BatchSize);
        DeleteBatched(itemDaily, nameof(ItemDailyInfo.ItemDailyID), new WhereCondition().WhereLessThan(nameof(ItemDailyInfo.Day), dailyCut), options.BatchSize);
        Fold(RollupScheduler.CeilingDay(today - options.Retention.SubThresholdPruneAfter), options);
    }

    /// <summary>Raw cut-offs (searches, interactions): rows received before them are no longer needed by any future rollup or by retention.</summary>
    internal static (DateTime Searches, DateTime Interactions) RawCutoffs(DateTimeOffset now, DateOnly? watermark, SuiteAnalyticsOptions options)
    {
        if (watermark is not DateOnly last)
        {
            return (DateTime.MinValue, DateTime.MinValue);
        }

        var retained = now.UtcDateTime - options.Retention.RawRetention;
        var nextDay = RollupScheduler.Midnight(last.AddDays(1));
        var window = options.Attribution.AttributionWindow;
        return (Min(retained, nextDay - window), Min(retained, nextDay - (2 * window)));
    }

    private void PurgeRaw(DateTimeOffset now, DateOnly? watermark, SuiteAnalyticsOptions options)
    {
        var (searchCut, interactionCut) = RawCutoffs(now, watermark, options);
        int batch = options.BatchSize;
        DeleteRaw(searchCut, interactionCut, batch);

        var hardCut = now.UtcDateTime - options.Retention.RawHardLimit;
        if (hardCut > searchCut || hardCut > interactionCut)
        {
            int deleted = DeleteRaw(Max(searchCut, hardCut), Max(interactionCut, hardCut), batch);
            if (deleted > 0)
            {
                logger.LogError(
                    "Lucene Suite deleted {Count} raw analytics rows older than Retention:RawHardLimit ({Limit}) before they were rolled up; the rollup is stuck at day {Day}.",
                    deleted,
                    options.Retention.RawHardLimit,
                    watermark?.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(no day rolled yet)");
            }
        }
    }

    private int DeleteRaw(DateTime searchCut, DateTime interactionCut, int batch) =>
        DeleteBatched(searchEvents, nameof(SearchEventInfo.SearchEventID), new WhereCondition().WhereLessThan(nameof(SearchEventInfo.ReceivedUtc), searchCut), batch)
        + DeleteBatched(interactions, nameof(SearchInteractionInfo.SearchInteractionID), new WhereCondition().WhereLessThan(nameof(SearchInteractionInfo.ReceivedUtc), interactionCut), batch);

    private void Fold(DateOnly lastSeenBefore, SuiteAnalyticsOptions options)
    {
        var surfacing = options.Surfacing;
        var groups = daily.Get()
            .Columns(nameof(SearchDailyInfo.IndexName), nameof(SearchDailyInfo.QueryText))
            .WhereNotEmpty(nameof(SearchDailyInfo.QueryText))
            .GroupBy(nameof(SearchDailyInfo.IndexName), nameof(SearchDailyInfo.QueryText))
            // Rows are unique per (Day, IndexName, QueryText), so COUNT(*) is the number of distinct days. Values are a date and ints, not input.
            .Having(string.Create(
                CultureInfo.InvariantCulture,
                $"MAX([Day]) < '{lastSeenBefore:yyyyMMdd}' AND (SUM([Searches]) < {surfacing.MinSearches} OR COUNT(*) < {surfacing.MinDistinctDays})"))
            .GetEnumerableTypedResult()
            .Select(group => (group.IndexName, group.QueryText))
            .ToList();

        foreach (var batch in groups.Chunk(Math.Min(options.BatchSize, MaxFoldGroupsPerBatch)))
        {
            FoldBatch(batch);
        }
    }

    private void FoldBatch((string IndexName, string QueryText)[] groups)
    {
        using var scope = new CMSTransactionScope();
        var where = new WhereCondition();
        foreach (var (indexName, queryText) in groups)
        {
            where.Or(new WhereCondition()
                .WhereEquals(nameof(SearchDailyInfo.IndexName), indexName)
                .WhereEquals(nameof(SearchDailyInfo.QueryText), queryText));
        }

        var rows = daily.Get().Where(where).GetEnumerableTypedResult().ToList();
        var others = daily.Get()
            .WhereNull(nameof(SearchDailyInfo.QueryText))
            .WhereIn(nameof(SearchDailyInfo.IndexName), rows.Select(row => row.IndexName).Distinct(StringComparer.Ordinal).ToList())
            .WhereGreaterOrEquals(nameof(SearchDailyInfo.Day), rows.Min(row => row.Day))
            .WhereLessOrEquals(nameof(SearchDailyInfo.Day), rows.Max(row => row.Day))
            .GetEnumerableTypedResult()
            .ToDictionary(other => (other.Day, other.IndexName));

        var created = new Dictionary<(DateTime, string), SearchDailyInfo>();
        var updated = new HashSet<SearchDailyInfo>();
        foreach (var row in rows)
        {
            var key = (row.Day, row.IndexName);
            if (others.TryGetValue(key, out var other))
            {
                updated.Add(other);
            }
            else if (!created.TryGetValue(key, out other))
            {
                created[key] = other = NewOther(row.Day, row.IndexName);
            }

            AddCounts(other, row);
        }

        foreach (var other in updated)
        {
            daily.Set(other);
        }

        if (created.Count > 0)
        {
            daily.BulkInsert(created.Values);
        }

        daily.BulkDelete(new WhereCondition().WhereIn(nameof(SearchDailyInfo.SearchDailyID), rows.Select(row => row.SearchDailyID).ToList()));
        scope.Commit();
    }

    private static SearchDailyInfo NewOther(DateTime day, string indexName)
    {
        var other = new SearchDailyInfo { Day = day, IndexName = indexName, ClickPositionSum = 0 };
        // NULL marks the "(other)" bucket; '' is the browse bucket.
        other.SetValue(nameof(SearchDailyInfo.QueryText), null);
        foreach (string column in IntCountColumns)
        {
            other.SetValue(column, 0);
        }

        return other;
    }

    private static void AddCounts(SearchDailyInfo target, SearchDailyInfo row)
    {
        foreach (string column in IntCountColumns)
        {
            target.SetValue(column, Int(target, column) + Int(row, column));
        }

        target.ClickPositionSum += row.ClickPositionSum;
    }

    private static int Int(SearchDailyInfo info, string column) => ValidationHelper.GetInteger(info.GetValue(column), 0, CultureInfo.InvariantCulture);

    private static int DeleteBatched<TInfo>(IInfoProvider<TInfo> provider, string idColumn, IWhereCondition where, int batchSize)
        where TInfo : AbstractInfoBase<TInfo>, new()
    {
        int total = 0;
        while (true)
        {
            var ids = provider.Get().TopN(batchSize).Column(idColumn).Where(where).GetListResult<int>();
            if (ids.Count > 0)
            {
                provider.BulkDelete(new WhereCondition().WhereIn(idColumn, ids));
                total += ids.Count;
            }

            if (ids.Count < batchSize)
            {
                return total;
            }
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
