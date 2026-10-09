using System.Threading.Channels;

using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

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
    SearchEventOrigin Origin);

/// <summary>Bounded in-memory buffer between the search path and <see cref="SearchEventWriter"/>; drops when full.</summary>
internal sealed class SearchEventQueue
{
    private readonly Channel<SearchEvent> channel;
    private long dropped;

    public SearchEventQueue(IOptions<SuiteSearchOptions> options)
    {
        channel = Channel.CreateBounded<SearchEvent>(
            new BoundedChannelOptions(Math.Max(1, options.Value.Analytics.QueueCapacity))
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            },
            // With DropWrite, TryWrite still returns true for a dropped item; this callback is the only signal.
            _ => Interlocked.Increment(ref dropped));
    }

    /// <summary>Total events dropped because the buffer was full.</summary>
    public long Dropped => Interlocked.Read(ref dropped);

    public ChannelReader<SearchEvent> Reader => channel.Reader;

    /// <summary>Enqueues without blocking.</summary>
    public void Enqueue(SearchEvent searchEvent) => channel.Writer.TryWrite(searchEvent);
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
