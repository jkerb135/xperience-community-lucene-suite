using System.Diagnostics;

using Microsoft.Data.SqlClient;

using Xunit.Abstractions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Opt-in (LUCENESUITE_VOLUME_TEST=1); the persisted case also needs the disposable database.</summary>
public sealed class VolumeFactAttribute : FactAttribute
{
    public VolumeFactAttribute(bool database = false)
    {
        if (Environment.GetEnvironmentVariable("LUCENESUITE_VOLUME_TEST") != "1")
        {
            Skip = "Set LUCENESUITE_VOLUME_TEST=1 to measure rollup volume.";
        }
        else if (database && new DatabaseFactAttribute().Skip is { } skip)
        {
            Skip = skip;
        }
    }
}

[Collection(DatabaseCollection.Name)]
public sealed class RollupVolumeTests(ITestOutputHelper output)
{
    private const int SearchIds = 25_000;
    private const int RowsPerSearch = 4;
    private const int InteractionCount = 20_000;
    private static readonly DateOnly Day = new(2030, 6, 1);
    private static readonly TimeSpan MaxEngineTime = TimeSpan.FromSeconds(30);
    private const long MaxMemoryBytes = 500L * 1024 * 1024;

    [VolumeFact]
    public void Engine_RollsOneHundredThousandRowsAndTwentyThousandInteractions_WithinLimits()
    {
        var traffic = Generate();
        var (kpis, elapsed, allocated, heap) = Measure(() => RollupEngine.Roll(Day, traffic.Searches, traffic.Interactions, RollupSettings.From(new SuiteAnalyticsOptions())).Kpis);

        Report("engine (in-process)", kpis, elapsed, allocated, heap);
        Assert.True(elapsed < MaxEngineTime && heap < MaxMemoryBytes, "Thresholds exceeded: stream per SearchId batch.");
        Assert.Equal(SearchIds, kpis.Sum(kpi => kpi.Searches));
    }

    [VolumeFact(database: true)]
    public void Store_LoadsRollsAndPersistsTheDay_WithinLimits()
    {
        RollupDb.Reset();
        var traffic = Generate();
        var insert = Stopwatch.StartNew();
        RollupDb.Insert(traffic);
        output.WriteLine($"setup: bulk-inserted {traffic.Searches.Count:N0} searches + {traffic.Interactions.Count:N0} interactions in {insert.Elapsed.TotalSeconds:N1} s");
        var store = RollupDb.Store();
        var settings = RollupSettings.From(new SuiteAnalyticsOptions());

        var (kpis, elapsed, allocated, heap) = Measure(() =>
        {
            var computed = store.Compute(Day, settings);
            store.Replace(Day, computed);
            return computed.Kpis;
        });

        Report("store (load + engine + transactional replace)", kpis, elapsed, allocated, heap);
        Assert.True(elapsed < MaxEngineTime && heap < MaxMemoryBytes, "Thresholds exceeded: stream per SearchId batch.");
        Assert.Equal(kpis.Count, DatabaseTestHost.Scalar("SELECT COUNT(*) FROM LuceneSuite_SearchDaily"));
        SqlConnection.ClearAllPools();
    }

    private static (IReadOnlyList<DailyKpi> Result, TimeSpan Elapsed, long Allocated, long PeakHeap) Measure(Func<IReadOnlyList<DailyKpi>> roll)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long heapBefore = GC.GetTotalMemory(true);
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        long peakHeap = heapBefore;
        using var sampling = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            while (!sampling.IsCancellationRequested)
            {
                peakHeap = Math.Max(peakHeap, GC.GetTotalMemory(false));
                await Task.Delay(10).ConfigureAwait(false);
            }
        });

        var watch = Stopwatch.StartNew();
        var result = roll();
        watch.Stop();
        peakHeap = Math.Max(peakHeap, GC.GetTotalMemory(false));
        sampling.Cancel();
        sampler.Wait();
        return (result, watch.Elapsed, GC.GetTotalAllocatedBytes(true) - allocatedBefore, peakHeap);
    }

    private void Report(string what, IReadOnlyList<DailyKpi> kpis, TimeSpan elapsed, long allocated, long peakHeap) =>
        output.WriteLine(
            $"{what}: {SearchIds * RowsPerSearch:N0} search rows / {InteractionCount:N0} interactions → {kpis.Count:N0} KPI rows in {elapsed.TotalSeconds:N2} s; " +
            $"allocated {allocated / 1048576.0:N0} MB; peak managed heap {peakHeap / 1048576.0:N0} MB; process peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:N0} MB");

    // 25 000 logical searches typed in 4 steps (100 000 rows) over 5 000 distinct queries, plus 20 000 clicks and conversions.
    private static SyntheticTraffic Generate()
    {
        var random = new Random(2026);
        var dayStart = RollupScheduler.Midnight(Day);
        var searches = new List<RawSearchRow>(SearchIds * RowsPerSearch);
        var lastRows = new List<RawSearchRow>(SearchIds);
        for (int s = 0; s < SearchIds; s++)
        {
            string text = $"product query {random.Next(5_000)}";
            var searchId = Guid.NewGuid();
            Guid? session = random.Next(2) == 0 ? Guid.NewGuid() : null;
            var start = dayStart.AddSeconds(random.Next(86_000));
            for (int r = 1; r <= RowsPerSearch; r++)
            {
                string typed = r == RowsPerSearch ? text : text[..(text.Length * r / RowsPerSearch)];
                searches.Add(new RawSearchRow(searches.Count + 1, searchId, r, session, start.AddSeconds(r * 3), RollupDb.Index, typed, 1, 10, random.Next(30), random.Next(1, 3000), r == RowsPerSearch));
            }

            lastRows.Add(searches[^1]);
        }

        var interactions = Enumerable.Range(0, InteractionCount).Select(i =>
        {
            var row = lastRows[random.Next(lastRows.Count)];
            return i % 5 == 0
                ? new RawInteractionRow(i + 1, row.SearchId, row.Sequence, Guid.NewGuid(), null, SearchInteractionType.Conversion, Guid.NewGuid(), row.ReceivedUtc.AddMinutes(3))
                : new RawInteractionRow(i + 1, row.SearchId, row.Sequence, Guid.NewGuid(), random.Next(1, 11), SearchInteractionType.Click, Guid.NewGuid(), row.ReceivedUtc.AddSeconds(20));
        }).ToList();
        return new SyntheticTraffic(searches, interactions);
    }
}
