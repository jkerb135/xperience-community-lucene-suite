using System.Text.Json;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RollupEngineTests
{
    private const string Index = "dancinggoat";
    private static readonly DateOnly Day = new(2026, 10, 9);
    private static readonly DateTime T0 = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
    private static readonly RollupSettings Defaults = RollupSettings.From(new SuiteAnalyticsOptions());
    private static int nextId;

    [Fact]
    public void Settings_ComeFromTheOptions_AndCapDurationAtTheAttributionWindow()
    {
        var options = new SuiteAnalyticsOptions();
        options.Merge.TextChangeWindow = TimeSpan.FromSeconds(45);
        options.Attribution.AttributionWindow = TimeSpan.FromMinutes(20);

        var settings = RollupSettings.From(options);

        Assert.Equal((TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(20)), (settings.TextChangeWindow, settings.AttributionWindow, settings.MaxLogicalSearchDuration));
    }

    [Fact]
    public void DuplicateSequence_FirstReceivedWins()
    {
        var id = Guid.NewGuid();
        var kpi = Single(Roll([Row(id, 1, 1, "coffee", results: 0), Row(id, 1, 0, "coffee", results: 48)]));

        Assert.Equal((1, 0), (kpi.Searches, kpi.ZeroResultSearches));
    }

    [Fact]
    public void DuplicateSequence_WithEqualReceivedUtc_LowestRowIdWins_InEveryOrder()
    {
        var id = Guid.NewGuid();
        RawSearchRow[] rows = [Row(id, 1, 0, "coffee", rowId: 7, results: 48), Row(id, 1, 0, "coffee", rowId: 9, results: 0), Row(id, 1, 0, "coffee", rowId: 8, results: 0)];

        foreach (var order in Shuffles(rows))
        {
            Assert.Equal(0, Single(Roll(order)).ZeroResultSearches);
        }
    }

    [Fact]
    public void Rows_AreOrderedBySequence_NotByArrival()
    {
        var id = Guid.NewGuid();

        // Node clocks disagree: seq 2 ("coffee") arrives stamped before seq 1 ("tea").
        var kpi = Single(Roll([Row(id, 2, 0.5, "coffee"), Row(id, 1, 1, "tea")]));

        Assert.Equal("coffee", kpi.QueryText);
    }

    [Theory]
    [InlineData(30.0, 1)]
    [InlineData(30.001, 2)]
    public void TextChange_AfterMoreThanTheWindow_StartsANewLogicalSearch(double gapSeconds, int expected)
    {
        var id = Guid.NewGuid();

        var kpis = Roll([Row(id, 1, 0, "cof"), Row(id, 2, gapSeconds, "coffee")]);

        Assert.Equal(expected, kpis.Sum(k => k.Searches));
    }

    [Fact]
    public void PageAndFilterChanges_NeverSplit_HoweverLongTheGap()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "coffee"), Row(id, 2, 600, "coffee", page: 2), Row(id, 3, 1800, "coffee", results: 3)]));

        Assert.Equal(1, kpi.Searches);
    }

    [Fact]
    public void ConfiguredTextChangeWindow_ChangesTheSplit()
    {
        var id = Guid.NewGuid();
        RawSearchRow[] rows = [Row(id, 1, 0, "cof"), Row(id, 2, 40, "coffee")];

        Assert.Equal(2, Roll(rows).Sum(k => k.Searches));
        Assert.Equal(1, Roll(rows, Defaults with { TextChangeWindow = TimeSpan.FromSeconds(45) }).Sum(k => k.Searches));
    }

    [Fact]
    public void SearchStartingBeforeMidnight_PagingTo0040_BelongsEntirelyToTheEarlierDay()
    {
        var id = Guid.NewGuid();
        var start = new DateTime(2026, 10, 9, 23, 59, 50, DateTimeKind.Utc);
        RawSearchRow[] rows = [RowAt(id, 1, start, "coffee"), RowAt(id, 2, start.AddMinutes(20), "coffee", page: 2), RowAt(id, 3, new DateTime(2026, 10, 10, 0, 40, 0, DateTimeKind.Utc), "coffee", page: 3)];

        Assert.Equal(1, Single(Roll(rows)).Searches);
        Assert.Empty(Roll(rows, day: Day.AddDays(1)));
    }

    [Fact]
    public void SearchPagingPastTheDurationCap_SplitsThere()
    {
        var id = Guid.NewGuid();
        var start = new DateTime(2026, 10, 9, 23, 59, 50, DateTimeKind.Utc);
        RawSearchRow[] rows =
        [
            RowAt(id, 1, start, "coffee"),
            RowAt(id, 2, new DateTime(2026, 10, 10, 0, 40, 0, DateTimeKind.Utc), "coffee", page: 2),
            RowAt(id, 3, new DateTime(2026, 10, 10, 1, 10, 0, DateTimeKind.Utc), "coffee", page: 3),
        ];

        Assert.Equal(2, RollupEngine.Segment(rows, Defaults).Count);
        Assert.Equal(1, Single(Roll(rows)).Searches);
        Assert.Equal(1, Single(Roll(rows, day: Day.AddDays(1))).Searches);
    }

    [Fact]
    public void SearchStartingJustAfterMidnight_BelongsToTheNewDay()
    {
        var row = RowAt(Guid.NewGuid(), 1, new DateTime(2026, 10, 10, 0, 0, 5, DateTimeKind.Utc), "coffee");

        Assert.Empty(Roll([row]));
        Assert.Equal(1, Single(Roll([row], day: Day.AddDays(1))).Searches);
    }

    [Fact]
    public void ClickBeforeMidnightSearch_At0020NextDay_CountsForTheEarlierDay()
    {
        var id = Guid.NewGuid();
        var start = new DateTime(2026, 10, 9, 23, 59, 50, DateTimeKind.Utc);
        var click = new RawInteractionRow(NextId(), id, 1, Guid.NewGuid(), 1, SearchInteractionType.Click, Guid.NewGuid(), new DateTime(2026, 10, 10, 0, 20, 0, DateTimeKind.Utc));

        var kpi = Single(Roll([RowAt(id, 1, start, "coffee")], [click]));

        Assert.Equal((1, 1), (kpi.ClickedSearches, kpi.Clicks));
    }

    [Fact]
    public void SettleBug_PausingWhileTyping_IsStillOneSearch()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "c"), Row(id, 2, 1, "co"), Row(id, 3, 2, "cof"), Row(id, 4, 4.5, "coffee grinder")]));

        Assert.Equal(("coffee grinder", 1), (kpi.QueryText, kpi.Searches));
    }

    [Fact]
    public void ClickedRow_DecidesTheCountedText()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "cof"), Row(id, 2, 1, "coffee")], [Click(id, 1, 5, 3)]));

        Assert.Equal(("cof", 1, 1, 3L), (kpi.QueryText, kpi.ClickedSearches, kpi.Clicks, kpi.ClickPositionSum));
    }

    [Fact]
    public void EarliestClickedRow_DecidesTheCountedText_WhateverTheClickOrder()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll(
            [Row(id, 1, 0, "co"), Row(id, 2, 1, "cof"), Row(id, 3, 2, "coffee")],
            [Click(id, 3, 5, 1), Click(id, 2, 9, 2)]));

        Assert.Equal(("cof", 2), (kpi.QueryText, kpi.Clicks));
    }

    [Fact]
    public void WithoutClicks_TheLastRowThatIsNotABackspaceFragment_IsCounted()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "coffee grinder"), Row(id, 2, 1, "coffee grind"), Row(id, 3, 2, "coffee")]));

        Assert.Equal("coffee grinder", kpi.QueryText);
    }

    [Fact]
    public void TypoFixedByBackspacing_CountsTheRetypedText()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "cofee"), Row(id, 2, 1, "cofe"), Row(id, 3, 2, "cof"), Row(id, 4, 3, "coff"), Row(id, 5, 4, "coffe"), Row(id, 6, 5, "coffee")]));

        Assert.Equal("coffee", kpi.QueryText);
    }

    [Fact]
    public void FilterZeroResultBug_AFilterReturningNothing_DoesNotMakeTheQueryZeroResult()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();

        var kpis = Roll(
        [
            Row(id, 1, 0, "coffee", results: 48), Row(id, 2, 5, "coffee", results: 0),
            Row(other, 1, 0, "coffee", results: 0), Row(other, 2, 5, "coffee", results: 0, page: 2),
        ]);

        Assert.Equal((2, 1), (Single(kpis).Searches, Single(kpis).ZeroResultSearches));
    }

    [Fact]
    public void ZeroResult_IgnoresRowsWithOtherText()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "cofee", results: 12), Row(id, 2, 1, "coffee", results: 0)]));

        Assert.Equal(("coffee", 1), (kpi.QueryText, kpi.ZeroResultSearches));
    }

    [Fact]
    public void EmptyQueryBug_ClearBetweenQueries_GivesSeparateSearches_AndABrowseBucket()
    {
        var kpis = Roll([Row(Guid.NewGuid(), 1, 0, "laptop"), Row(Guid.NewGuid(), 1, 1, ""), Row(Guid.NewGuid(), 1, 2, "phone")]);

        Assert.Equal(["", "laptop", "phone"], kpis.Select(k => k.QueryText));
        Assert.All(kpis, k => Assert.Equal(1, k.Searches));
    }

    [Fact]
    public void ShortQueriesMintedSeparately_StaySeparate()
    {
        var kpis = Roll([Row(Guid.NewGuid(), 1, 0, "tv"), Row(Guid.NewGuid(), 1, 1, "pc")]);

        Assert.Equal(["pc", "tv"], kpis.Select(k => k.QueryText));
    }

    [Fact]
    public void Latency_IsTheFirstRowWithTheCountedText()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "cof", latency: 300), Row(id, 2, 1, "coffee", latency: 40), Row(id, 3, 2, "coffee", latency: 3, page: 2)]));

        Assert.Equal(1, kpi.LatencyBuckets[2]);
        Assert.Equal(1, kpi.LatencyBuckets.Sum());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(25, 1)]
    [InlineData(100, 3)]
    [InlineData(5000, 8)]
    [InlineData(5001, 9)]
    [InlineData(int.MaxValue, 9)]
    public void LatencyBuckets_UseTheFixedEdges(int latencyMs, int bucket) => Assert.Equal(bucket, DailyKpi.LatencyBucket(latencyMs));

    [Fact]
    public void Submitted_AndSessions_AreCounted()
    {
        var session = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var kpi = Single(Roll(
        [
            Row(a, 1, 0, "coffee", session: session), Row(a, 2, 1, "coffee", submit: true, session: session),
            Row(b, 1, 0, "coffee", session: session), Row(Guid.NewGuid(), 1, 0, "coffee", session: Guid.NewGuid()), Row(Guid.NewGuid(), 1, 0, "coffee"),
        ]));

        Assert.Equal((4, 1, 2), (kpi.Searches, kpi.SubmittedSearches, kpi.SessionCount));
    }

    [Theory]
    [InlineData(3600.0, 1)]
    [InlineData(3600.001, 0)]
    public void Interaction_CountsOnlyWithinTheAttributionWindowOfItsRow(double afterSeconds, int expected)
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "coffee")], [Click(id, 1, afterSeconds, 1), Conversion(id, 1, afterSeconds)]));

        Assert.Equal((expected, expected), (kpi.Clicks, kpi.Conversions));
    }

    [Fact]
    public void Interaction_WindowIsMeasuredFromTheReferencedRow()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "cof"), Row(id, 2, 1800, "cof", page: 2)], [Click(id, 2, 3600.5, 11), Click(id, 1, 3600.5, 1)]));

        Assert.Equal((1, 11L), (kpi.Clicks, kpi.ClickPositionSum));
    }

    [Fact]
    public void Interaction_ForAnUnknownSearchOrSequence_IsDropped()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "coffee")], [Click(id, 2, 1, 1), Click(Guid.NewGuid(), 1, 1, 1), Conversion(id, 5, 1)]));

        Assert.Equal((0, 0), (kpi.Clicks, kpi.Conversions));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 0)]
    public void Click_MustBeOnThePageItsRowReturned(int position, int expected)
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "coffee", page: 2, pageSize: 10)], [Click(id, 1, 1, position)]));

        Assert.Equal(expected, kpi.Clicks);
    }

    [Fact]
    public void DuplicateEventIds_CollapseFirst_ThenClicksPerItem()
    {
        var id = Guid.NewGuid();
        var item = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var kpi = Single(Roll(
            [Row(id, 1, 0, "coffee"), Row(id, 2, 1, "coffee", page: 2)],
            [
                Click(id, 1, 5, 2, item, eventId), Click(id, 1, 6, 2, item, eventId),
                Click(id, 2, 7, 14, item), Click(id, 1, 8, 3),
                Conversion(id, 1, 9, eventId: eventId), Conversion(id, 1, 10, item), Conversion(id, 1, 11, item, eventId: Guid.Empty), Conversion(id, 1, 12, item, eventId: Guid.Empty),
            ]));

        Assert.Equal((2, 5L, 1, 2), (kpi.Clicks, kpi.ClickPositionSum, kpi.ConvertedSearches, kpi.Conversions));
    }

    [Fact]
    public void Conversion_WithoutAPosition_Counts()
    {
        var id = Guid.NewGuid();

        var kpi = Single(Roll([Row(id, 1, 0, "coffee")], [Conversion(id, 1, 30)]));

        Assert.Equal((0, 1, 1), (kpi.Clicks, kpi.ConvertedSearches, kpi.Conversions));
    }

    [Fact]
    public void Click_WithoutAPosition_IsDropped()
    {
        var id = Guid.NewGuid();
        var click = new RawInteractionRow(NextId(), id, 1, Guid.NewGuid(), null, SearchInteractionType.Click, Guid.NewGuid(), T0.AddSeconds(1));

        Assert.Equal(0, Single(Roll([Row(id, 1, 0, "coffee")], [click])).Clicks);
    }

    [Fact]
    public void Interaction_OnASearchOfAnotherDay_IsIgnored()
    {
        var yesterday = Guid.NewGuid();
        var today = Guid.NewGuid();

        var kpi = Single(Roll(
            [RowAt(yesterday, 1, new DateTime(2026, 10, 8, 23, 50, 0, DateTimeKind.Utc), "tea"), Row(today, 1, 0, "coffee")],
            [new RawInteractionRow(NextId(), yesterday, 1, Guid.NewGuid(), 1, SearchInteractionType.Click, Guid.NewGuid(), new DateTime(2026, 10, 9, 0, 10, 0, DateTimeKind.Utc))]));

        Assert.Equal(("coffee", 0), (kpi.QueryText, kpi.Clicks));
    }

    [Fact]
    public void Output_GroupsByIndexAndCountedText_InOrdinalOrder()
    {
        var kpis = Roll(
        [
            Row(Guid.NewGuid(), 1, 0, "tea"), Row(Guid.NewGuid(), 1, 1, "coffee"), Row(Guid.NewGuid(), 1, 2, "coffee"),
            Row(Guid.NewGuid(), 1, 3, "coffee") with { IndexName = "articles" },
        ]);

        Assert.Equal([("articles", "coffee", 1), (Index, "coffee", 2), (Index, "tea", 1)], kpis.Select(k => (k.IndexName, k.QueryText, k.Searches)));
        Assert.All(kpis, k => Assert.Equal(Day, k.Day));
    }

    [Fact]
    public void MultiNode_ShuffledAndDuplicatedRows_GiveIdenticalOutput()
    {
        var (searches, interactions) = Scenario();
        string? expected = null;

        foreach (int seed in new[] { 1, 42, 2026 })
        {
            var random = new Random(seed);

            // Each node delivers a random subset again (retries), with its own clock offset and row ids.
            var nodeCopies = searches.Where(_ => random.Next(3) == 0).Select(r => r with { Id = r.Id + 100_000, ReceivedUtc = r.ReceivedUtc.AddMilliseconds(random.Next(1, 500)) });
            var allSearches = searches.Concat(nodeCopies).OrderBy(_ => random.Next()).ToList();
            var allInteractions = interactions.Concat(interactions.Where(_ => random.Next(2) == 0).Select(i => i with { Id = i.Id + 100_000, ReceivedUtc = i.ReceivedUtc.AddMilliseconds(random.Next(1, 500)) }))
                .OrderBy(_ => random.Next())
                .ToList();

            string actual = JsonSerializer.Serialize(RollAll(allSearches, allInteractions));
            expected ??= actual;
            Assert.Equal(expected, actual);
        }

        var rollup = RollAll(searches, interactions);
        var baseline = rollup.Kpis;
        Assert.Equal(JsonSerializer.Serialize(rollup), expected);
        Assert.True(rollup.Items.Count > 10, "scenario has item rows");
        Assert.True(baseline.Count > 6 && baseline.Sum(k => k.Clicks) > 10 && baseline.Sum(k => k.Conversions) > 5 && baseline.Sum(k => k.ZeroResultSearches) > 0, "scenario is non-trivial");
    }

    [Fact]
    public void Items_CountOncePerLogicalSearch_AcrossSearches_WithTheirPositions()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var x = new Guid("00000000-0000-0000-0000-000000000001");
        var y = new Guid("00000000-0000-0000-0000-000000000002");

        var items = RollAll(
            [Row(a, 1, 0, "coffee"), Row(b, 1, 100, "tea")],
            [Click(a, 1, 5, 2, x), Click(a, 1, 6, 2, x), Click(a, 1, 7, 4, y), Click(b, 1, 105, 1, x)]).Items;

        Assert.Equal([new ItemKpi(Day, Index, x, 2, 3, 0), new ItemKpi(Day, Index, y, 1, 4, 0)], items);
    }

    [Theory]
    [InlineData(11, 30.0)]
    [InlineData(1, 3600.001)]
    public void Items_ClickOffThePageOrPastTheWindow_IsNotCounted(int position, double afterSeconds)
    {
        var id = Guid.NewGuid();

        var rollup = RollAll([Row(id, 1, 0, "coffee")], [Click(id, 1, afterSeconds, position), Conversion(id, 1, 3600.001)]);

        Assert.Equal(0, Single(rollup.Kpis).Clicks);
        Assert.Empty(rollup.Items);
    }

    [Fact]
    public void Items_ConversionWithoutAClick_IsCounted()
    {
        var id = Guid.NewGuid();
        var item = Guid.NewGuid();

        var items = RollAll([Row(id, 1, 0, "coffee")], [Conversion(id, 1, 30, item), Conversion(id, 1, 31, item)]).Items;

        Assert.Equal([new ItemKpi(Day, Index, item, 0, 0, 2)], items);
    }

    [Fact]
    public void Items_KeepIndexesApart_OrderedByIndexThenItemGuid()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var low = new Guid("00000000-0000-0000-0000-000000000001");
        var high = new Guid("00000000-0000-0000-0000-000000000002");

        var items = RollAll(
            [Row(a, 1, 0, "coffee"), Row(b, 1, 1, "coffee") with { IndexName = "articles" }],
            [Click(a, 1, 5, 1, high), Click(a, 1, 6, 2, low), Click(b, 1, 7, 3, high)]).Items;

        Assert.Equal([("articles", high, 1), (Index, low, 1), (Index, high, 1)], items.Select(i => (i.IndexName, i.ItemGuid, i.ClickedSearches)));
    }

    [Fact]
    public void Items_SumToTheQueryKpis_PerIndex()
    {
        var (searches, interactions) = Scenario();
        var other = SyntheticTraffic.Generate(5, [Day], "articles");

        var rollup = RollAll([.. searches, .. other.Searches], [.. interactions, .. other.Interactions]);

        var fromKpis = rollup.Kpis.GroupBy(k => k.IndexName).ToDictionary(g => g.Key, g => (g.Sum(k => k.Clicks), g.Sum(k => k.ClickPositionSum), g.Sum(k => k.Conversions)));
        var fromItems = rollup.Items.GroupBy(i => i.IndexName).ToDictionary(g => g.Key, g => (g.Sum(i => i.ClickedSearches), g.Sum(i => i.ClickPositionSum), g.Sum(i => i.Conversions)));
        Assert.True(fromKpis.Count >= 2 && fromItems.Values.All(sums => sums.Item1 > 0));
        Assert.Equal(fromKpis.OrderBy(e => e.Key, StringComparer.Ordinal), fromItems.OrderBy(e => e.Key, StringComparer.Ordinal));
        Assert.All(rollup.Items, i => Assert.True(i.ClickedSearches + i.Conversions > 0));
    }

    internal static RawSearchRow RowAt(
        Guid searchId,
        int sequence,
        DateTime receivedUtc,
        string query,
        int results = 10,
        int latency = 20,
        int page = 1,
        int pageSize = 10,
        bool submit = false,
        Guid? session = null,
        int? rowId = null) =>
        new(rowId ?? NextId(), searchId, sequence, session, receivedUtc, Index, query, page, pageSize, results, latency, submit);

    private static RawSearchRow Row(
        Guid searchId,
        int sequence,
        double seconds,
        string query,
        int results = 10,
        int latency = 20,
        int page = 1,
        int pageSize = 10,
        bool submit = false,
        Guid? session = null,
        int? rowId = null) =>
        RowAt(searchId, sequence, T0.AddSeconds(seconds), query, results, latency, page, pageSize, submit, session, rowId);

    private static RawInteractionRow Click(Guid searchId, int sequence, double seconds, int position, Guid? item = null, Guid? eventId = null) =>
        new(NextId(), searchId, sequence, item ?? Guid.NewGuid(), position, SearchInteractionType.Click, eventId ?? Guid.NewGuid(), T0.AddSeconds(seconds));

    private static RawInteractionRow Conversion(Guid searchId, int sequence, double seconds, Guid? item = null, Guid? eventId = null) =>
        new(NextId(), searchId, sequence, item ?? Guid.NewGuid(), null, SearchInteractionType.Conversion, eventId ?? Guid.NewGuid(), T0.AddSeconds(seconds));

    private static IReadOnlyList<DailyKpi> Roll(IEnumerable<RawSearchRow> searches, IEnumerable<RawInteractionRow>? interactions = null, RollupSettings? settings = null, DateOnly? day = null) =>
        RollAll(searches, interactions, settings, day).Kpis;

    private static DailyRollup RollAll(IEnumerable<RawSearchRow> searches, IEnumerable<RawInteractionRow>? interactions = null, RollupSettings? settings = null, DateOnly? day = null) =>
        RollupEngine.Roll(day ?? Day, searches, interactions ?? [], settings ?? Defaults);

    private static IReadOnlyList<DailyKpi> Roll(IEnumerable<RawSearchRow> searches, RollupSettings settings) => Roll(searches, null, settings);

    private static DailyKpi Single(IReadOnlyList<DailyKpi> kpis) => Assert.Single(kpis);

    private static int NextId() => Interlocked.Increment(ref nextId);

    private static IEnumerable<T[]> Shuffles<T>(T[] items)
    {
        foreach (int seed in new[] { 1, 42, 2026 })
        {
            var random = new Random(seed);
            yield return items.OrderBy(_ => random.Next()).ToArray();
        }

        yield return items.Reverse().ToArray();
    }

    // A day of mixed traffic: typing, paging, clicks (incl. duplicates and invalid ones), conversions, two indexes.
    private static (List<RawSearchRow> Searches, List<RawInteractionRow> Interactions) Scenario()
    {
        var searches = new List<RawSearchRow>();
        var interactions = new List<RawInteractionRow>();
        var random = new Random(7);
        string[] queries = ["coffee", "coffee grinder", "tea", "espresso", "", "kettle"];
        for (int s = 0; s < 60; s++)
        {
            var id = Guid.NewGuid();
            string target = queries[s % queries.Length];
            double t = random.Next(0, 40_000);
            int seq = 0;
            for (int len = Math.Min(3, target.Length); len <= target.Length; len++)
            {
                searches.Add(Row(id, ++seq, t += random.Next(0, 3), target[..len], results: random.Next(0, 3) == 0 ? 0 : 20, latency: random.Next(1, 6000), session: s % 4 == 0 ? null : Guid.NewGuid()));
            }

            searches.Add(Row(id, ++seq, t += random.Next(10, 4000), target, page: 2, submit: s % 5 == 0) with { IndexName = s % 7 == 0 ? "articles" : Index });
            if (s % 2 == 0)
            {
                var eventId = Guid.NewGuid();
                interactions.Add(Click(id, seq, t + random.Next(1, 4000), random.Next(5, 25), eventId: eventId));
                interactions.Add(Click(id, seq, t + random.Next(1, 4000), 11, eventId: eventId));
                interactions.Add(Click(id, random.Next(1, seq + 1), t + random.Next(1, 4000), random.Next(1, 12)));
            }

            if (s % 3 == 0)
            {
                interactions.Add(Conversion(id, seq, t + random.Next(1, 5000)));
            }
        }

        return (searches, interactions);
    }
}
