using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Drains <see cref="SearchEventQueue"/> into bulk inserts of <see cref="SearchEventInfo"/>; failed batches are logged and discarded.</summary>
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
    private readonly LogThrottle dropThrottle = new(timeProvider, TimeSpan.FromMinutes(1));
    private long reportedDropped;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var analytics = options.Value.Analytics;
        int batchSize = Math.Max(1, analytics.BatchSize);
        var batch = new List<SearchEvent>(batchSize);

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

    private void Write(List<SearchEvent> batch, int timeoutSeconds)
    {
        ReportDrops();
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            serviceProvider.GetRequiredService<IInfoProvider<SearchEventInfo>>()
                .BulkInsert(batch.Select(ToInfo), new BulkInsertSettings { BulkCopyTimeout = timeoutSeconds });
        }
        catch (Exception exception)
        {
            if (failureThrottle.ShouldLog())
            {
                logger.LogError(exception, "Lucene Suite could not write {Count} search events; the batch was discarded (further failures suppressed for a minute).", batch.Count);
            }
        }
        finally
        {
            batch.Clear();
        }
    }

    private void ReportDrops()
    {
        long dropped = queue.Dropped;
        long newlyDropped = dropped - reportedDropped;
        if (newlyDropped > 0 && dropThrottle.ShouldLog())
        {
            reportedDropped = dropped;
            logger.LogWarning("Lucene Suite dropped {Count} search events because the capture queue was full.", newlyDropped);
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
}
