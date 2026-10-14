using System.Globalization;

using CMS.DataEngine;

namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Loads a day's raw rows per the engine input contract and replaces the day's <see cref="SearchDailyInfo"/> and <see cref="ItemDailyInfo"/> rows together with the watermark.</summary>
internal sealed class DailyRollupStore(
    IInfoProvider<SearchEventInfo> searchEvents,
    IInfoProvider<SearchInteractionInfo> interactions,
    IInfoProvider<SearchDailyInfo> daily,
    IInfoProvider<ItemDailyInfo> itemDaily,
    IInfoProvider<SettingsKeyInfo> settingsKeys)
{
    private const string DayFormat = "yyyy-MM-dd";

    private static readonly string[] SearchColumns =
    [
        nameof(SearchEventInfo.SearchEventID), nameof(SearchEventInfo.SearchId), nameof(SearchEventInfo.Sequence),
        nameof(SearchEventInfo.SessionId), nameof(SearchEventInfo.ReceivedUtc), nameof(SearchEventInfo.IndexName),
        nameof(SearchEventInfo.QueryText), nameof(SearchEventInfo.Page), nameof(SearchEventInfo.PageSize),
        nameof(SearchEventInfo.ResultCount), nameof(SearchEventInfo.LatencyMs), nameof(SearchEventInfo.IsSubmit),
    ];

    private static readonly string[] InteractionColumns =
    [
        nameof(SearchInteractionInfo.SearchInteractionID), nameof(SearchInteractionInfo.SearchId), nameof(SearchInteractionInfo.Sequence),
        nameof(SearchInteractionInfo.ItemGuid), nameof(SearchInteractionInfo.Position), nameof(SearchInteractionInfo.Type),
        nameof(SearchInteractionInfo.EventId), nameof(SearchInteractionInfo.ReceivedUtc),
    ];

    /// <summary>Installed schema version read from the database, 0 when absent.</summary>
    public int ReadInstalledVersion() =>
        int.TryParse(ReadKey(AnalyticsModuleInstaller.VersionKeyName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ? version : 0;

    /// <summary>Last fully rolled UTC day read from the database, or null before the first rollup.</summary>
    public DateOnly? ReadWatermark() =>
        DateOnly.TryParseExact(ReadKey(AnalyticsModuleInstaller.WatermarkKeyName), DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    /// <summary>UTC day of the oldest raw search row, or null when there is none.</summary>
    public DateOnly? EarliestRawDay()
    {
        // GetScalarResult<DateTime?> returns null even for a value, so MinValue stands for "no rows".
        var earliest = searchEvents.Get()
            .TopN(1)
            .Column(nameof(SearchEventInfo.ReceivedUtc))
            .OrderBy(nameof(SearchEventInfo.ReceivedUtc))
            .GetScalarResult(DateTime.MinValue);
        return earliest == DateTime.MinValue ? null : DateOnly.FromDateTime(earliest);
    }

    /// <summary>Rolls <paramref name="day"/> from the raw rows its input contract needs.</summary>
    public DailyRollup Compute(DateOnly day, RollupSettings settings)
    {
        var dayStart = RollupScheduler.Midnight(day);
        var window = settings.AttributionWindow;
        var touchingIds = searchEvents.Get()
            .Column(nameof(SearchEventInfo.SearchId))
            .WhereGreaterOrEquals(nameof(SearchEventInfo.ReceivedUtc), dayStart - window)
            .WhereLessOrEquals(nameof(SearchEventInfo.ReceivedUtc), dayStart.AddDays(1) + window);
        var searchRows = searchEvents.Get()
            .Columns(SearchColumns)
            .WhereIn(nameof(SearchEventInfo.SearchId), touchingIds)
            .GetEnumerableTypedResult()
            .Select(ToRow)
            .ToList();
        var interactionRows = interactions.Get()
            .Columns(InteractionColumns)
            .WhereGreaterOrEquals(nameof(SearchInteractionInfo.ReceivedUtc), dayStart)
            .WhereLessOrEquals(nameof(SearchInteractionInfo.ReceivedUtc), dayStart.AddDays(1) + (2 * window))
            .GetEnumerableTypedResult()
            .Select(ToRow)
            .ToList();
        var rollup = RollupEngine.Roll(day, searchRows, interactionRows, settings);
        return rollup with { Kpis = MergeCollationDuplicates(rollup.Kpis, CollationCanonicalTexts(touchingIds)) };
    }

    /// <summary>
    /// Merges KPIs whose query texts differ ordinally but are equal under the database collation (case, ß/ss, ignorable characters), which the
    /// unique index would reject; <paramref name="canonical"/> maps (index, text) to its group's spelling. The most searched spelling is kept and SessionCount becomes an upper bound.
    /// </summary>
    internal static IReadOnlyList<DailyKpi> MergeCollationDuplicates(IReadOnlyList<DailyKpi> kpis, IReadOnlyDictionary<(string IndexName, string QueryText), string> canonical)
    {
        if (canonical.Count == 0)
        {
            return kpis;
        }

        return kpis
            .GroupBy(kpi => (kpi.IndexName, canonical.GetValueOrDefault((kpi.IndexName, kpi.QueryText), kpi.QueryText)))
            .Select(group => group
                .OrderByDescending(kpi => kpi.Searches)
                .ThenBy(kpi => kpi.QueryText, StringComparer.Ordinal)
                .First() with
            {
                Searches = group.Sum(kpi => kpi.Searches),
                ZeroResultSearches = group.Sum(kpi => kpi.ZeroResultSearches),
                SubmittedSearches = group.Sum(kpi => kpi.SubmittedSearches),
                ClickedSearches = group.Sum(kpi => kpi.ClickedSearches),
                Clicks = group.Sum(kpi => kpi.Clicks),
                ClickPositionSum = group.Sum(kpi => kpi.ClickPositionSum),
                ConvertedSearches = group.Sum(kpi => kpi.ConvertedSearches),
                Conversions = group.Sum(kpi => kpi.Conversions),
                SessionCount = group.Sum(kpi => kpi.SessionCount),
                LatencyBuckets = Enumerable.Range(0, SearchDailyInfo.LatencyBucketCount).Select(i => group.Sum(kpi => kpi.LatencyBuckets[i])).ToArray(),
            })
            .OrderBy(kpi => kpi.IndexName, StringComparer.Ordinal)
            .ThenBy(kpi => kpi.QueryText, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>In one transaction: deletes the day's query rows (including "(other)") and item rows, advances the watermark to the day and inserts <paramref name="rollup"/>.</summary>
    public void Replace(DateOnly day, DailyRollup rollup)
    {
        using var scope = new CMSTransactionScope();
        daily.BulkDelete(new WhereCondition().WhereEquals(nameof(SearchDailyInfo.Day), RollupScheduler.Midnight(day)));
        itemDaily.BulkDelete(new WhereCondition().WhereEquals(nameof(ItemDailyInfo.Day), RollupScheduler.Midnight(day)));
        WriteWatermark(day);
        if (rollup.Kpis.Count > 0)
        {
            daily.BulkInsert(rollup.Kpis.Select(ToInfo));
        }

        if (rollup.Items.Count > 0)
        {
            itemDaily.BulkInsert(rollup.Items.Select(ToInfo));
        }

        scope.Commit();
    }

    /// <summary>Sets the watermark; joins the caller's transaction when there is one.</summary>
    public void WriteWatermark(DateOnly day)
    {
        var key = settingsKeys.Get().WhereEquals(nameof(SettingsKeyInfo.KeyName), AnalyticsModuleInstaller.WatermarkKeyName).First();
        key.KeyValue = day.ToString(DayFormat, CultureInfo.InvariantCulture);
        settingsKeys.Set(key);
    }

    // Partitioning by QueryText groups with the column's collation, which the SearchDaily unique index shares; BIN2 keeps and picks exact spellings.
    private Dictionary<(string IndexName, string QueryText), string> CollationCanonicalTexts(ObjectQuery<SearchEventInfo> searchIds) =>
        searchEvents.Get()
            .Columns(
                nameof(SearchEventInfo.IndexName),
                "QueryText COLLATE Latin1_General_BIN2 AS Spelling",
                "MIN(QueryText COLLATE Latin1_General_BIN2) OVER (PARTITION BY IndexName, QueryText) AS Canonical")
            .Distinct(true)
            .WhereIn(nameof(SearchEventInfo.SearchId), searchIds)
            .WhereNotNull(nameof(SearchEventInfo.QueryText))
            .GetEnumerableResult()
            .Select(record => (IndexName: (string)record[0], Spelling: (string)record[1], Canonical: (string)record[2]))
            .Where(row => !string.Equals(row.Spelling, row.Canonical, StringComparison.Ordinal))
            .ToDictionary(row => (row.IndexName, row.Spelling), row => row.Canonical);

    // An ObjectQuery reads the table directly; SettingsKeyInfoProvider.GetValue would read Kentico's settings cache, which can be stale on other nodes.
    private string? ReadKey(string keyName) =>
        settingsKeys.Get()
            .Column(nameof(SettingsKeyInfo.KeyValue))
            .WhereEquals(nameof(SettingsKeyInfo.KeyName), keyName)
            .GetScalarResult<string?>(null);

    private static RawSearchRow ToRow(SearchEventInfo e) =>
        new(e.SearchEventID, e.SearchId, e.Sequence, e.SessionId, e.ReceivedUtc, e.IndexName, e.QueryText, e.Page, e.PageSize, e.ResultCount, e.LatencyMs, e.IsSubmit);

    private static RawInteractionRow ToRow(SearchInteractionInfo i) =>
        new(i.SearchInteractionID, i.SearchId, i.Sequence, i.ItemGuid, i.Position, i.Type, i.EventId, i.ReceivedUtc);

    /// <summary>Maps an engine KPI onto a new <see cref="SearchDailyInfo"/>.</summary>
    internal static SearchDailyInfo ToInfo(DailyKpi kpi)
    {
        var info = new SearchDailyInfo
        {
            Day = RollupScheduler.Midnight(kpi.Day),
            IndexName = kpi.IndexName,
            QueryText = kpi.QueryText,
            Searches = kpi.Searches,
            ZeroResultSearches = kpi.ZeroResultSearches,
            SubmittedSearches = kpi.SubmittedSearches,
            ClickedSearches = kpi.ClickedSearches,
            Clicks = kpi.Clicks,
            ClickPositionSum = kpi.ClickPositionSum,
            ConvertedSearches = kpi.ConvertedSearches,
            Conversions = kpi.Conversions,
            SessionCount = kpi.SessionCount,
        };
        for (int i = 0; i < SearchDailyInfo.LatencyBucketCount; i++)
        {
            info.SetValue($"LatencyBucket{i}", kpi.LatencyBuckets[i]);
        }

        return info;
    }

    /// <summary>Maps an engine item KPI onto a new <see cref="ItemDailyInfo"/>.</summary>
    internal static ItemDailyInfo ToInfo(ItemKpi item) => new()
    {
        Day = RollupScheduler.Midnight(item.Day),
        IndexName = item.IndexName,
        ItemGuid = item.ItemGuid,
        ClickedSearches = item.ClickedSearches,
        ClickPositionSum = item.ClickPositionSum,
        Conversions = item.Conversions,
    };
}
