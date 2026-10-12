---
title: Samples
nav_order: 45
---

# Samples

Complete walkthroughs to copy into a site. They build on each other: (a) is the setup the others assume. All of them use an index named `SiteSearch` with the stored field `Title`. Replace both with your own.

The C# files compile against the Suite (.NET 10, `ImplicitUsings` on, as in the `Microsoft.NET.Sdk.Web` template).

## (a) Minimal setup

`Program.cs`:

```csharp
using Kentico.Content.Web.Mvc.Routing;
using Kentico.PageBuilder.Web.Mvc;
using Kentico.Web.Mvc;

using MySite.Search;

using XperienceCommunity.Lucene.Suite;
using XperienceCommunity.Lucene.Suite.Widgets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKentico(features =>
{
    features.UsePageBuilder();
    features.UseWebPageRouting();
});
builder.Services.AddControllersWithViews();

// 1. Kentico Lucene: your indexing strategy.
builder.Services.AddKenticoLucene(lucene => lucene.RegisterStrategy<SiteSearchIndexingStrategy>(SiteSearchIndexingStrategy.StrategyName), builder.Configuration);

// 2. Lucene Suite core: which fields of the index the query text is matched against, and their boosts.
builder.Services.AddLuceneSuite(options => options.SearchFields["SiteSearch"] = new()
{
    ["Title"] = 3f,
    ["Body"] = 1f,
});

// 3. Widgets: components, Page Builder widgets, events and suggest endpoints.
builder.Services.AddLuceneSuiteWidgets(options => options.SuggestionIndexes = ["SiteSearch"]);

var app = builder.Build();

app.InitKentico();
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseKentico();
app.UseAuthorization();

app.Kentico().MapRoutes();
app.MapLuceneSuiteEvents();       // POST /lucenesuite/events, GET /lucenesuite/events/settings
app.MapLuceneSuiteSuggestions();  // GET /lucenesuite/suggest?index=SiteSearch&q=...
app.MapControllers();

app.Run();
```

`Search/SiteSearchIndexingStrategy.cs`, a deliberately small Kentico Lucene strategy (page name as title, content type as facet):

```csharp
using Kentico.Xperience.Lucene.Core.Indexing;

using Lucene.Net.Documents;
using Lucene.Net.Facet;

namespace MySite.Search;

public sealed class SiteSearchIndexingStrategy : DefaultLuceneIndexingStrategy
{
    public const string StrategyName = "SiteSearch";

    public override FacetsConfig FacetsConfigFactory() => new();

    public override Task<Document?> MapToLuceneDocumentOrNull(IIndexEventItemModel item)
    {
        if (item is not IndexEventWebPageItemModel page)
        {
            return Task.FromResult<Document?>(null);
        }

        return Task.FromResult<Document?>(new Document
        {
            new TextField("Title", page.Name, Field.Store.YES),
            new TextField("Body", page.Name, Field.Store.NO),
            new FacetField("ContentType", page.ContentTypeName),
        });
    }
}
```

Then, in the administration:

1. In the Kentico Lucene administration application, create the index `SiteSearch` with the strategy `SiteSearch`, and rebuild it.
2. **Site search → Indexes → SiteSearch → Query tester**: type a query to check the results.

To put search on a page, add the three widgets or view components; see [Widgets](widgets).

## (b) Call `ISuiteSearchService` from your own code

`ISuiteSearchService.Search(SuiteSearchRequest)` is synchronous and returns a `SuiteSearchResponse`. It records the search in analytics unless `TrackAnalytics = false`.

### A JSON endpoint

`Search/SiteSearchApi.cs`; add `app.MapSiteSearchApi();` to `Program.cs`:

```csharp
using Kentico.Xperience.Lucene.Core;

using XperienceCommunity.Lucene.Suite.Search;

namespace MySite.Search;

public static class SiteSearchApi
{
    private const string IndexName = "SiteSearch";
    private const int PageSize = 10;

    /// <summary>GET /api/search?q=&amp;page=&amp;searchId=&amp;sequence=&amp;submit=&amp;f.ContentType=</summary>
    public static IEndpointRouteBuilder MapSiteSearchApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/search", (HttpRequest http, ISuiteSearchService search,
            string? q, int? page, Guid? searchId, int? sequence, bool? submit) =>
        {
            // f.{dimension}=value, repeatable: values of one dimension are OR'd, dimensions AND'd.
            var facets = http.Query
                .Where(p => p.Key.StartsWith("f.", StringComparison.Ordinal) && p.Key.Length > 2)
                .ToDictionary(p => p.Key[2..], p => (IReadOnlyList<string>)[.. p.Value.OfType<string>()]);

            SuiteSearchResponse response;
            try
            {
                response = search.Search(new SuiteSearchRequest
                {
                    IndexName = IndexName,
                    Query = q,
                    Facets = facets,
                    Page = page ?? 1,
                    PageSize = PageSize,
                    SearchId = searchId,
                    Sequence = searchId is null ? null : sequence,
                    IsSubmit = submit ?? false,
                });
            }
            catch (ArgumentException exception)
            {
                // Page out of the 1000-result window, query over 256 characters, facets on an index without facets, ...
                return Results.BadRequest(exception.Message);
            }

            return Results.Ok(new
            {
                response.SearchId,
                // A server-minted search id starts at sequence 1.
                Sequence = searchId is null ? 1 : sequence ?? 1,
                Page = page ?? 1,
                PageSize,
                response.TotalHits,
                Hits = response.Hits.Select(hit => new
                {
                    hit.ItemGuid,
                    Title = hit.Fields.GetValueOrDefault("Title")?.FirstOrDefault(),
                    Url = hit.Fields.GetValueOrDefault(BaseDocumentProperties.URL)?.FirstOrDefault(),
                    Pinned = hit.PinnedByRuleId is not null,
                }),
                Facets = response.Facets,
                response.UserData,
            });
        });

        return endpoints;
    }
}
```

`GET /api/search?q=coffee` answers with this shape (ASP.NET Core serializes camelCase; values elided):

```json
{ "searchId": "…", "sequence": 1, "page": 1, "pageSize": 10, "totalHits": 0, "hits": [ { "itemGuid": "…", "title": "…", "url": "…", "pinned": false } ], "facets": [ { "dimension": "ContentType", "values": [ { "label": "…", "count": 0 } ] } ], "userData": [ { "ruleId": 0, "data": { } } ] }
```

The request fields:

| Field | Default | Notes |
|---|---|---|
| `IndexName` | required | An unknown index throws `ArgumentException` |
| `Query` | null | Null, empty or whitespace matches every document (browse). At most 256 characters. |
| `Facets` | empty | Dimension → values. At most 20 dimensions and 50 values in total. Facet filters on an index without facets throw `ArgumentException`. |
| `Page`, `PageSize` | 1, 10 | `PageSize` 1–100; `Page × PageSize` ≤ 1 000 |
| `SearchId`, `Sequence`, `IsSubmit` | null, null, false | Analytics: see [Analytics client contract](analytics-client-contract) §1. Not `Guid.Empty`; `Sequence` ≥ 1. |
| `TrackAnalytics` | true | `false` for probes, previews and admin tools |
| `Contexts` | empty | Visitor contexts for rule conditions; at most 50, each 1–100 characters |
| `ApplyRules`, `ApplySynonyms` | true, true | See [Rules and synonyms](rules-and-synonyms) |

`InvalidOperationException` means configuration is missing (no `SearchFields` for the index). Let it surface as a 500 rather than a 400.

### An MVC controller

Server-rendered, without JavaScript. With no `SearchId`, the server mints one and records the search as submitted:

```csharp
using Microsoft.AspNetCore.Mvc;

using XperienceCommunity.Lucene.Suite.Search;

namespace MySite.Controllers;

public sealed class SearchController(ISuiteSearchService search) : Controller
{
    [HttpGet("/search")]
    public IActionResult Index(string? q, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return View((SuiteSearchResponse?)null);
        }

        try
        {
            // No SearchId: the server mints one and records the search as submitted.
            var response = search.Search(new SuiteSearchRequest { IndexName = "SiteSearch", Query = q, Page = page });
            return View(response);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }
}
```

The view needs a model of type `SuiteSearchResponse?`. If you render paging links yourself, keep in mind that each page then counts as a new search, unless the links carry the search id. The Widgets components handle that for you; see [Widgets](widgets#how-the-page-search-works).

### Reading rule output

Custom data and applied synonyms come back with every response:

```csharp
using XperienceCommunity.Lucene.Suite.Search;

namespace MySite.Search;

public sealed record SearchPage(SuiteSearchResponse Response, string? BannerText, IReadOnlyList<string> UsedSynonyms);

public sealed class CampaignSearch(ISuiteSearchService search)
{
    public SearchPage Run(string query, IReadOnlyCollection<string> contactGroups)
    {
        var response = search.Search(new SuiteSearchRequest
        {
            IndexName = "SiteSearch",
            Query = query,
            Contexts = contactGroups, // e.g. ["VipCustomers"]; rule conditions can require one of them
        });

        // Custom data of every fired rule, in run order; take the first "banner".
        string? banner = response.UserData
            .Select(data => data.Data.TryGetProperty("banner", out var value) ? value.GetString() : null)
            .FirstOrDefault(text => text is not null);

        // "laptop → notebook" for each synonym variant that was searched.
        var synonyms = response.AppliedSynonyms.Select(s => $"{s.Term} → {s.Replacement}").ToList();

        return new SearchPage(response, banner, synonyms);
    }
}
```

Register it with `builder.Services.AddSingleton<CampaignSearch>();` (`ISuiteSearchService` is a singleton).

## (c) Custom `ISearchQueryFactory`

`ISearchQueryFactory` turns non-empty query text into the Lucene query. The default, `DefaultSearchQueryFactory`, ORs an analyzed query per `SearchFields` entry, each weighted by its boost. Replace it to change matching. Rules, synonyms, facets, pins, paging and analytics keep working on top of your query.

This factory keeps the default and adds a prefix match of the last word, so `grin` finds `grinder`:

```csharp
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Search;

using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Search;

namespace MySite.Search;

/// <summary>The default field query, plus a prefix match of the last word on Title, so "grin" finds "grinder".</summary>
public sealed class PrefixSearchQueryFactory(IOptions<SuiteSearchOptions> options) : ISearchQueryFactory
{
    private readonly DefaultSearchQueryFactory inner = new(options);

    public Query Create(string indexName, string queryText, Analyzer analyzer)
    {
        var query = new BooleanQuery
        {
            { inner.Create(indexName, queryText, analyzer), Occur.SHOULD },
        };

        // PrefixQuery is not analyzed, so lower-case the word as StandardAnalyzer would.
        string? lastWord = queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToLowerInvariant();
        if (lastWord is { Length: >= 3 })
        {
            query.Add(new PrefixQuery(new Term("Title", lastWord)) { Boost = 0.5f }, Occur.SHOULD);
        }

        return query;
    }
}
```

Register it as a **singleton**, before or after `AddLuceneSuite` (the Suite only adds its default when none is registered, and the last registration wins anyway):

```csharp
builder.Services.AddSingleton<ISearchQueryFactory, PrefixSearchQueryFactory>();
builder.Services.AddLuceneSuite(options => options.SearchFields["SiteSearch"] = new() { ["Title"] = 3f, ["Body"] = 1f });
```

For `coffee Grin` with `StandardAnalyzer`, it builds:

```
((Body:coffee Body:grin) ((Title:coffee Title:grin)^3.0)) Title:grin*^0.5
```

What to know when you write one:

- **When it is called:** only for non-empty query text. Browse searches use a match-all query.
- **Called per variant:** it is called once more for each synonym variant, and the Suite multiplies that query's `Boost` by `Synonyms:Weight`. Do not rely on `Boost` staying 1.
- **Index name:** `indexName` is the request's, as given (case preserved).
- **Lifetime:** the search service is a singleton and keeps the factory for the life of the app. A scoped or transient factory is captured once.
- **Errors:** an exception from the factory fails the search. The search is retried without rules and synonyms only when rules or synonyms were active.

## (d) Suggestions

Turn on the packaged endpoint for an index:

```csharp
builder.Services.AddLuceneSuiteWidgets(options =>
{
    options.SuggestionIndexes = ["SiteSearch"];
    options.SuggestionsMaxAgeSeconds = 60; // browser/CDN cache; also how long a newly blocked text can linger
});

// ...
app.MapLuceneSuiteSuggestions(); // or MapLuceneSuiteSuggestions("/api/autocomplete")
```

Call it:

```bash
curl "https://localhost/lucenesuite/suggest?index=SiteSearch&q=co"
```

It answers with a JSON array of strings. On the Dancing Goat sample index, for example:

```json
["colombia carlos imbachi","coffee processing techniques","coffee","colombia","coffee plunger"]
```

The search box (widget or `<vc:lucene-suite-search-box index-name="SiteSearch" />`) now shows these as the visitor types.

Suggestions come from what visitors searched successfully, so a new site returns `[]` until queries pass the thresholds. With the defaults, a query must be searched 5 times on 2 days, and the rollup must have run. See [Query suggestions](suggestions#which-queries-are-suggested).

### Your own endpoint

`ISuiteSuggestions` is registered by `AddLuceneSuite` and serves every index, with no allow-list or rate limit:

```csharp
using XperienceCommunity.Lucene.Suite.Suggestions;

namespace MySite.Search;

public static class SuggestApi
{
    /// <summary>GET /api/suggest?q=co: at most 5 suggestions, no rate limit or index allow-list of the packaged endpoint.</summary>
    public static IEndpointRouteBuilder MapSiteSuggestApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/suggest", async (string? q, ISuiteSuggestions suggestions, CancellationToken cancellationToken) =>
            Results.Ok(await suggestions.SuggestAsync("SiteSearch", q, maxResults: 5, cancellationToken)));

        return endpoints;
    }
}
```

`maxResults` outside 1–20 throws `ArgumentOutOfRangeException`. Render suggestions as text, never as HTML: they are visitor-typed.

## (e) Custom front end with analytics

A JavaScript search UI over the endpoint from (b) that keeps Suite analytics correct:

- it mints `searchId`/`sequence` the way the rollup expects;
- it marks submits;
- it reports clicks with the absolute position.

It uses the package's `lucene-suite.js`, so the `searchId` rules of the [Analytics client contract](analytics-client-contract) are already implemented and read from the server's settings. Needs (a) with `MapLuceneSuiteEvents()` and (b).

`wwwroot/js/site-search.js`:

```js
import { createSearchTracker, loadSettings, sendClick } from "/_content/XperienceCommunity.Lucene.Suite.Widgets/lucene-suite.js";

const settings = await loadSettings("/lucenesuite/events/settings");
const tracker = createSearchTracker(settings);

/** The page on screen: clicks must refer to the request whose response listed the item. */
let shown = null;

/** Runs one request against the site's own API (sample b). isSubmit only for Enter or the button. */
export async function search(text, { isSubmit = false, page = 1 } = {}) {
  const { searchId, sequence } = tracker.beforeSend(text, isSubmit);
  const params = new URLSearchParams({ q: text, page: String(page), searchId, sequence: String(sequence), submit: String(isSubmit) });
  const response = await fetch(`/api/search?${params}`);
  if (!response.ok) throw new Error(`Search failed: HTTP ${response.status}`);
  const result = await response.json();
  shown = { searchId: result.searchId, sequence: result.sequence, page: result.page, pageSize: result.pageSize, query: text };
  return result;
}

/** Reports a click on the hit at indexOnPage (1-based) of the page on screen. */
export function trackClick(itemGuid, indexOnPage, title) {
  if (!shown) return;
  sendClick(settings.eventsUrl, {
    searchId: shown.searchId,
    sequence: shown.sequence,
    itemGuid,
    position: (shown.page - 1) * shown.pageSize + indexOnPage,
    query: shown.query.slice(0, 100),
    title: title.trim().slice(0, 100),
  });
}

/** Wires a form holding input[name=q] and an <ol> for the results. */
export function wire(form, list) {
  const input = form.querySelector("input[name=q]");
  let timer;
  const render = (result) => {
    list.replaceChildren(...result.hits.map((hit, i) => {
      const li = document.createElement("li");
      const a = document.createElement("a");
      a.href = hit.url ?? "#";
      a.textContent = hit.title ?? ""; // hit text is content, but never use innerHTML for it
      a.addEventListener("click", () => trackClick(hit.itemGuid, i + 1, a.textContent));
      li.append(a);
      return li;
    }));
  };
  input.addEventListener("input", () => {
    clearTimeout(timer);
    timer = setTimeout(() => search(input.value).then(render, console.warn), 250);
  });
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    clearTimeout(timer);
    search(input.value, { isSubmit: true }).then(render, console.warn);
  });
}
```

On the page:

```html
<form id="site-search" role="search">
  <input name="q" type="search" aria-label="Search" />
  <button type="submit">Search</button>
</form>
<ol id="site-search-results"></ol>
<script type="module">
  import { wire } from "/js/site-search.js";
  wire(document.getElementById("site-search"), document.getElementById("site-search-results"));
</script>
```

What the tracker does with a typical session. Typing `cof`, then `coffee`, pressing Enter, opening page 2, then typing `tea`, sends:

| Request | `searchId` | `sequence` | `submit` |
|---|---|---|---|
| `cof` | A | 1 | false |
| `coffee` | A | 2 | false |
| `coffee` (Enter) | A | 3 | true |
| `coffee`, page 2 | A | 4 | false |
| `tea` | B (new) | 1 | false |

The first four are one logical search in the reports; `tea` is another. Clicks are sent with `navigator.sendBeacon` (falling back to `fetch` with `keepalive`), so they survive navigation. The events endpoint answers 202.

A strict Content-Security-Policy blocks the inline module above; move those two lines into a file in that case.

### Conversions on a later page

To credit a goal (a sign-up, a purchase) to the search result that led there, pass the result reference along. The Widgets results component adds `?sr=<searchId>.<sequence>.<position>` to tracked links; a custom UI can build it with `formatResultReference(searchId, sequence, position)` from `lucene-suite.js`. Then log the conversion on the server:

```csharp
using Microsoft.AspNetCore.Mvc;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Widgets.Events;

namespace MySite.Controllers;

public sealed class NewsletterController(ISearchInteractionLogger interactions) : Controller
{
    /// <summary>The form posts the sr value it was opened with (/newsletter?sr=...) and the item GUID of the page.</summary>
    [HttpPost("/newsletter/subscribe")]
    public IActionResult Subscribe(string email, Guid itemGuid, string? sr)
    {
        // ... subscribe the visitor ...

        if (SuiteResultReference.TryParse(sr, out var reference) && itemGuid != Guid.Empty)
        {
            interactions.LogConversion(reference.SearchId, reference.Sequence, itemGuid, reference.Position);
        }

        return Ok();
    }
}
```

- `SuiteResultReference.TryParse` never throws, and rejects anything but a valid reference (position 1–1000).
- `LogConversion` returns at once; the event is written in the background.
- A conversion counts only within the attribution window (1 hour by default) of the search response.
- Or send it from the browser with `sendConversion(settings.eventsUrl, { searchId, sequence, itemGuid, position })`.

### Related pages

- [Getting started](getting-started)
- [Configuration](configuration)
- [Widgets](widgets)
- [Rules and synonyms](rules-and-synonyms)
- [Query suggestions](suggestions)
- [Analytics client contract](analytics-client-contract)
