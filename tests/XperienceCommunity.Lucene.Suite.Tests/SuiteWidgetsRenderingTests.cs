using System.Net;

using Kentico.Content.Web.Mvc;
using Kentico.PageBuilder.Web.Mvc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets;
using XperienceCommunity.Lucene.Suite.Widgets.Components;
using XperienceCommunity.Lucene.Suite.Widgets.Events;
using XperienceCommunity.Lucene.Suite.Widgets.PageBuilder;
using XperienceCommunity.Lucene.Suite.Widgets.Search;
using XperienceCommunity.Lucene.Suite.Widgets.Suggestions;

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
        Assert.Contains($"<ol class=\"lucene-suite-results__list\" start=\"11\" data-lucene-suite-results data-lucene-suite-events=\"/lucenesuite/events\" data-lucene-suite-search=\"{SearchId}\" data-lucene-suite-sequence=\"1\" data-lucene-suite-query=\"grinder\">", html);
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
    public async Task Results_ListCarriesTheScrubbedQuery_Encoded()
    {
        string html = await Render("/test/results?q=" + Uri.EscapeDataString("  Mail  Jane@Example.com \"<x>\" "));

        Assert.Contains("data-lucene-suite-query=\"mail {email} &quot;&lt;x&gt;&quot;\">", html);
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
    public async Task Preview_ResultsCarryNoTracking()
    {
        string html = await Render("/test/results?q=grinder", preview: "preview");

        Assert.Contains("href=\"/coffee/grinder?x=1#frag\">", html);
        Assert.DoesNotContain("sr=", html);
        Assert.DoesNotContain("data-lucene-suite-item", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("data-lucene-suite-events=\"\"", html);
    }

    [Fact]
    public async Task EditMode_ShowsPlaceholdersAndAnInertSearchBox()
    {
        Assert.Contains("Search results appear here when a visitor searches.", await Render("/test/results", preview: "edit"));
        Assert.Contains("Filters appear here when a visitor searches.", await Render("/test/facets", preview: "edit"));
        Assert.Contains("<fieldset class=\"lucene-suite-search-box__fields\" disabled=\"disabled\">", await Render("/test/box", preview: "edit"));
        Assert.Contains("<fieldset class=\"lucene-suite-search-box__fields\" disabled=\"disabled\">", await Render("/test/box", preview: "preview"));
        Assert.Contains("<fieldset class=\"lucene-suite-search-box__fields\">", await Render("/test/box"));
        Assert.Equal(string.Empty, (await Render("/test/results")).Trim());
    }

    [Fact]
    public async Task Facets_UseDimensionLabels_LinksKeepTheDimension()
    {
        string html = await Render("/test/facets?q=grinder&labels=" + Uri.EscapeDataString("ContentType = Content type\nBrand=Make"));

        Assert.Contains("<h2 class=\"lucene-suite-facets__title\">Content type</h2>", html);
        Assert.Contains("f.ContentType=Article", html);
    }

    [Fact]
    public async Task Widgets_RenderTheSharedViews()
    {
        string results = await Render("/test/widget/results?q=grinder");
        string filters = await Render("/test/widget/filters?q=grinder");
        string box = await Render("/test/widget/box?q=grinder");

        Assert.Contains("35 results for “grinder”", results);
        Assert.Contains("<p class=\"lucene-suite-results__summary\">Burr &amp; blade</p>", results);
        Assert.Contains("<h2 class=\"lucene-suite-facets__title\">Content type</h2>", filters);
        Assert.Contains("action=\"/search-results\"", box);
    }

    [Fact]
    public async Task Widgets_WithoutAUsableIndex_TellEditorsAndShowVisitorsNothing()
    {
        Assert.Equal(string.Empty, await Render("/test/widget/results?index=missing"));
        Assert.Equal(string.Empty, await Render("/test/widget/filters?index="));
        Assert.Contains("choose an existing index", await Render("/test/widget/results?index=missing", preview: "edit"));
    }

    [Fact]
    public async Task SearchBoxWidget_WithAnUnresolvablePage_SubmitsToTheCurrentPage()
    {
        Assert.Contains("action=\"/test/widget/box\"", await Render("/test/widget/box?page=broken"));
        Assert.Contains("action=\"/test/widget/box\"", await Render("/test/widget/box?page=none"));
    }

    [Fact]
    public async Task ResultsAndFacets_OnOnePage_ShareOneSearch()
    {
        var service = new CannedSearch();

        string html = await Render("/test/both?q=grinder", service: service);

        Assert.Contains("lucene-suite-facets", html);
        Assert.Equal(1, service.Calls);
    }

    // Captured before suggestions existed (09-02): with suggestions off, the search box must render exactly this.
    private const string GoldenBox =
        "<form class=\"lucene-suite-search-box\" role=\"search\" method=\"get\" action=\"/test/box\">\r\n" +
        "    <fieldset class=\"lucene-suite-search-box__fields\">\r\n" +
        "        <label class=\"lucene-suite-search-box__label\">\r\n" +
        "            <span class=\"lucene-suite-search-box__label-text\">Search</span>\r\n" +
        "            <input class=\"lucene-suite-search-box__input\" type=\"search\" name=\"q\" value=\"&lt;b&gt;tea&lt;/b&gt;\" maxlength=\"256\" autocomplete=\"off\" />\r\n" +
        "        </label>\r\n" +
        "        <button class=\"lucene-suite-search-box__submit\" type=\"submit\">Search</button>\r\n" +
        "    </fieldset>\r\n" +
        "</form>\r\n";

    private const string GoldenPreviewBox =
        "<form class=\"lucene-suite-search-box\" role=\"search\" method=\"get\" action=\"/test/box\">\r\n" +
        "    <fieldset class=\"lucene-suite-search-box__fields\" disabled=\"disabled\">\r\n" +
        "        <label class=\"lucene-suite-search-box__label\">\r\n" +
        "            <span class=\"lucene-suite-search-box__label-text\">Search</span>\r\n" +
        "            <input class=\"lucene-suite-search-box__input\" type=\"search\" name=\"q\" value=\"\" maxlength=\"256\" autocomplete=\"off\" />\r\n" +
        "        </label>\r\n" +
        "        <button class=\"lucene-suite-search-box__submit\" type=\"submit\">Search</button>\r\n" +
        "    </fieldset>\r\n" +
        "</form>\r\n";

    private static readonly string TeaQuery = "?q=" + Uri.EscapeDataString("<b>tea</b>");

    [Theory]
    [InlineData(null, true)] // no suggestion index configured
    [InlineData("Other", true)] // index not in SuggestionIndexes
    [InlineData("Shop", false)] // endpoint not mapped
    public async Task SearchBox_WithSuggestionsOff_IsByteIdenticalToTheGolden(string? index, bool mapSuggest)
    {
        string[] indexes = index is null ? [] : ["Shop", "Second"];
        string url = "/test/box" + TeaQuery + (index is null ? string.Empty : "&index=" + index);

        Assert.Equal(GoldenBox, await Render(url, suggestionIndexes: indexes, mapSuggest: mapSuggest));
    }

    [Fact]
    public async Task SearchBox_InPreview_HasNoSuggestions_AndIsByteIdenticalToTheGolden()
    {
        Assert.Equal(GoldenPreviewBox, await Render("/test/box?index=Shop", preview: "preview", suggestionIndexes: ["Shop"]));
        Assert.DoesNotContain("data-lucene-suite", await Render("/test/box", preview: "edit", suggestionIndexes: ["Shop"]));
    }

    [Fact]
    public async Task SearchBox_WithSeveralIndexesAndNoneChosen_HasNoSuggestions()
    {
        Assert.Equal(GoldenBox, await Render("/test/box" + TeaQuery, suggestionIndexes: ["Shop", "Second"]));
    }

    [Fact]
    public async Task SearchBox_WithSuggestions_RendersTheComboboxHooks_AndAssetsOnce()
    {
        string html = await Render("/test/box?index=shop", suggestionIndexes: ["Second", "Shop"]);

        Assert.Contains("<form class=\"lucene-suite-search-box\" role=\"search\" method=\"get\" action=\"/test/box\" data-lucene-suite-suggest=\"/lucenesuite/suggest\" data-lucene-suite-index=\"Shop\" data-lucene-suite-min-length=\"2\">", html);
        Assert.Contains("<div class=\"lucene-suite-suggest\">", html);
        Assert.Contains("<input class=\"lucene-suite-search-box__input\" id=\"lucene-suite-q-1\" type=\"search\"", html);
        Assert.Contains("<ul class=\"lucene-suite-suggest__list\" id=\"lucene-suite-q-1-listbox\" role=\"listbox\" aria-label=\"Suggestions\" hidden></ul>", html);
        Assert.Contains("<div class=\"lucene-suite-suggest__status\" id=\"lucene-suite-q-1-status\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\"></div>", html);
        Assert.Contains("<link rel=\"stylesheet\" href=\"/_content/XperienceCommunity.Lucene.Suite.Widgets/lucene-suite-suggest.css", html);
        Assert.Contains("<script type=\"module\" src=\"/_content/XperienceCommunity.Lucene.Suite.Widgets/lucene-suite-suggest.js", html);
        Assert.DoesNotContain("style=", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Equal(html.Split("<div").Length - 1, html.Split("</div>").Length - 1);
    }

    [Fact]
    public async Task SearchBox_SingleSuggestionIndex_IsTheFallback_AndStylesCanBeOff()
    {
        string html = await Render("/test/box", suggestionIndexes: ["Shop"], suggestionStyles: false);

        Assert.Contains("data-lucene-suite-index=\"Shop\"", html);
        Assert.Contains("lucene-suite-suggest.js", html);
        Assert.DoesNotContain("lucene-suite-suggest.css", html);
    }

    [Fact]
    public async Task SearchBox_TwoOnAPage_GetUniqueIds_AndTheAssetsOnce()
    {
        string html = await Render("/test/two-boxes", suggestionIndexes: ["Shop"]);

        Assert.Contains("id=\"lucene-suite-q-1\"", html);
        Assert.Contains("id=\"lucene-suite-q-1-listbox\"", html);
        Assert.Contains("id=\"lucene-suite-q-2\"", html);
        Assert.Contains("id=\"lucene-suite-q-2-listbox\"", html);
        Assert.Contains("id=\"lucene-suite-q-2-status\"", html);
        Assert.Equal(2, html.Split("data-lucene-suite-suggest=").Length - 1);
        Assert.Equal(1, html.Split("lucene-suite-suggest.js").Length - 1);
        Assert.Equal(1, html.Split("lucene-suite-suggest.css").Length - 1);
    }

    [Fact]
    public async Task SearchBoxWidget_PassesItsSuggestionsIndex()
    {
        Assert.Contains("data-lucene-suite-index=\"Second\"", await Render("/test/widget/box?index=Second", suggestionIndexes: ["Shop", "Second"]));
        Assert.DoesNotContain("data-lucene-suite", await Render("/test/widget/box?index=Missing", suggestionIndexes: ["Shop", "Second"]));
    }

    private static async Task<string> Render(string url, bool mapEvents = true, CannedSearch? service = null, string? preview = null, string[]? suggestionIndexes = null, bool mapSuggest = true, bool suggestionStyles = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISuiteSearchService>(service ?? new CannedSearch());
        builder.Services.AddSingleton<ISearchInteractionLogger>(new NoLogger());
        builder.Services.AddSingleton<Kentico.Xperience.Lucene.Core.Indexing.ILuceneIndexManager>(new SuiteSearchUrlTests.OneIndex());
        builder.Services.AddSingleton<SuiteContactGroupContexts>(new SuiteSearchUrlTests.StubGroups(null, []));
        builder.Services.AddSingleton<ISuitePreviewState>(new HeaderPreview());
        builder.Services.AddSingleton<CMS.Websites.IWebPageUrlRetriever>(new OnePageUrl());
        builder.Services.AddSingleton<XperienceCommunity.Lucene.Suite.Suggestions.ISuiteSuggestions>(new NoSuggestions());
        builder.Services.AddLuceneSuiteWidgets(o =>
        {
            o.SuggestionIndexes = suggestionIndexes ?? [];
            o.SuggestionStyles = suggestionStyles;
        });
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(LuceneSuiteResultsViewComponent).Assembly)
            .AddApplicationPart(typeof(SuiteWidgetsRenderingTests).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        if (mapEvents)
        {
            app.MapLuceneSuiteEvents();
        }

        if (mapSuggest)
        {
            app.MapLuceneSuiteSuggestions();
        }

        await app.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (preview is not null)
        {
            request.Headers.Add(HeaderPreview.Header, preview);
        }

        var response = await app.GetTestClient().SendAsync(request);
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

    /// <summary>Preview state from a test header: "preview", "edit", or none.</summary>
    private sealed class HeaderPreview : ISuitePreviewState
    {
        public const string Header = "X-Test-Preview";

        public SuitePreview Get(Microsoft.AspNetCore.Http.HttpContext context) => context.Request.Headers[Header].ToString() switch
        {
            "edit" => new SuitePreview(true, true),
            "preview" => new SuitePreview(true, false),
            _ => default,
        };
    }

    /// <summary>Resolves the one known results page; any other page throws like a deleted page.</summary>
    private sealed class OnePageUrl : CMS.Websites.IWebPageUrlRetriever
    {
        public static readonly Guid Page = Guid.Parse("5a5a5a5a-1111-4222-8333-944444444444");

        public Task<CMS.Websites.WebPageUrl> Retrieve(Guid webPageItemGuid, string languageName, bool forPreview = false, CancellationToken cancellationToken = default) =>
            webPageItemGuid == Page ? Task.FromResult(new CMS.Websites.WebPageUrl("~/search-results", "http://localhost/search-results")) : throw new InvalidOperationException("page not found");

        public Task<CMS.Websites.WebPageUrl> Retrieve(CMS.Websites.IWebPageFieldsSource webPageFieldsSource, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CMS.Websites.WebPageUrl> Retrieve(CMS.Websites.IWebPageFieldsSource webPageFieldsSource, string languageName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CMS.Websites.WebPageUrl> Retrieve(string webPageUrlPath, string webPageTreePath, int websiteChannelId, string languageName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CMS.Websites.WebPageUrl> Retrieve(int webPageItemId, string languageName, bool forPreview = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IDictionary<Guid, CMS.Websites.WebPageUrl>> Retrieve(IReadOnlyCollection<Guid> webPageItemGuids, string websiteChannelName, string languageName, bool forPreview = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CMS.Websites.WebPageUrl> Retrieve(string webPageTreePath, string websiteChannelName, string languageName, bool forPreview = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoSuggestions : XperienceCommunity.Lucene.Suite.Suggestions.ISuiteSuggestions
    {
        public Task<IReadOnlyList<string>> SuggestAsync(string indexName, string? input, int? maxResults = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoLogger : ISearchInteractionLogger
    {
        public void LogClick(Guid searchId, int sequence, Guid itemGuid, int position, Guid? eventId = null, string? queryText = null, string? resultTitle = null)
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
    public IActionResult Box(string? index) => ViewComponent(typeof(LuceneSuiteSearchBoxViewComponent), new { indexName = index });

    [HttpGet("/test/two-boxes")]
    public async Task TwoBoxes()
    {
        // Buffered so the second render can still set headers: two boxes in one request.
        var body = Response.Body;
        using var buffer = new MemoryStream();
        Response.Body = buffer;
        await ViewComponent(typeof(LuceneSuiteSearchBoxViewComponent)).ExecuteResultAsync(ControllerContext);
        await ViewComponent(typeof(LuceneSuiteSearchBoxViewComponent)).ExecuteResultAsync(ControllerContext);
        Response.Body = body;
        buffer.Position = 0;
        await buffer.CopyToAsync(body);
    }

    [HttpGet("/test/results")]
    public IActionResult Results() => ViewComponent(typeof(LuceneSuiteResultsViewComponent), new { indexName = SuiteSearchUrlTests.Index, summaryField = "Summary" });

    [HttpGet("/test/facets")]
    public IActionResult Facets(string? labels) => ViewComponent(typeof(LuceneSuiteFacetsViewComponent), new { indexName = SuiteSearchUrlTests.Index, dimensionLabels = labels });

    [HttpGet("/test/widget/results")]
    public IActionResult ResultsWidget(string? index = SuiteSearchUrlTests.Index) => ViewComponent(typeof(LuceneSuiteResultsWidget), new
    {
        widget = ComponentViewModel<LuceneSuiteResultsWidgetProperties>.Create(new RoutedWebPage { LanguageName = "en" }, new LuceneSuiteResultsWidgetProperties { IndexName = index ?? string.Empty, SummaryField = "Summary" }),
    });

    [HttpGet("/test/widget/filters")]
    public IActionResult FiltersWidget(string? index = SuiteSearchUrlTests.Index) => ViewComponent(typeof(LuceneSuiteFiltersWidget), new
    {
        widget = ComponentViewModel<LuceneSuiteFiltersWidgetProperties>.Create(new RoutedWebPage { LanguageName = "en" }, new LuceneSuiteFiltersWidgetProperties { IndexName = index ?? string.Empty, DimensionLabels = "ContentType = Content type" }),
    });

    [HttpGet("/test/widget/box")]
    public IActionResult BoxWidget(string? page, string? index) => ViewComponent(typeof(LuceneSuiteSearchBoxWidget), new
    {
        widget = ComponentViewModel<LuceneSuiteSearchBoxWidgetProperties>.Create(new RoutedWebPage { LanguageName = "en" }, new LuceneSuiteSearchBoxWidgetProperties
        {
            SuggestionsIndex = index,
            ResultsPage = page switch
            {
                "none" => [],
                "broken" => [new CMS.Websites.WebPageRelatedItem { WebPageGuid = Guid.NewGuid() }],
                _ => [new CMS.Websites.WebPageRelatedItem { WebPageGuid = Guid.Parse("5a5a5a5a-1111-4222-8333-944444444444") }],
            },
        }),
    });

    [HttpGet("/test/both")]
    public IActionResult Both([FromServices] SuitePageSearch search)
    {
        // Same request, two components: the second must reuse the first's search.
        search.For(HttpContext, SuiteSearchUrlTests.Index);
        return ViewComponent(typeof(LuceneSuiteFacetsViewComponent), new { indexName = SuiteSearchUrlTests.Index });
    }
}
