using CMS.Helpers;
using CMS.Websites;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SearchCaptureTests
{
    private const string BrowserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0";

    [Fact]
    public void ClientIds_AreStoredUnchanged_WithAllFields()
    {
        var searchId = Guid.NewGuid();
        var harness = new Harness();

        var response = harness.Search(new SuiteSearchRequest
        {
            IndexName = "products",
            Query = "  Coffee  Beans ",
            Page = 2,
            PageSize = 20,
            Facets = new Dictionary<string, IReadOnlyList<string>> { ["Brand"] = ["a", "b"], ["Size"] = ["l"] },
            SearchId = searchId,
            Sequence = 4,
        });

        var e = Assert.Single(harness.Drain());
        Assert.Equal(searchId, response.SearchId);
        Assert.Equal(searchId, e.SearchId);
        Assert.Equal(4, e.Sequence);
        Assert.Equal("products", e.IndexName);
        Assert.Equal("coffee beans", e.QueryText);
        Assert.Equal((2, 20, 3, 42, 13), (e.Page, e.PageSize, e.FacetFilterCount, e.ResultCount, e.LatencyMs));
        Assert.False(e.IsSubmit);
        Assert.Equal(SearchEventOrigin.Client, e.Origin);
        Assert.Equal(Harness.Now.UtcDateTime, e.ReceivedUtc);
    }

    [Fact]
    public void ClientSubmitFlag_IsStored()
    {
        var harness = new Harness();

        harness.Search(Request() with { SearchId = Guid.NewGuid(), Sequence = 2, IsSubmit = true });

        Assert.True(Assert.Single(harness.Drain()).IsSubmit);
    }

    [Fact]
    public void MissingSearchId_IsMintedAsServerSubmit_AndReturned()
    {
        var harness = new Harness();

        var response = harness.Search(Request() with { Sequence = 7 });

        var e = Assert.Single(harness.Drain());
        Assert.NotEqual(Guid.Empty, response.SearchId);
        Assert.Equal(response.SearchId, e.SearchId);
        Assert.Equal(1, e.Sequence);
        Assert.True(e.IsSubmit);
        Assert.Equal(SearchEventOrigin.Server, e.Origin);
    }

    [Fact]
    public void SearchIdIsMinted_EvenWhenNotTracked()
    {
        var harness = new Harness();

        var response = harness.Search(Request() with { TrackAnalytics = false });

        Assert.NotEqual(Guid.Empty, response.SearchId);
    }

    [Fact]
    public void QueryText_IsScrubbed()
    {
        var harness = new Harness();

        harness.Search(Request() with { Query = "Jane@Example.com 0123456789" });

        Assert.Equal("{email} {number}", Assert.Single(harness.Drain()).QueryText);
    }

    [Fact]
    public void OverlongIndexName_IsTruncatedToColumnLength()
    {
        var harness = new Harness();

        harness.Search(Request() with { IndexName = new string('x', 300) });

        Assert.Equal(SearchEventInfo.IndexNameMaxLength, Assert.Single(harness.Drain()).IndexName.Length);
    }

    [Theory]
    [InlineData("untracked")]
    [InlineData("disabled")]
    [InlineData("emptyIndex")]
    [InlineData("googlebot")]
    [InlineData("noUserAgent")]
    [InlineData("secPurposePrefetch")]
    [InlineData("secPurposePrerender")]
    [InlineData("purposePrefetch")]
    public void ExcludedSearches_ReturnResults_ButEnqueueNothing(string scenario)
    {
        var harness = new Harness(
            userAgent: scenario switch { "googlebot" => "Mozilla/5.0 (compatible; Googlebot/2.1)", "noUserAgent" => null, _ => BrowserAgent },
            indexDocumentCount: scenario == "emptyIndex" ? 0 : 100);
        harness.Options.Analytics.Enabled = scenario != "disabled";
        var headers = harness.Context.Request.Headers;
        switch (scenario)
        {
            case "secPurposePrefetch": headers["Sec-Purpose"] = "prefetch"; break;
            case "secPurposePrerender": headers["Sec-Purpose"] = "prefetch;prerender"; break;
            case "purposePrefetch": headers["Purpose"] = "prefetch"; break;
        }

        var response = harness.Search(Request() with { TrackAnalytics = scenario != "untracked" });

        Assert.Equal(42, response.TotalHits);
        Assert.Empty(harness.Drain());
    }

    [Fact]
    public void UnknownDocumentCount_IsCaptured()
    {
        var harness = new Harness(indexDocumentCount: null);

        harness.Search(Request());

        Assert.Single(harness.Drain());
    }

    [Fact]
    public void OutsideHttpRequest_IsCaptured_WithoutSession()
    {
        var harness = new Harness();
        harness.Accessor.HttpContext = null;

        harness.Search(Request());

        Assert.Null(Assert.Single(harness.Drain()).SessionId);
    }

    [Fact]
    public void FullQueue_DropsAndCounts_WithoutBlocking()
    {
        var harness = new Harness(queueCapacity: 2);

        for (int i = 0; i < 5; i++)
        {
            harness.Search(Request());
        }

        Assert.Equal(2, harness.Drain().Count);
        Assert.Equal(3, harness.Queue.Dropped);
    }

    [Fact]
    public void CaptureFailure_NeverReachesCaller()
    {
        var harness = new Harness(cookieLevel: new ThrowingCookieLevelProvider());

        var response = harness.Search(Request());

        Assert.Equal(42, response.TotalHits);
        Assert.Empty(harness.Drain());
    }

    [Fact]
    public void InnerSearchFailure_Propagates()
    {
        var harness = new Harness(innerFailure: new ArgumentException("bad"));

        Assert.Throws<ArgumentException>(() => harness.Search(Request()));
        Assert.Empty(harness.Drain());
    }

    [Theory]
    [InlineData("searchIdEmpty")]
    [InlineData("sequence0")]
    [InlineData("sequenceNegative")]
    public void InvalidSearchIdOrSequence_IsRejectedBeforeSearch(string scenario)
    {
        var request = scenario switch
        {
            "searchIdEmpty" => Request() with { SearchId = Guid.Empty, Sequence = 1 },
            "sequence0" => Request() with { SearchId = Guid.NewGuid(), Sequence = 0 },
            _ => Request() with { SearchId = Guid.NewGuid(), Sequence = -3 },
        };

        Assert.ThrowsAny<ArgumentException>(() => SearchExecutor.Validate(request));
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(0)]
    [InlineData(100)]
    public void SessionCookie_BelowVisitor_IsNullAndNotWritten(int level)
    {
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(level));

        Assert.Null(harness.Cookie.GetSessionId());
        Assert.False(harness.Context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Theory]
    [InlineData(CookieLevelConstants.VISITOR)]
    [InlineData(CookieLevelConstants.ALL)]
    public void SessionCookie_AtVisitorOrAll_IsCreatedWithSlidingExpiry(int level)
    {
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(level));

        var id = harness.Cookie.GetSessionId();

        Assert.NotNull(id);
        string setCookie = harness.Context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{SearchSessionCookie.Name}={id:N}", setCookie);
        Assert.Contains("expires=" + (Harness.Now + SearchSessionCookie.SlidingExpiration).ToString("R"), setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionCookie_Existing_IsReusedAndReissued()
    {
        var existing = Guid.NewGuid();
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(CookieLevelConstants.VISITOR));
        harness.Context.Request.Headers.Cookie = $"{SearchSessionCookie.Name}={existing:N}";

        Assert.Equal(existing, harness.Cookie.GetSessionId());
        Assert.Contains($"{SearchSessionCookie.Name}={existing:N}", harness.Context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void SessionCookie_ResponseStarted_UsesExistingWithoutWriting()
    {
        var existing = Guid.NewGuid();
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(CookieLevelConstants.ALL));
        harness.Context.Request.Headers.Cookie = $"{SearchSessionCookie.Name}={existing:N}";
        harness.Context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        Assert.Equal(existing, harness.Cookie.GetSessionId());
        Assert.False(harness.Context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public void SessionCookie_ResponseStarted_WithoutCookie_IsNull()
    {
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(CookieLevelConstants.ALL));
        harness.Context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        Assert.Null(harness.Cookie.GetSessionId());
    }

    [Fact]
    public void SessionCookie_NoHttpContext_IsNull()
    {
        var harness = new Harness();
        harness.Accessor.HttpContext = null;

        Assert.Null(harness.Cookie.GetSessionId());
    }

    [Fact]
    public void SessionCookie_IsResolvedPerRequest()
    {
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(CookieLevelConstants.VISITOR));
        Assert.NotNull(harness.Cookie.GetSessionId());

        harness.Accessor.HttpContext = Harness.CreateContext(BrowserAgent, new FixedCookieLevelProvider(CookieLevelConstants.ESSENTIAL));

        Assert.Null(harness.Cookie.GetSessionId());
    }

    [Fact]
    public void CapturedSearch_AtVisitor_StoresSessionId()
    {
        var harness = new Harness(cookieLevel: new FixedCookieLevelProvider(CookieLevelConstants.VISITOR));

        harness.Search(Request());

        Assert.NotNull(Assert.Single(harness.Drain()).SessionId);
    }

    private static SuiteSearchRequest Request() => new() { IndexName = "products", Query = "coffee" };

    private sealed class Harness
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        private readonly SearchCaptureService service;

        public Harness(
            string? userAgent = BrowserAgent,
            int? indexDocumentCount = 100,
            int queueCapacity = 100,
            ICurrentCookieLevelProvider? cookieLevel = null,
            Exception? innerFailure = null)
        {
            var time = new FixedTimeProvider(Now);
            var options = Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions());
            options.Value.Analytics.QueueCapacity = queueCapacity;
            Options = options.Value;
            Context = CreateContext(userAgent, cookieLevel ?? new FixedCookieLevelProvider(CookieLevelConstants.ESSENTIAL));
            Accessor = new HttpContextAccessor { HttpContext = Context };
            Queue = new SearchEventQueue(options);
            Cookie = new SearchSessionCookie(Accessor, time);
            service = new SearchCaptureService(
                new FakeInner(indexDocumentCount, innerFailure), Queue, Cookie, new AnalyticsRequestFilter(Accessor, options), time, NullLogger<SearchCaptureService>.Instance);
        }

        public SuiteSearchOptions Options { get; }

        public DefaultHttpContext Context { get; }

        public HttpContextAccessor Accessor { get; }

        public SearchEventQueue Queue { get; }

        public SearchSessionCookie Cookie { get; }

        public static DefaultHttpContext CreateContext(string? userAgent, ICurrentCookieLevelProvider cookieLevel)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton(cookieLevel).BuildServiceProvider(),
            };
            if (userAgent is not null)
            {
                context.Request.Headers.UserAgent = userAgent;
            }

            return context;
        }

        public SuiteSearchResponse Search(SuiteSearchRequest request) => service.Search(request);

        public List<SearchEvent> Drain()
        {
            var events = new List<SearchEvent>();
            while (Queue.Reader.TryRead(out var e))
            {
                events.Add((SearchEvent)e);
            }

            return events;
        }
    }

    private sealed class FakeInner(int? indexDocumentCount, Exception? failure) : ISuiteSearchService
    {
        public SuiteSearchResponse Search(SuiteSearchRequest request) =>
            failure is not null
                ? throw failure
                : new SuiteSearchResponse([], 42, [], TimeSpan.FromMilliseconds(12.6)) { IndexDocumentCount = indexDocumentCount };
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

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
