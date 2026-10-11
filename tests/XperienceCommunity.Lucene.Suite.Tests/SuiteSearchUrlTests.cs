using System.Runtime.CompilerServices;

using CMS.ContactManagement;
using CMS.DataEngine;
using CMS.Helpers;

using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets;
using XperienceCommunity.Lucene.Suite.Widgets.Events;
using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>The search URL model, the per-request search and contact-group contexts.</summary>
public sealed class SuiteSearchUrlTests
{
    internal const string Index = "products";
    private static readonly Guid Sid = Guid.Parse("8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b");

    [Fact]
    public void Parse_ReadsEveryParameter()
    {
        var url = Parse("?q=%20coffee%20grinder%20&page=3&f.ContentType=a&f.ContentType=b&f.Brand=x&sid=" + Sid.ToString("N") + "&seq=4");

        Assert.Equal("coffee grinder", url.Query);
        Assert.Equal(3, url.Page);
        Assert.Equal(["a", "b"], url.Facets["ContentType"]);
        Assert.Equal(["x"], url.Facets["Brand"]);
        Assert.Equal((Sid, 4), (url.SearchId, url.Sequence));
        Assert.False(url.Invalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?page=abc")]
    [InlineData("?page=101")]
    [InlineData("?page=99999999999999")]
    [InlineData("?sid=nope&seq=2")]
    [InlineData("?sid=00000000000000000000000000000000&seq=2")]
    [InlineData("?sid=8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b")]
    [InlineData("?sid=8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b&seq=0")]
    [InlineData("?sid=8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b&seq=10001")]
    [InlineData("?f.=x&f.Empty=")]
    public void Parse_FallsBackToDefaults(string queryString)
    {
        var url = Parse(queryString);

        Assert.Equal((1, (Guid?)null, (int?)null, 0), (url.Page, url.SearchId, url.Sequence, url.Facets.Count));
        Assert.True(url.IsEmpty);
    }

    [Fact]
    public void Parse_LimitsAndInvalidText()
    {
        string many = string.Join("&", Enumerable.Range(0, 15).Select(d => $"f.d{d}=v"));
        string values = string.Join("&", Enumerable.Range(0, 30).Select(v => $"f.big=v{v}"));
        var url = Parse($"?q={new string('x', 257)}&{many}&{values}&f.long={new string('y', 257)}&f.dup=a&f.dup=a");

        Assert.True(url.Invalid);
        Assert.Equal(string.Empty, url.Query);
        Assert.False(url.IsEmpty);
        Assert.Equal(SuiteSearchUrl.MaxDimensions, url.Facets.Count);
        Assert.Equal(1000 / 10, Parse("?page=100").Page);
        Assert.Equal(SuiteSearchUrl.MaxValuesPerDimension, Parse($"?{values}").Facets["big"].Count);
        Assert.False(Parse($"?f.long={new string('y', 257)}").Facets.ContainsKey("long"));
        Assert.Equal(["a"], Parse("?f.dup=a&f.dup=a").Facets["dup"]);
    }

    [Fact]
    public void Links_RoundTripEncodedValues_AndContinueTheSearch()
    {
        var url = Parse("?q=" + Uri.EscapeDataString("café & co = 1") + "&f." + Uri.EscapeDataString("Content.Type") + "=" + Uri.EscapeDataString("a&b=c d") + "&page=2");

        var next = Parse(url.PageLink(3, Sid, 5));

        Assert.Equal(url with { Page = 3, SearchId = Sid, Sequence = 5, Facets = next.Facets }, next);
        Assert.Equal(["a&b=c d"], next.Facets["Content.Type"]);
    }

    [Fact]
    public void ToggleFacetLink_AddsRemovesAndResetsPage()
    {
        var url = Parse("?q=tea&page=4&f.Brand=x&f.Brand=y");

        var removed = Parse(url.ToggleFacetLink("Brand", "x", Sid, 2));
        var added = Parse(url.ToggleFacetLink("Type", "loose", Sid, 2));
        var cleared = Parse(Parse("?f.Brand=x").ToggleFacetLink("Brand", "x", Sid, 2));

        Assert.Equal(1, removed.Page);
        Assert.Equal(["y"], removed.Facets["Brand"]);
        Assert.Equal(["loose"], added.Facets["Type"]);
        Assert.Equal(2, added.Facets.Count);
        Assert.Empty(cleared.Facets);
        Assert.Equal((Sid, 2), (cleared.SearchId, cleared.Sequence));
        Assert.True(url.IsSelected("Brand", "y"));
        Assert.False(url.IsSelected("Brand", "z"));
    }

    [Fact]
    public void PageSearch_RunsOncePerIndexPerRequest_WithSubmitForNewText()
    {
        var (search, service) = PageSearch();
        var context = Context("?q=grinder");

        var first = search.For(context, Index);
        var second = search.For(context, Index);

        Assert.Same(first, second);
        var request = Assert.Single(service.Requests);
        Assert.Equal(("grinder", (Guid?)null, (int?)null, true, 10, true), (request.Query, request.SearchId, request.Sequence, request.IsSubmit, request.PageSize, request.TrackAnalytics));
        Assert.Equal((SuitePageSearchStatus.Ok, 1, 2), (first.Status, first.Sequence, first.NextSequence));
        Assert.Equal(["vip"], request.Contexts);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("same-origin", true)]
    [InlineData("none", false)]
    [InlineData("same-site", false)]
    [InlineData("cross-site", false)]
    public void PageSearch_ContinuesOnlySameOriginNavigation(string? fetchSite, bool continues)
    {
        var (search, service) = PageSearch();
        var context = Context($"?q=grinder&page=2&sid={Sid:N}&seq=3");
        if (fetchSite is not null)
        {
            context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        }

        var result = search.For(context, Index);

        var request = Assert.Single(service.Requests);
        Assert.Equal(continues ? (Sid, 3, false) : (null, null, true), (request.SearchId, request.Sequence, request.IsSubmit));
        Assert.Equal(continues ? 3 : 1, result.Sequence);
    }

    [Fact]
    public void PageSearch_EmptyUrl_SearchesNothing_AndLeavesCachingAlone()
    {
        var (search, service) = PageSearch();
        var context = Context(string.Empty);

        var result = search.For(context, Index);

        Assert.Equal(SuitePageSearchStatus.NotSearched, result.Status);
        Assert.Empty(service.Requests);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
        Assert.True(context.Features.Get<IOutputCacheFeature>()!.Context.AllowCacheStorage);
    }

    [Fact]
    public void PageSearch_SearchOnEmptyQuery_BrowsesWithoutSubmit()
    {
        var (search, service) = PageSearch(o => o.SearchOnEmptyQuery = true);

        search.For(Context(string.Empty), Index);

        Assert.False(Assert.Single(service.Requests).IsSubmit);
    }

    [Fact]
    public void PageSearch_AfterASearch_ResponseIsUncacheable()
    {
        var (search, _) = PageSearch();
        var context = Context("?q=tea");

        search.For(context, Index);

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.False(context.Features.Get<IOutputCacheFeature>()!.Context.AllowCacheStorage);
    }

    [Fact]
    public void PageSearch_Failures_RenderStates_AndUnknownIndexThrows()
    {
        var (invalid, _) = PageSearch(fail: new ArgumentException("bad facet"));
        var (broken, _) = PageSearch(fail: new IOException("disk"));
        var (aborted, _) = PageSearch(fail: new OperationCanceledException());
        var (search, service) = PageSearch();

        Assert.Equal(SuitePageSearchStatus.Invalid, invalid.For(Context("?q=tea"), Index).Status);
        Assert.Equal(SuitePageSearchStatus.Unavailable, broken.For(Context("?q=tea"), Index).Status);
        Assert.Throws<OperationCanceledException>(() => aborted.For(Context("?q=tea"), Index));
        Assert.Equal(SuitePageSearchStatus.Invalid, search.For(Context($"?q={new string('x', 300)}"), Index).Status);
        Assert.Empty(service.Requests);
        var error = Assert.Throws<InvalidOperationException>(() => search.For(Context("?q=tea"), "missing"));
        Assert.Contains("'missing'", error.Message);
    }

    [Fact]
    public void PageSearch_ContactGroupContextsCanBeTurnedOff()
    {
        var (search, service) = PageSearch(o => o.ContactGroupContexts = false);

        search.For(Context("?q=tea"), Index);

        Assert.Empty(Assert.Single(service.Requests).Contexts);
    }

    [Fact]
    public void PageSearch_InPreview_IsUntrackedWithoutContexts()
    {
        var (search, service) = PageSearch(preview: new SuitePreview(true, false));

        var result = search.For(Context("?q=tea"), Index);

        var request = Assert.Single(service.Requests);
        Assert.Equal((false, 0), (request.TrackAnalytics, request.Contexts.Count));
        Assert.Equal((true, false), (result.IsPreview, result.IsEditMode));
    }

    [Fact]
    public void PageSearch_UnreadablePreviewState_FailsClosed()
    {
        var options = new SuiteWidgetsOptions();
        var service = new RecordingSearch(null);
        var search = new SuitePageSearch(service, new OneIndex(), new StubGroups(1, ["vip"]), new SuiteEventsPath(), new FixedPreview(null), Options.Create(options), TimeProvider.System, NullLogger<SuitePageSearch>.Instance);

        var result = search.For(Context("?q=tea"), Index);

        Assert.False(Assert.Single(service.Requests).TrackAnalytics);
        Assert.True(result.IsPreview);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("ContentType = Content type", "ContentType=Content type")]
    [InlineData("  A=1 \r\nno equals\n=orphan\nB=\nC = x = y\nA = 2", "A=2|C=x = y")]
    public void ParseLabels_SkipsIncompleteLines_LastWins(string? text, string expected) =>
        Assert.Equal(expected, string.Join("|", Widgets.Components.LuceneSuiteFacetsViewComponent.ParseLabels(text).Select(l => $"{l.Key}={l.Value}")));

    [Fact]
    public void WidgetsAssembly_IsDiscoverable_AndRegistersThreeUncachedWidgets()
    {
        var assembly = typeof(SuitePageSearch).Assembly;
        // The attribute keeps the types internally, so read the constructor arguments as written.
        var widgets = assembly.GetCustomAttributesData()
            .Where(a => a.AttributeType == typeof(Kentico.PageBuilder.Web.Mvc.RegisterWidgetAttribute))
            .Select(a => (
                (string)a.ConstructorArguments[0].Value!,
                ((Type)a.ConstructorArguments[1].Value!).Name,
                ((Type)a.ConstructorArguments[3].Value!).Name,
                (bool)a.NamedArguments.Single(n => n.MemberName == "AllowCache").TypedValue.Value!))
            .OrderBy(w => w.Item1, StringComparer.Ordinal)
            .ToList();

        Assert.NotNull(assembly.GetCustomAttributes(typeof(CMS.AssemblyDiscoverableAttribute), false).SingleOrDefault());
        Assert.Equal(
            [
                ("XperienceCommunity.LuceneSuite.SearchBox", "LuceneSuiteSearchBoxWidget", "LuceneSuiteSearchBoxWidgetProperties", false),
                ("XperienceCommunity.LuceneSuite.SearchFilters", "LuceneSuiteFiltersWidget", "LuceneSuiteFiltersWidgetProperties", false),
                ("XperienceCommunity.LuceneSuite.SearchResults", "LuceneSuiteResultsWidget", "LuceneSuiteResultsWidgetProperties", false),
            ],
            widgets);
    }

    [Fact]
    public void ContactGroups_FilterLimitAndFailSafe()
    {
        string[] loaded = ["", "  ", new string('g', 101), .. Enumerable.Range(0, 60).Select(i => $"group{i:00}")];

        Assert.Equal(SuiteContactGroupContexts_Max(), new StubGroups(7, loaded).Get().Count);
        Assert.Equal("group00", new StubGroups(7, loaded).Get().First());
        Assert.Empty(new StubGroups(null, loaded).Get());
        Assert.Empty(new StubGroups(7, null).Get());
    }

    private static int SuiteContactGroupContexts_Max() => SuiteContactGroupContexts.MaxGroups;

    private static SuiteSearchUrl Parse(string queryString) => SuiteSearchUrl.Parse(new QueryCollection(QueryHelpers.ParseQuery(queryString)), 10);

    private static DefaultHttpContext Context(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        context.Features.Set<IOutputCacheFeature>(new OutputCacheFeature(context));
        return context;
    }

    private static (SuitePageSearch Search, RecordingSearch Service) PageSearch(Action<SuiteWidgetsOptions>? configure = null, Exception? fail = null, SuitePreview preview = default)
    {
        var options = new SuiteWidgetsOptions();
        configure?.Invoke(options);
        var service = new RecordingSearch(fail);
        var search = new SuitePageSearch(service, new OneIndex(), new StubGroups(1, ["vip"]), new SuiteEventsPath(), new FixedPreview(preview), Options.Create(options), TimeProvider.System, NullLogger<SuitePageSearch>.Instance);
        return (search, service);
    }

    /// <summary>A fixed preview state; null throws (unreadable state).</summary>
    internal sealed class FixedPreview(SuitePreview? preview) : ISuitePreviewState
    {
        public SuitePreview Get(HttpContext context) => preview ?? throw new InvalidOperationException("no Kentico context");
    }

    private sealed class OutputCacheFeature(HttpContext httpContext) : IOutputCacheFeature
    {
        public OutputCacheContext Context { get; } = new() { HttpContext = httpContext, AllowCacheStorage = true };
    }

    private sealed class RecordingSearch(Exception? fail) : ISuiteSearchService
    {
        public List<SuiteSearchRequest> Requests { get; } = [];

        public SuiteSearchResponse Search(SuiteSearchRequest request)
        {
            if (fail is not null)
            {
                throw fail;
            }

            Requests.Add(request);
            return new SuiteSearchResponse([], 0, [], TimeSpan.Zero) { SearchId = request.SearchId ?? Guid.NewGuid() };
        }
    }

    internal sealed class OneIndex : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(string indexName) =>
            // LuceneIndex has only an internal constructor; existence is all the page search reads.
            indexName == Index ? (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex)) : null;

        public LuceneIndex? GetIndex(int identifier) => throw new NotSupportedException();

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }

    /// <summary>Real filtering and caching path; the contact and the Kentico query are stubbed (null groups = lookup failure).</summary>
    internal sealed class StubGroups(int? contactId, IEnumerable<string>? groups)
        : SuiteContactGroupContexts(null!, null!, null!, new PassThroughCache(), TimeProvider.System, NullLogger<SuiteContactGroupContexts>.Instance)
    {
        protected override int? CurrentContactId() => contactId;

        protected override IEnumerable<string> LoadGroups(int contactId) => groups ?? throw new InvalidOperationException("db down");
    }

    internal sealed class PassThroughCache : IProgressiveCache
    {
        public TData Load<TData>(Func<CacheSettings, TData> loadDataFunc, CacheSettings settings)
        {
            settings.Cached = false;
            return loadDataFunc(settings);
        }

        public Task<TData> LoadAsync<TData>(Func<CacheSettings, Task<TData>> loadDataFuncAsync, CacheSettings settings) => loadDataFuncAsync(settings);

        public Task<TData> LoadAsync<TData>(Func<CacheSettings, CancellationToken, Task<TData>> loadDataFuncAsync, CacheSettings settings, CancellationToken cancellationToken) =>
            loadDataFuncAsync(settings, cancellationToken);
    }
}
