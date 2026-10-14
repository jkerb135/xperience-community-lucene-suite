using System.Runtime.InteropServices;

using XperienceCommunity.Lucene.Suite.Search;

using ItemCounts = (int ClickedSearches, long ClickPositionSum, int Conversions);

namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Server-side merge settings the rollup needs; the client-only rules (typos, deep backspace) are not re-applied.</summary>
internal sealed record RollupSettings(TimeSpan TextChangeWindow, TimeSpan AttributionWindow)
{
    /// <summary>Longest a logical search lasts from its first row; a later row starts a new one.</summary>
    public TimeSpan MaxLogicalSearchDuration => AttributionWindow;

    /// <summary>Settings taken from the configured options.</summary>
    public static RollupSettings From(SuiteAnalyticsOptions options) =>
        new(options.Merge.TextChangeWindow, options.Attribution.AttributionWindow);
}

/// <summary>Pure, deterministic rollup of one UTC day of raw rows into daily query and item KPIs: no I/O, no clock.</summary>
/// <remarks>
/// Input contract for <see cref="Roll"/>: every raw search row of each SearchId that has at least one row in
/// [day start − AttributionWindow, day end + AttributionWindow], and the interactions received in
/// [day start, day end + 2 × AttributionWindow]. The engine decides which logical searches belong to the day.
/// </remarks>
internal static class RollupEngine
{
    /// <summary>
    /// Rolls up the logical searches that start on <paramref name="day"/> (UTC): query KPIs ordered by index then query text, and item KPIs
    /// ordered by index then ItemGuid, counted at the same points as the query KPIs' clicks and conversions.
    /// </summary>
    public static DailyRollup Roll(
        DateOnly day,
        IEnumerable<RawSearchRow> searches,
        IEnumerable<RawInteractionRow> interactions,
        RollupSettings settings)
    {
        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var owned = Segment(searches, settings)
            .Where(search => search.First.ReceivedUtc >= dayStart && search.First.ReceivedUtc < dayEnd)
            .Select(search => new Tally(search))
            .ToList();

        var rowOwners = new Dictionary<(Guid SearchId, int Sequence), (Tally Tally, RawSearchRow Row)>();
        foreach (var tally in owned)
        {
            foreach (var row in tally.Search.Rows)
            {
                rowOwners[(row.SearchId, row.Sequence)] = (tally, row);
            }
        }

        var items = new Dictionary<(string IndexName, Guid ItemGuid), ItemCounts>();
        Attribute(interactions, rowOwners, items, settings);

        var kpis = owned
            .GroupBy(tally => (tally.Search.First.IndexName, tally.CountedText))
            .Select(group => Kpi(day, group.Key.IndexName, group.Key.CountedText, group.ToList()))
            .OrderBy(kpi => kpi.IndexName, StringComparer.Ordinal)
            .ThenBy(kpi => kpi.QueryText, StringComparer.Ordinal)
            .ToList();
        var itemKpis = items
            .OrderBy(item => item.Key.IndexName, StringComparer.Ordinal)
            .ThenBy(item => item.Key.ItemGuid)
            .Select(item => new ItemKpi(day, item.Key.IndexName, item.Key.ItemGuid, item.Value.ClickedSearches, item.Value.ClickPositionSum, item.Value.Conversions))
            .ToList();
        return new DailyRollup(kpis, itemKpis);
    }

    /// <summary>Deduplicates rows per (SearchId, Sequence) and splits each SearchId into logical searches.</summary>
    public static IReadOnlyList<LogicalSearch> Segment(IEnumerable<RawSearchRow> searches, RollupSettings settings)
    {
        // Duplicate (SearchId, Sequence): earliest ReceivedUtc wins, then the lowest row id, whatever the input order.
        var bySearchId = new Dictionary<Guid, Dictionary<int, RawSearchRow>>();
        foreach (var row in searches)
        {
            if (!bySearchId.TryGetValue(row.SearchId, out var bySequence))
            {
                bySearchId[row.SearchId] = bySequence = [];
            }

            if (!bySequence.TryGetValue(row.Sequence, out var kept)
                || (row.ReceivedUtc, row.Id).CompareTo((kept.ReceivedUtc, kept.Id)) < 0)
            {
                bySequence[row.Sequence] = row;
            }
        }

        var result = new List<LogicalSearch>();
        foreach (var bySequence in bySearchId.Values)
        {
            var rows = bySequence.Values.OrderBy(row => row.Sequence).ToList();
            int start = 0;
            for (int i = 1; i <= rows.Count; i++)
            {
                if (i == rows.Count || StartsNew(rows[start], rows[i - 1], rows[i], settings))
                {
                    result.Add(new LogicalSearch(rows.GetRange(start, i - start)));
                    start = i;
                }
            }
        }

        return result;
    }

    private static bool StartsNew(RawSearchRow first, RawSearchRow previous, RawSearchRow row, RollupSettings settings) =>
        (row.QueryText != previous.QueryText && row.ReceivedUtc - previous.ReceivedUtc > settings.TextChangeWindow)
        || row.ReceivedUtc - first.ReceivedUtc > settings.MaxLogicalSearchDuration;

    private static void Attribute(
        IEnumerable<RawInteractionRow> interactions,
        Dictionary<(Guid SearchId, int Sequence), (Tally Tally, RawSearchRow Row)> rowOwners,
        Dictionary<(string IndexName, Guid ItemGuid), ItemCounts> items,
        RollupSettings settings)
    {
        var seenEvents = new HashSet<Guid>();
        foreach (var interaction in interactions.OrderBy(i => i.ReceivedUtc).ThenBy(i => i.Id))
        {
            if (!seenEvents.Add(interaction.EventId)
                || !rowOwners.TryGetValue((interaction.SearchId, interaction.Sequence), out var owner)
                || (interaction.ReceivedUtc - owner.Row.ReceivedUtc).Duration() > settings.AttributionWindow)
            {
                continue;
            }

            // Items are counted exactly where the query KPIs count, so per index and day their sums match SearchDaily's Clicks and Conversions.
            if (interaction.Type == SearchInteractionType.Conversion)
            {
                owner.Tally.Conversions++;
                ItemOf(items, owner.Tally, interaction).Conversions++;
            }
            else if (interaction.Type == SearchInteractionType.Click
                && interaction.Position is int position
                && (long)(owner.Row.Page - 1) * owner.Row.PageSize < position
                && position <= (long)owner.Row.Page * owner.Row.PageSize
                && owner.Tally.ClickedItems.Add(interaction.ItemGuid))
            {
                owner.Tally.ClickPositionSum += position;
                owner.Tally.ClickedSequences.Add(owner.Row.Sequence);
                ref var item = ref ItemOf(items, owner.Tally, interaction);
                item.ClickedSearches++;
                item.ClickPositionSum += position;
            }
        }
    }

    private static ref ItemCounts ItemOf(Dictionary<(string IndexName, Guid ItemGuid), ItemCounts> items, Tally tally, RawInteractionRow interaction) =>
        ref CollectionsMarshal.GetValueRefOrAddDefault(items, (tally.Search.First.IndexName, interaction.ItemGuid), out _);

    private static DailyKpi Kpi(DateOnly day, string indexName, string queryText, List<Tally> tallies)
    {
        var buckets = new int[SearchDailyInfo.LatencyBucketCount];
        var sessions = new HashSet<Guid>();
        int zeroResult = 0;
        int submitted = 0;
        foreach (var tally in tallies)
        {
            var countedRows = tally.Search.Rows.Where(row => row.QueryText == queryText).ToList();
            buckets[DailyKpi.LatencyBucket(countedRows[0].LatencyMs)]++;
            zeroResult += countedRows.TrueForAll(row => row.ResultCount == 0) ? 1 : 0;
            submitted += tally.Search.Rows.Any(row => row.IsSubmit) ? 1 : 0;
            sessions.UnionWith(tally.Search.Rows.Where(row => row.SessionId.HasValue).Select(row => row.SessionId!.Value));
        }

        return new DailyKpi(
            day,
            indexName,
            queryText,
            tallies.Count,
            zeroResult,
            submitted,
            tallies.Count(t => t.ClickedItems.Count > 0),
            tallies.Sum(t => t.ClickedItems.Count),
            tallies.Sum(t => t.ClickPositionSum),
            tallies.Count(t => t.Conversions > 0),
            tallies.Sum(t => t.Conversions),
            sessions.Count,
            buckets);
    }

    /// <summary>Counted text: the earliest clicked row's, else the last row that is not a backspace fragment.</summary>
    private static string CountedText(LogicalSearch search, SortedSet<int> clickedSequences)
    {
        var rows = search.Rows;
        if (clickedSequences.Count > 0)
        {
            return rows.First(row => row.Sequence == clickedSequences.Min).QueryText;
        }

        // A row is a fragment when an earlier row's text strictly extends it; compare distinct texts once.
        var firstIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            firstIndex.TryAdd(rows[i].QueryText, i);
        }

        var earliestExtension = firstIndex.Keys.ToDictionary(
            text => text,
            text => firstIndex
                .Where(other => other.Key.Length > text.Length && other.Key.StartsWith(text, StringComparison.Ordinal))
                .Select(other => other.Value)
                .DefaultIfEmpty(int.MaxValue)
                .Min(),
            StringComparer.Ordinal);

        for (int i = rows.Count - 1; ; i--)
        {
            // Row 0 has no earlier row, so the loop always ends here at the latest.
            if (earliestExtension[rows[i].QueryText] >= i)
            {
                return rows[i].QueryText;
            }
        }
    }

    private sealed class Tally(LogicalSearch search)
    {
        public LogicalSearch Search { get; } = search;

        public HashSet<Guid> ClickedItems { get; } = [];

        public SortedSet<int> ClickedSequences { get; } = [];

        public long ClickPositionSum { get; set; }

        public int Conversions { get; set; }

        public string CountedText => RollupEngine.CountedText(Search, ClickedSequences);
    }
}
