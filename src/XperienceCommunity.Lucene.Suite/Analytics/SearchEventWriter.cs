using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Drains <see cref="SearchEventQueue"/> into bulk inserts of <see cref="SearchEventInfo"/> and <see cref="SearchInteractionInfo"/>; failed batches are logged and discarded.</summary>
internal sealed class SearchEventWriter(
    SearchEventQueue queue,
    IServiceProvider serviceProvider,
    IOptions<SuiteSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<SearchEventWriter> logger) : BackgroundService
{
    private const int BulkCopyTimeoutSeconds = 30;
    private const int ShutdownBulkCopyTimeoutSeconds = 5;

    private readonly LogThrottle failureThrottle = new(timeProvider, TimeSpan.FromMinutes(1));
    private readonly LogThrottle interactionFailureThrottle = new(timeProvider, TimeSpan.FromMinutes(1));
    private readonly LogThrottle dropThrottle = new(timeProvider, TimeSpan.FromMinutes(1));
    private long reportedDropped;
    private long reportedDroppedInteractions;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var analytics = options.Value.Analytics;
        int batchSize = Math.Max(1, analytics.BatchSize);
        var batch = new List<AnalyticsEvent>(batchSize);

        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                using (var window = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                {
                    window.CancelAfter(analytics.FlushInterval);
                    try
                    {
                        while (batch.Count < batchSize)
                        {
                            if (queue.Reader.TryRead(out var item))
                            {
                                batch.Add(item);
                            }
                            else if (!await queue.Reader.WaitToReadAsync(window.Token).ConfigureAwait(false))
                            {
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        // Flush interval elapsed with a partial batch.
                    }
                }

                Write(batch, BulkCopyTimeoutSeconds);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            while (queue.Reader.TryRead(out var item))
            {
                batch.Add(item);
            }

            Write(batch, ShutdownBulkCopyTimeoutSeconds);
        }
    }

    private void Write(List<AnalyticsEvent> batch, int timeoutSeconds)
    {
        ReportDrops();
        try
        {
            var settings = new BulkInsertSettings { BulkCopyTimeout = timeoutSeconds };
            var searches = batch.OfType<SearchEvent>().Select(ToInfo).ToList();
            if (searches.Count > 0)
            {
                Insert(searches, settings, failureThrottle, "search events");
            }

            var interactions = batch.OfType<SearchInteractionEvent>().Select(ToInfo).ToList();
            if (interactions.Count > 0)
            {
                Insert(interactions, settings, interactionFailureThrottle, "search interactions");
            }
        }
        finally
        {
            batch.Clear();
        }
    }

    private void Insert<TInfo>(List<TInfo> infos, BulkInsertSettings settings, LogThrottle throttle, string kind)
        where TInfo : AbstractInfoBase<TInfo>, new()
    {
        try
        {
            serviceProvider.GetRequiredService<IInfoProvider<TInfo>>().BulkInsert(infos, settings);
        }
        catch (Exception exception)
        {
            if (throttle.ShouldLog())
            {
                logger.LogError(exception, "Lucene Suite could not write {Count} {Kind}; the batch was discarded (further failures suppressed for a minute).", infos.Count, kind);
            }
        }
    }

    private void ReportDrops()
    {
        long dropped = queue.Dropped;
        long droppedInteractions = queue.DroppedInteractions;
        long newlyDropped = dropped - reportedDropped;
        if (newlyDropped > 0 && dropThrottle.ShouldLog())
        {
            long newlyDroppedInteractions = droppedInteractions - reportedDroppedInteractions;
            reportedDropped = dropped;
            reportedDroppedInteractions = droppedInteractions;
            logger.LogWarning(
                "Lucene Suite dropped {Count} analytics events ({Searches} search events, {Interactions} interactions) because the capture queue was full.",
                newlyDropped,
                newlyDropped - newlyDroppedInteractions,
                newlyDroppedInteractions);
        }
    }

    private static SearchEventInfo ToInfo(SearchEvent e) => new()
    {
        SearchId = e.SearchId,
        Sequence = e.Sequence,
        SessionId = e.SessionId,
        ReceivedUtc = e.ReceivedUtc,
        IndexName = e.IndexName,
        QueryText = e.QueryText,
        Page = e.Page,
        PageSize = e.PageSize,
        FacetFilterCount = e.FacetFilterCount,
        ResultCount = e.ResultCount,
        LatencyMs = e.LatencyMs,
        IsSubmit = e.IsSubmit,
        Origin = e.Origin,
    };

    private static SearchInteractionInfo ToInfo(SearchInteractionEvent e) => new()
    {
        SearchId = e.SearchId,
        Sequence = e.Sequence,
        ItemGuid = e.ItemGuid,
        Position = e.Position,
        Type = e.Type,
        EventId = e.EventId,
        SessionId = e.SessionId,
        ReceivedUtc = e.ReceivedUtc,
    };
}
