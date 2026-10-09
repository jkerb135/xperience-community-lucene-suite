using CMS.Helpers;
using CMS.Websites;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SearchInteractionLoggerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SearchId = Guid.NewGuid();
    private static readonly Guid ItemGuid = Guid.NewGuid();

    [Fact]
    public void Click_IsEnqueued_WithAllFields()
    {
        var eventId = Guid.NewGuid();
        var harness = new Harness();

        harness.Logger.LogClick(SearchId, 3, ItemGuid, 12, eventId);

        var e = Assert.Single(harness.Drain());
        Assert.Equal(new SearchInteractionEvent(SearchId, 3, ItemGuid, 12, SearchInteractionType.Click, eventId, null, Now.UtcDateTime), e);
    }

    [Fact]
    public void Conversion_WithoutPosition_IsEnqueued_AndEventIdIsMinted()
    {
        var harness = new Harness();

        harness.Logger.LogConversion(SearchId, 1, ItemGuid);

        var e = Assert.Single(harness.Drain());
        Assert.Equal(SearchInteractionType.Conversion, e.Type);
        Assert.Null(e.Position);
        Assert.NotEqual(Guid.Empty, e.EventId);
    }

    [Fact]
    public void Conversion_WithPosition_KeepsIt()
    {
        var harness = new Harness();

        harness.Logger.LogConversion(SearchId, 2, ItemGuid, SearchExecutor.MaxResultWindow);

        Assert.Equal(SearchExecutor.MaxResultWindow, Assert.Single(harness.Drain()).Position);
    }

    [Theory]
    [InlineData("searchIdEmpty")]
    [InlineData("itemGuidEmpty")]
    [InlineData("eventIdEmpty")]
    [InlineData("sequence0")]
    [InlineData("position0")]
    [InlineData("positionNegative")]
    [InlineData("positionOverWindow")]
    [InlineData("conversionPosition0")]
    [InlineData("conversionPositionOverWindow")]
    public void InvalidArguments_Throw_AndEnqueueNothing(string scenario)
    {
        var harness = new Harness();
        var logger = harness.Logger;

        Action call = scenario switch
        {
            "searchIdEmpty" => () => logger.LogClick(Guid.Empty, 1, ItemGuid, 1),
            "itemGuidEmpty" => () => logger.LogClick(SearchId, 1, Guid.Empty, 1),
            "eventIdEmpty" => () => logger.LogClick(SearchId, 1, ItemGuid, 1, Guid.Empty),
            "sequence0" => () => logger.LogClick(SearchId, 0, ItemGuid, 1),
            "position0" => () => logger.LogClick(SearchId, 1, ItemGuid, 0),
            "positionNegative" => () => logger.LogClick(SearchId, 1, ItemGuid, -5),
            "positionOverWindow" => () => logger.LogClick(SearchId, 1, ItemGuid, SearchExecutor.MaxResultWindow + 1),
            "conversionPosition0" => () => logger.LogConversion(SearchId, 1, ItemGuid, 0),
            _ => () => logger.LogConversion(SearchId, 1, ItemGuid, SearchExecutor.MaxResultWindow + 1),
        };

        Assert.ThrowsAny<ArgumentException>(call);
        Assert.Empty(harness.Drain());
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("googlebot")]
    [InlineData("noUserAgent")]
    [InlineData("secPurposePrefetch")]
    [InlineData("purposePrefetch")]
    public void ExcludedRequests_EnqueueNothing_AndDoNotThrow(string scenario)
    {
        var harness = new Harness(userAgent: scenario switch
        {
            "googlebot" => "Mozilla/5.0 (compatible; Googlebot/2.1)",
            "noUserAgent" => null,
            _ => Harness.BrowserAgent,
        });
        harness.Options.Analytics.Enabled = scenario != "disabled";
        switch (scenario)
        {
            case "secPurposePrefetch": harness.Context.Request.Headers["Sec-Purpose"] = "prefetch"; break;
            case "purposePrefetch": harness.Context.Request.Headers["Purpose"] = "prefetch"; break;
        }

        harness.Logger.LogClick(SearchId, 1, ItemGuid, 1);
        harness.Logger.LogConversion(SearchId, 1, ItemGuid);

        Assert.Empty(harness.Drain());
    }

    [Theory]
    [InlineData(CookieLevelConstants.ESSENTIAL, false)]
    [InlineData(CookieLevelConstants.VISITOR, true)]
    [InlineData(CookieLevelConstants.ALL, true)]
    public void SessionId_FollowsCookieLevel(int level, bool expected)
    {
        var harness = new Harness(cookieLevel: level);

        harness.Logger.LogClick(SearchId, 1, ItemGuid, 1);

        Assert.Equal(expected, Assert.Single(harness.Drain()).SessionId.HasValue);
    }

    [Fact]
    public void OutsideHttpRequest_IsRecorded_WithoutSession()
    {
        var harness = new Harness(cookieLevel: CookieLevelConstants.ALL);
        harness.Accessor.HttpContext = null;

        harness.Logger.LogClick(SearchId, 1, ItemGuid, 1);

        Assert.Null(Assert.Single(harness.Drain()).SessionId);
    }

    [Fact]
    public void CaptureFailure_NeverReachesCaller()
    {
        var harness = new Harness(cookieLevelProvider: new ThrowingCookieLevelProvider());

        harness.Logger.LogClick(SearchId, 1, ItemGuid, 1);

        Assert.Empty(harness.Drain());
    }

    [Fact]
    public void FullQueue_DropsAndCountsInteractions_WithoutBlocking()
    {
        var harness = new Harness(queueCapacity: 2);

        for (int i = 0; i < 5; i++)
        {
            harness.Logger.LogClick(SearchId, 1, ItemGuid, 1);
        }

        Assert.Equal(2, harness.Drain().Count);
        Assert.Equal(3, harness.Queue.Dropped);
        Assert.Equal(3, harness.Queue.DroppedInteractions);
    }

    [Fact]
    public void AddLuceneSuite_RegistersLogger()
    {
        var services = new ServiceCollection().AddLogging().AddLuceneSuite().BuildServiceProvider();

        Assert.IsType<SearchInteractionLogger>(services.GetRequiredService<ISearchInteractionLogger>());
    }

    private sealed class Harness
    {
        public const string BrowserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0";

        public Harness(
            string? userAgent = BrowserAgent,
            int cookieLevel = CookieLevelConstants.ESSENTIAL,
            ICurrentCookieLevelProvider? cookieLevelProvider = null,
            int queueCapacity = 100)
        {
            var time = new FixedTimeProvider(Now);
            var options = Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions());
            options.Value.Analytics.QueueCapacity = queueCapacity;
            Options = options.Value;
            Context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddSingleton(cookieLevelProvider ?? new FixedCookieLevelProvider(cookieLevel))
                    .BuildServiceProvider(),
            };
            if (userAgent is not null)
            {
                Context.Request.Headers.UserAgent = userAgent;
            }

            Accessor = new HttpContextAccessor { HttpContext = Context };
            Queue = new SearchEventQueue(options);
            Logger = new SearchInteractionLogger(
                Queue,
                new SearchSessionCookie(Accessor, time),
                new AnalyticsRequestFilter(Accessor, options),
                time,
                NullLogger<SearchInteractionLogger>.Instance);
        }

        public SuiteSearchOptions Options { get; }

        public DefaultHttpContext Context { get; }

        public HttpContextAccessor Accessor { get; }

        public SearchEventQueue Queue { get; }

        public ISearchInteractionLogger Logger { get; }

        public List<SearchInteractionEvent> Drain()
        {
            var events = new List<SearchInteractionEvent>();
            while (Queue.Reader.TryRead(out var e))
            {
                events.Add((SearchInteractionEvent)e);
            }

            return events;
        }
    }

    private sealed class FixedCookieLevelProvider(int level) : ICurrentCookieLevelProvider
    {
        public int GetCurrentCookieLevel() => level;

        public int GetDefaultCookieLevel() => level;

        public void SetCurrentCookieLevel(int cookieLevel) => throw new NotSupportedException();
    }

    private sealed class ThrowingCookieLevelProvider : ICurrentCookieLevelProvider
    {
        public int GetCurrentCookieLevel() => throw new InvalidOperationException("cookie level unavailable");

        public int GetDefaultCookieLevel() => throw new InvalidOperationException("cookie level unavailable");

        public void SetCurrentCookieLevel(int cookieLevel) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
