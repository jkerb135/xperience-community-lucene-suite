using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets;
using XperienceCommunity.Lucene.Suite.Widgets.Components;
using XperienceCommunity.Lucene.Suite.Widgets.Events;
using XperienceCommunity.Lucene.Suite.Widgets.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Default views of the search components, rendered by MVC on an in-process host.</summary>
public sealed class SuiteWidgetsRenderingTests
{
    private static readonly Guid SearchId = Guid.Parse("8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b");
    private static readonly Guid Item = Guid.Parse("1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b");

    [Fact]
    public async Task SearchBox_IsALabelledGetFormWithoutContinuation()
    {
        string html = await Render("/test/box?q=" + Uri.EscapeDataString("<b>tea</b>") + $"&sid={SearchId:N}&seq=3");

        Assert.Contains("role=\"search\" method=\"get\" action=\"/test/box\"", html);
        Assert.Contains("<label class=\"lucene-suite-search-box__label\">", html);
        Assert.Contains("name=\"q\" value=\"&lt;b&gt;tea&lt;/b&gt;\"", html);
        Assert.DoesNotContain("sid", html);
    }

    [Fact]
    public async Task Results_RenderStatusHitsAndSafeTrackedLinks()
    {
        string html = await Render("/test/results?q=grinder&page=2");

        Assert.Contains("35 results for “grinder”", html);
        Assert.Contains($"<ol class=\"lucene-suite-results__list\" start=\"11\" data-lucene-suite-results data-lucene-suite-events=\"/lucenesuite/events\" data-lucene-suite-search=\"{SearchId}\" data-lucene-suite-sequence=\"1\">", html);
        Assert.Contains($"href=\"/coffee/grinder?x=1&amp;sr={SearchId:N}.1.11#frag\" data-lucene-suite-item=\"{Item}\" data-lucene-suite-position=\"11\">&lt;script&gt;alert(1)&lt;/script&gt; &amp; co</a>", html);
        Assert.Contains("<p class=\"lucene-suite-results__summary\">Burr &amp; blade</p>", html);
        Assert.Contains($"href=\"http://localhost/same?sr={SearchId:N}.1.16\" data-lucene-suite-item=", html);
        Assert.Contains("<a class=\"lucene-suite-results__title\" href=\"https://external.example/p\">External</a>", html);
        foreach (string unlinked in new[] { "JS", "Data", "Proto", "Backslash", "No link" })
        {
            Assert.Contains($"<span class=\"lucene-suite-results__title\">{unlinked}</span>", html);
        }

        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("evil.example", html);
        Assert.Contains("<script type=\"module\" src=\"/_content/XperienceCommunity.Lucene.Suite.Widgets/lucene-suite-results.js", html);
    }

    [Fact]
    public async Task Results_PaginationContinuesTheSearch()
    {
        string html = await Render("/test/results?q=grinder&page=2");

        Assert.Contains($"href=\"?q=grinder&amp;sid={SearchId:N}&amp;seq=2\" rel=\"prev nofollow\">Previous</a>", html);
        Assert.Contains($"href=\"?q=grinder&amp;page=2&amp;sid={SearchId:N}&amp;seq=2\" rel=\"nofollow\" aria-current=\"page\">2</a>", html);
        Assert.Contains($"href=\"?q=grinder&amp;page=4&amp;sid={SearchId:N}&amp;seq=2\" rel=\"nofollow\">4</a>", html);
        Assert.Contains($"href=\"?q=grinder&amp;page=3&amp;sid={SearchId:N}&amp;seq=2\" rel=\"next nofollow\">Next</a>", html);
        Assert.DoesNotContain(">5</a>", html);
    }

    [Fact]
    public async Task Results_WithoutEventsEndpoint_HaveNoBeaconScript()
    {
        string html = await Render("/test/results?q=grinder", mapEvents: false);

        Assert.DoesNotContain("<script", html);
        Assert.Contains("data-lucene-suite-results", html);
        Assert.Contains("data-lucene-suite-events=\"\"", html);
    }

    [Theory]
    [InlineData("?q=", "")]
    [InlineData("?q=bad", "This search could not be run.")]
    [InlineData("?q=down", "Search is unavailable right now.")]
    public async Task Results_EmptyInvalidAndUnavailableStates(string query, string expected)
    {
        string html = await Render("/test/results" + query);

        Assert.Equal(expected.Length == 0, html.Trim().Length == 0);
        Assert.Contains(expected, html);
        Assert.DoesNotContain("<ol", html);
    }

    [Fact]
    public async Task Facets_ToggleLinks_KeepSelectedValuesRemovable()
    {
        string html = await Render("/test/facets?q=grinder&page=2&f.ContentType=Gone&f.Brand=Acme");

        Assert.Contains("<nav class=\"lucene-suite-facets\" aria-label=\"Filters\">", html);
        Assert.Contains("<h2 class=\"lucene-suite-facets__title\">ContentType</h2>", html);
        Assert.Contains($"href=\"?q=grinder&amp;f.ContentType=Gone&amp;f.ContentType=Article&amp;f.Brand=Acme&amp;sid={SearchId:N}&amp;seq=2\" rel=\"nofollow\">", html);
        Assert.Contains("<span class=\"lucene-suite-facets__label\">Product &amp; Co</span>", html);
        Assert.Contains($"lucene-suite-facets__link--selected\" href=\"?q=grinder&amp;f.Brand=Acme&amp;sid={SearchId:N}&amp;seq=2\" rel=\"nofollow\">", html);
        Assert.Contains("<span class=\"lucene-suite-facets__label\">Gone</span>\n                                <span class=\"lucene-suite-facets__count\">(0)</span>", html.Replace("\r\n", "\n"));
        Assert.Contains("<h2 class=\"lucene-suite-facets__title\">Brand</h2>", html);
        Assert.Equal(2, html.Split("<span class=\"lucene-suite-facets__state\">selected</span>").Length - 1);
    }

    [Fact]
    public async Task Facets_OfARejectedSearch_StillListTheFilters()
    {
        string html = await Render("/test/facets?q=bad&f.Brand=Acme");

        Assert.Contains("<span class=\"lucene-suite-facets__label\">Acme</span>", html);
        Assert.Contains("href=\"?q=bad\" rel=\"nofollow\"", html);
    }

    [Fact]
    public async Task ResultsAndFacets_OnOnePage_ShareOneSearch()
    {
        var service = new CannedSearch();

        string html = await Render("/test/both?q=grinder", service: service);

        Assert.Contains("lucene-suite-facets", html);
        Assert.Equal(1, service.Calls);
    }

    private static async Task<string> Render(string url, bool mapEvents = true, CannedSearch? service = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISuiteSearchService>(service ?? new CannedSearch());
        builder.Services.AddSingleton<ISearchInteractionLogger>(new NoLogger());
        builder.Services.AddSingleton<Kentico.Xperience.Lucene.Core.Indexing.ILuceneIndexManager>(new SuiteSearchUrlTests.OneIndex());
        builder.Services.AddSingleton<SuiteContactGroupContexts>(new SuiteSearchUrlTests.StubGroups(null, []));
        builder.Services.AddLuceneSuiteWidgets();
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(LuceneSuiteResultsViewComponent).Assembly)
            .AddApplicationPart(typeof(SuiteWidgetsRenderingTests).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        if (mapEvents)
        {
            app.MapLuceneSuiteEvents();
        }

        await app.StartAsync();
        var response = await app.GetTestClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private sealed class CannedSearch : ISuiteSearchService
    {
        public int Calls { get; private set; }

        public SuiteSearchResponse Search(SuiteSearchRequest request)
        {
            Calls++;
            return request.Query switch
            {
                "bad" => throw new ArgumentException("bad facet"),
                "down" => throw new IOException("index locked"),
                _ => new SuiteSearchResponse(
                    [
                        Hit(Item, "<script>alert(1)</script> & co", "~/coffee/grinder?x=1#frag", "Burr & blade"),
                        Hit(Guid.NewGuid(), "JS", "javascript:alert(1)"),
                        Hit(Guid.NewGuid(), "Data", "data:text/html,x"),
                        Hit(Guid.NewGuid(), "Proto", "//evil.example/x"),
                        Hit(Guid.NewGuid(), "External", "https://external.example/p"),
                        Hit(Guid.NewGuid(), "Same host", "http://localhost/same"),
                        Hit(Guid.NewGuid(), "No link", null),
                        Hit(Guid.NewGuid(), "Backslash", "/\\evil.example"),
                    ],
                    35,
                    [new SuiteFacet("ContentType", [new SuiteFacetValue("Article", 3), new SuiteFacetValue("Product & Co", 5)])],
                    TimeSpan.Zero) { SearchId = SearchId },
            };
        }

        private static SuiteSearchHit Hit(Guid item, string title, string? url, string? summary = null)
        {
            var fields = new Dictionary<string, IReadOnlyList<string>> { ["Title"] = [title] };
            if (url is not null)
            {
                fields["Url"] = [url];
            }

            if (summary is not null)
            {
                fields["Summary"] = [summary];
            }

            return new SuiteSearchHit(item, 1, fields);
        }
    }

    private sealed class NoLogger : ISearchInteractionLogger
    {
        public void LogClick(Guid searchId, int sequence, Guid itemGuid, int position, Guid? eventId = null)
        {
        }

        public void LogConversion(Guid searchId, int sequence, Guid itemGuid, int? position = null, Guid? eventId = null)
        {
        }
    }
}

/// <summary>Renders the search components for <see cref="SuiteWidgetsRenderingTests"/>.</summary>
public sealed class WidgetsTestController : Controller
{
    [HttpGet("/test/box")]
    public IActionResult Box() => ViewComponent(typeof(LuceneSuiteSearchBoxViewComponent));

    [HttpGet("/test/results")]
    public IActionResult Results() => ViewComponent(typeof(LuceneSuiteResultsViewComponent), new { indexName = SuiteSearchUrlTests.Index, summaryField = "Summary" });

    [HttpGet("/test/facets")]
    public IActionResult Facets() => ViewComponent(typeof(LuceneSuiteFacetsViewComponent), new { indexName = SuiteSearchUrlTests.Index });

    [HttpGet("/test/both")]
    public IActionResult Both([FromServices] SuitePageSearch search)
    {
        // Same request, two components: the second must reuse the first's search.
        search.For(HttpContext, SuiteSearchUrlTests.Index);
        return ViewComponent(typeof(LuceneSuiteFacetsViewComponent), new { indexName = SuiteSearchUrlTests.Index });
    }
}
