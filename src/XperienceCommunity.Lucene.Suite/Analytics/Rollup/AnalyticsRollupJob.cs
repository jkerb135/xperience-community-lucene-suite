using System.Globalization;

using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Outcome of one rollup run.</summary>
internal sealed record RollupRun(bool LockAcquired, bool Installed, DateOnly? SkippedThrough, IReadOnlyList<DateOnly> Rolled);

/// <summary>Hosted job on every node: after a short delay and then every RollupInterval, the node holding the rollup lock rolls completed days and enforces retention.</summary>
internal sealed class AnalyticsRollupJob(
    IServiceProvider serviceProvider,
    IOptions<SuiteSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<AnalyticsRollupJob> logger) : BackgroundService
{
    /// <summary>Delay before the first run, so startup and the installer finish first.</summary>
    internal static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);

    private readonly LogThrottle failureThrottle = new(timeProvider, TimeSpan.FromHours(1));

    /// <summary>One run: lock, installed-version gate, watermark, schedule, roll each day, retention. Returns without work when another node holds the lock.</summary>
    internal RollupRun RunOnce(CancellationToken cancellationToken)
    {
        using var rollupLock = RollupLock.TryAcquire(ConnectionHelper.ConnectionString);
        if (rollupLock is null)
        {
            return new RollupRun(false, false, null, []);
        }

        var store = serviceProvider.GetRequiredService<DailyRollupStore>();
        if (store.ReadInstalledVersion() < AnalyticsModuleInstaller.Version)
        {
            return new RollupRun(true, false, null, []);
        }

        var analytics = options.Value.Analytics;
        var now = timeProvider.GetUtcNow();
        var watermark = store.ReadWatermark();
        var plan = RollupScheduler.Plan(watermark, watermark is null ? store.EarliestRawDay() : null, now, analytics);
        if (plan.SkipThrough is DateOnly skipThrough)
        {
            store.WriteWatermark(skipThrough);
            watermark = skipThrough;
            logger.LogError(
                "Lucene Suite skipped the rollup of UTC days up to {Day}: their raw rows may already be past Retention:RawRetention, so rolling them could replace correct KPIs with partial ones. Their existing daily KPIs were kept.",
                skipThrough.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        var settings = RollupSettings.From(analytics);
        foreach (var day in plan.Days)
        {
            cancellationToken.ThrowIfCancellationRequested();
            store.Replace(day, store.Compute(day, settings));
            watermark = day;
        }

        serviceProvider.GetRequiredService<AnalyticsRetention>().Run(now, watermark, analytics);
        return new RollupRun(true, true, plan.SkipThrough, plan.Days);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var analytics = options.Value.Analytics;
        if (!analytics.Enabled)
        {
            return;
        }

        try
        {
            await Task.Delay(StartDelay, timeProvider, stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(analytics.Job.RollupInterval, timeProvider);
            do
            {
                Tick(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private void Tick(CancellationToken stoppingToken)
    {
        try
        {
            RunOnce(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            if (failureThrottle.ShouldLog())
            {
                logger.LogError(exception, "Lucene Suite analytics rollup failed; it is retried on the next run (further failures suppressed for an hour).");
            }
        }
    }
}
