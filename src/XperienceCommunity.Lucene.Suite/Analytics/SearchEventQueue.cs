using System.Threading.Channels;

using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>An analytics event waiting to be written.</summary>
internal abstract record AnalyticsEvent;

/// <summary>A captured search, already scrubbed and length-checked, waiting to be written.</summary>
internal sealed record SearchEvent(
    Guid SearchId,
    int Sequence,
    Guid? SessionId,
    DateTime ReceivedUtc,
    string IndexName,
    string QueryText,
    int Page,
    int PageSize,
    int FacetFilterCount,
    int ResultCount,
    int LatencyMs,
    bool IsSubmit,
    SearchEventOrigin Origin) : AnalyticsEvent;

/// <summary>A validated click or conversion waiting to be written.</summary>
internal sealed record SearchInteractionEvent(
    Guid SearchId,
    int Sequence,
    Guid ItemGuid,
    int? Position,
    SearchInteractionType Type,
    Guid EventId,
    Guid? SessionId,
    DateTime ReceivedUtc) : AnalyticsEvent;

/// <summary>Bounded in-memory buffer between the search path and <see cref="SearchEventWriter"/>; drops when full.</summary>
internal sealed class SearchEventQueue
{
    private readonly Channel<AnalyticsEvent> channel;
    private long dropped;
    private long droppedInteractions;

    public SearchEventQueue(IOptions<SuiteSearchOptions> options)
    {
        channel = Channel.CreateBounded<AnalyticsEvent>(
            new BoundedChannelOptions(Math.Max(1, options.Value.Analytics.QueueCapacity))
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            },
            // With DropWrite, TryWrite still returns true for a dropped item; this callback is the only signal.
            item =>
            {
                Interlocked.Increment(ref dropped);
                if (item is SearchInteractionEvent)
                {
                    Interlocked.Increment(ref droppedInteractions);
                }
            });
    }

    /// <summary>Total events (searches and interactions) dropped because the buffer was full.</summary>
    public long Dropped => Interlocked.Read(ref dropped);

    /// <summary>Interaction events among <see cref="Dropped"/>.</summary>
    public long DroppedInteractions => Interlocked.Read(ref droppedInteractions);

    public ChannelReader<AnalyticsEvent> Reader => channel.Reader;

    /// <summary>Enqueues without blocking.</summary>
    public void Enqueue(AnalyticsEvent analyticsEvent) => channel.Writer.TryWrite(analyticsEvent);
}

/// <summary>Lets one log entry through per interval.</summary>
internal sealed class LogThrottle(TimeProvider timeProvider, TimeSpan interval)
{
    private long nextTicks;

    public bool ShouldLog()
    {
        long now = timeProvider.GetUtcNow().UtcTicks;
        long next = Interlocked.Read(ref nextTicks);
        return now >= next && Interlocked.CompareExchange(ref nextTicks, now + interval.Ticks, next) == next;
    }
}
