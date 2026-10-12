using CMS.Activities;
using CMS.Helpers;
using CMS.Websites;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.Lucene.Suite.Activities;
using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class SearchActivityLoggerTests
{
    private static readonly Guid Page = Guid.Parse("5a5a5a5a-1111-4222-8333-944444444444");
    private static readonly Guid Reusable = Guid.Parse("6b6b6b6b-1111-4222-8333-944444444444");

    [Fact]
    public void SubmittedSearch_LogsSearchPerformed_WithScrubbedQuery()
    {
        var harness = new Harness();

        harness.Search(Request());
        harness.Search(Request() with { SearchId = Guid.NewGuid(), Sequence = 2, IsSubmit = true });

        Assert.Equal(2, harness.Activities.Logged.Count);
        var (code, data) = harness.Activities.Logged[0];
        Assert.Equal(SuiteActivityTypes.Search, code);
        Assert.Equal(("Searched for \"coffee beans\"", "coffee beans", "products"), (data.ActivityTitle, data.ActivityValue, data.ActivityComment));
        Assert.Equal(Guid.Empty, data.ActivityWebPageItemGUID);
    }

    [Theory]
    [InlineData("continuation")]
    [InlineData("browse")]
    [InlineData("untracked")]
    [InlineData("bot")]
    [InlineData("analyticsDisabled")]
    [InlineData("emptyIndex")]
    public void NonSubmittedOrExcludedSearch_LogsNothing(string scenario)
    {
        var harness = new Harness(indexDocumentCount: scenario == "emptyIndex" ? 0 : 100);
        harness.Options.Analytics.Enabled = scenario != "analyticsDisabled";
        if (scenario == "bot")
        {
            harness.Context.Request.Headers.UserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1)";
        }

        harness.Search(scenario switch
        {
            "continuation" => Request() with { SearchId = Guid.NewGuid(), Sequence = 2, Page = 2 },
            "browse" => Request() with { Query = "   " },
            "untracked" => Request() with { TrackAnalytics = false },
            _ => Request(),
        });

        Assert.Empty(harness.Activities.Logged);
    }

    [Fact]
    public void ZeroHits_AlsoLogsSearchWithoutResults()
    {
        var harness = new Harness(totalHits: 0);

        harness.Search(Request());

        Assert.Equal([SuiteActivityTypes.Search, SuiteActivityTypes.SearchNoResults], harness.Activities.Logged.Select(a => a.Code));
        Assert.Equal("Search without results: \"coffee beans\"", harness.Activities.Logged[1].Data.ActivityTitle);
    }

    [Fact]
    public void DisabledTypes_LogNothing_AndSkipThePageLookup()
    {
        var harness = new Harness(totalHits: 0, enabled: []);

        harness.Search(Request());
        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1, queryText: "coffee", resultTitle: "Grinder");

        Assert.Empty(harness.Activities.Logged);
        Assert.Equal(0, harness.Pages.Lookups);
        Assert.Single(harness.DrainInteractions());
    }

    [Fact]
    public void ActivityLoggerFailure_LeavesSearchAndAnalyticsUnchanged()
    {
        var harness = new Harness(activityFailure: true);

        var response = harness.Search(Request());
        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1);

        Assert.Equal(42, response.TotalHits);
        Assert.Equal(2, harness.DrainAll().Count);
    }

    [Fact]
    public void WithoutHttpContext_NothingIsLogged_AndNothingThrows()
    {
        var harness = new Harness();
        harness.Accessor.HttpContext = null;

        harness.Search(Request());
        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1, queryText: "coffee");

        Assert.Empty(harness.Activities.Logged);
        Assert.Equal(2, harness.DrainAll().Count);
    }

    [Fact]
    public void UrlAndReferrer_NeverCarryTheQueryString()
    {
        var harness = new Harness();
        harness.Context.Request.Path = "/search";
        harness.Context.Request.QueryString = new QueryString("?q=jane@example.com");
        harness.Context.Request.Headers.Referer = "https://user:pw@shop.example/search?q=jane@example.com#top";

        harness.Search(Request() with { Query = "jane@example.com" });
        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1, queryText: "jane@example.com");

        Assert.Equal(2, harness.Activities.Logged.Count);
        var search = harness.Activities.Logged[0].Data;
        var click = harness.Activities.Logged[1].Data;
        Assert.Equal("http://shop.local/search", search.ActivityURL);
        Assert.Equal("https://shop.example/search", search.ActivityURLReferrer);
        // A click arrives on the events endpoint; its referrer is the results page, which becomes the activity URL.
        Assert.Equal("https://shop.example/search", click.ActivityURL);
        Assert.Null(click.ActivityURLReferrer);
        foreach (var data in new[] { search, click })
        {
            Assert.Equal("{email}", data.ActivityValue);
            Assert.DoesNotContain("jane", string.Join("|", data.ActivityTitle, data.ActivityValue, data.ActivityComment, data.ActivityURL, data.ActivityURLReferrer));
        }
    }

    [Fact]
    public void Click_WithoutReferrer_KeepsTheRequestUrl()
    {
        var harness = new Harness();
        harness.Context.Request.Path = "/lucenesuite/events";

        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1);

        var data = Assert.Single(harness.Activities.Logged).Data;
        Assert.Equal("http://shop.local/lucenesuite/events", data.ActivityURL);
        Assert.Null(data.ActivityURLReferrer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://shop.example/x")]
    public void MissingOrInvalidReferrer_IsNull(string? referer)
    {
        var harness = new Harness();
        if (referer is not null)
        {
            harness.Context.Request.Headers.Referer = referer;
        }

        harness.Search(Request());

        Assert.Null(Assert.Single(harness.Activities.Logged).Data.ActivityURLReferrer);
    }

    [Fact]
    public void Texts_AreCappedAndFreeOfControlCharacters()
    {
        var harness = new Harness();

        harness.Search(Request() with { Query = new string('a', 300) });
        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 7, queryText: "cof\u0001fee", resultTitle: "Burr\u0007 \n grinder" + new string('x', 300));

        foreach (var (_, data) in harness.Activities.Logged)
        {
            Assert.InRange(data.ActivityTitle.Length, 1, SearchActivityLogger.MaxTextLength);
            Assert.InRange(data.ActivityValue.Length, 1, SearchActivityLogger.MaxTextLength);
            Assert.DoesNotContain(data.ActivityTitle + data.ActivityValue, char.IsControl);
        }

        Assert.StartsWith("Search result clicked: \"Burr grinderxxx", harness.Activities.Logged[1].Data.ActivityTitle);
    }

    [Fact]
    public void Click_LogsTitlePositionScrubbedQuery_AndLinksAnExistingWebPage()
    {
        var harness = new Harness();

        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 12, queryText: "  Coffee   0123456789 ", resultTitle: "  Burr \t grinder ");

        var (code, data) = Assert.Single(harness.Activities.Logged);
        Assert.Equal(SuiteActivityTypes.ResultClick, code);
        Assert.Equal(("Search result clicked: \"Burr grinder\" (position 12)", "coffee {number}", Page), (data.ActivityTitle, data.ActivityValue, data.ActivityWebPageItemGUID));
    }

    [Fact]
    public void Click_WithoutTitleOrQuery_OnAReusableItem_FallsBackToTheGuid_AndLinksNoPage()
    {
        var harness = new Harness();

        harness.Interactions.LogClick(Guid.NewGuid(), 1, Reusable, 3);

        var data = Assert.Single(harness.Activities.Logged).Data;
        Assert.Equal(($"Search result clicked: \"{Reusable}\" (position 3)", null, Guid.Empty), (data.ActivityTitle, data.ActivityValue, data.ActivityWebPageItemGUID));
        Assert.Equal(1, harness.Pages.Lookups);
    }

    [Fact]
    public void ClickText_NeverReachesTheAnalyticsEvent_AndConversionsLogNoActivity()
    {
        var harness = new Harness();

        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1, queryText: "secret query", resultTitle: "secret title");
        harness.Interactions.LogConversion(Guid.NewGuid(), 1, Page, 1);

        var events = harness.DrainInteractions();
        Assert.Equal(2, events.Count);
        Assert.DoesNotContain(events, e => e.ToString().Contains("secret", StringComparison.Ordinal));
        Assert.Single(harness.Activities.Logged);
    }

    [Fact]
    public void FilteredClick_LogsNoActivity()
    {
        var harness = new Harness();
        harness.Context.Request.Headers["Sec-Purpose"] = "prefetch";

        harness.Interactions.LogClick(Guid.NewGuid(), 1, Page, 1, queryText: "coffee");

        Assert.Empty(harness.Activities.Logged);
    }

    private static SuiteSearchRequest Request() => new() { IndexName = "products", Query = "  Coffee  Beans " };

    /// <summary>Activity fakes for tests that construct the capture services by hand.</summary>
    internal static SearchActivityLogger CreateLogger(IHttpContextAccessor accessor, ICustomActivityLogger? activities = null) =>
        new(activities ?? new RecordingActivities(), _ => false, _ => false, accessor, TimeProvider.System, NullLogger<SearchActivityLogger>.Instance);

    private sealed class Harness
    {
        private readonly SearchCaptureService search;

        public Harness(int totalHits = 42, int indexDocumentCount = 100, string[]? enabled = null, bool activityFailure = false)
        {
            var options = Microsoft.Extensions.Options.Options.Create(new SuiteSearchOptions());
            Options = options.Value;
            Context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton<ICurrentCookieLevelProvider>(new EssentialCookies()).BuildServiceProvider(),
            };
            Context.Request.Scheme = "http";
            Context.Request.Host = new HostString("shop.local");
            Context.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0";
            Accessor = new HttpContextAccessor { HttpContext = Context };
            Activities = new RecordingActivities(activityFailure);
            string[] enabledTypes = enabled ?? [SuiteActivityTypes.Search, SuiteActivityTypes.SearchNoResults, SuiteActivityTypes.ResultClick];
            var activityLogger = new SearchActivityLogger(
                Activities,
                enabledTypes.Contains,
                Pages.Exists,
                Accessor,
                TimeProvider.System,
                NullLogger<SearchActivityLogger>.Instance);
            var cookie = new SearchSessionCookie(Accessor, TimeProvider.System);
            var filter = new AnalyticsRequestFilter(Accessor, options);
            search = new SearchCaptureService(new FakeInner(totalHits, indexDocumentCount), Queue = new SearchEventQueue(options), cookie, filter, activityLogger, TimeProvider.System, NullLogger<SearchCaptureService>.Instance);
            Interactions = new SearchInteractionLogger(Queue, cookie, filter, activityLogger, TimeProvider.System, NullLogger<SearchInteractionLogger>.Instance);
        }

        public SuiteSearchOptions Options { get; }

        public DefaultHttpContext Context { get; }

        public HttpContextAccessor Accessor { get; }

        public RecordingActivities Activities { get; }

        public FakeWebPages Pages { get; } = new(Page);

        public SearchEventQueue Queue { get; }

        public ISearchInteractionLogger Interactions { get; }

        public SuiteSearchResponse Search(SuiteSearchRequest request) => search.Search(request);

        public List<AnalyticsEvent> DrainAll()
        {
            var events = new List<AnalyticsEvent>();
            while (Queue.Reader.TryRead(out var e))
            {
                events.Add(e);
            }

            return events;
        }

        public List<SearchInteractionEvent> DrainInteractions() => [.. DrainAll().OfType<SearchInteractionEvent>()];
    }

    internal sealed class RecordingActivities(bool fail = false) : ICustomActivityLogger
    {
        public List<(string Code, CustomActivityData Data)> Logged { get; } = [];

        public void Log(string customActivityType, CustomActivityData activityData)
        {
            if (fail)
            {
                throw new InvalidOperationException("activity store down");
            }

            Logged.Add((customActivityType, activityData));
        }
    }

    private sealed class FakeWebPages(params Guid[] pages)
    {
        public int Lookups { get; private set; }

        public bool Exists(Guid guid)
        {
            Lookups++;
            return pages.Contains(guid);
        }
    }

    private sealed class FakeInner(int totalHits, int indexDocumentCount) : ISuiteSearchService
    {
        public SuiteSearchResponse Search(SuiteSearchRequest request) =>
            new([], totalHits, [], TimeSpan.Zero) { IndexDocumentCount = indexDocumentCount };
    }

    private sealed class EssentialCookies : ICurrentCookieLevelProvider
    {
        public int GetCurrentCookieLevel() => CookieLevelConstants.ESSENTIAL;

        public int GetDefaultCookieLevel() => CookieLevelConstants.ESSENTIAL;

        public void SetCurrentCookieLevel(int cookieLevel) => throw new NotSupportedException();
    }
}
