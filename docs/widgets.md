---
title: Widgets
nav_order: 40
---

# Widgets

Put a tracked search page on the site without writing search code. The `XperienceCommunity.Lucene.Suite.Widgets` package provides a search box, a results list and facet filters. Each comes as a Page Builder widget and as a view component. It also provides the browser endpoints for click tracking and suggestions.

```cshtml
@addTagHelper *, XperienceCommunity.Lucene.Suite.Widgets

<vc:lucene-suite-search-box label="Search" placeholder="Search the site" index-name="SiteSearch" />
<div class="search-layout">
    <aside><vc:lucene-suite-facets index-name="SiteSearch" dimension-labels="ContentType = Content type" /></aside>
    <main><vc:lucene-suite-results index-name="SiteSearch" title-field="Title" summary-field="Summary" /></main>
</div>
```

With the registration from [Getting started](getting-started), that view renders:

- a search form that submits `?q=` to the same page;
- facet links;
- results with paging;
- click beacons;
- suggestions as the visitor types (for an index listed in `SuggestionIndexes`).

## Setup

```csharp
builder.Services.AddLuceneSuite(options => options.SearchFields["SiteSearch"] = new() { ["Title"] = 3f, ["Body"] = 1f });
builder.Services.AddLuceneSuiteWidgets(options => options.SuggestionIndexes = ["SiteSearch"]);

// after app.Kentico().MapRoutes():
app.MapLuceneSuiteEvents();       // click beacons + client settings
app.MapLuceneSuiteSuggestions();  // as-you-type suggestions
```

`app.UseStaticFiles()` must run, because the scripts and styles are served from `/_content/XperienceCommunity.Lucene.Suite.Widgets/`. The options of `AddLuceneSuiteWidgets` are listed in [Configuration](configuration#widgets-suitewidgetsoptions).

## Page Builder widgets

The package registers three widgets. Its assembly is `AssemblyDiscoverable`, so no registration code is needed. All of them have `AllowCache = false`.

| Widget | Identifier | Properties |
|---|---|---|
| Lucene Suite search box | `XperienceCommunity.LuceneSuite.SearchBox` | **Results page** (page selector, one page; empty = this page), **Label** (default `Search`; also the button text), **Placeholder**, **Suggestions index** (drop-down of Lucene indexes; empty = the only `SuggestionIndexes` entry, if there is exactly one) |
| Lucene Suite search results | `XperienceCommunity.LuceneSuite.SearchResults` | **Index** (required), **Title field** (default `Title`), **URL field** (default `Url`), **Summary field** (optional stored field shown under each title) |
| Lucene Suite search filters | `XperienceCommunity.LuceneSuite.SearchFilters` | **Index** (required), **Dimension labels** (one `Dimension = Label` per line, for example `ContentType = Content type`) |

A typical results page has the search box, filters and results widgets on the same page. Other pages carry a search box whose **Results page** points at it. The results page URL is resolved in the current page's language.

If a page template restricts widgets per editable area or section, allow the `XperienceCommunity.LuceneSuite.` identifiers there.

In the Page Builder:

- **Edit mode:** results and filters show a placeholder.
- **Missing index:** a widget whose index is empty or no longer exists shows editors "Lucene Suite: choose an existing index in this widget's properties." and renders nothing for visitors.
- **Preview:** the search box is disabled, because Kentico signs preview URLs and a submitted query would fail.
- **Never tracked:** searches in preview and edit mode record no analytics, send no contact-group contexts and render no click beacons.

## View components

| Tag | Parameters |
|---|---|
| `<vc:lucene-suite-search-box>` | `action-url` (default: the current path), `label` (default `Search`), `placeholder`, `index-name` (suggestions index; same fallback as the widget) |
| `<vc:lucene-suite-results>` | `index-name` (**required**), `title-field` (default `Title`), `url-field` (default `Url`), `summary-field` |
| `<vc:lucene-suite-facets>` | `index-name` (**required**), `dimension-labels` (`Dimension = Label` lines) |

`@addTagHelper *, XperienceCommunity.Lucene.Suite.Widgets` is needed in the view or in `_ViewImports.cshtml`. Without `index-name`, Razor does not recognize `<vc:lucene-suite-results>` or `<vc:lucene-suite-facets>` as components and outputs the tag as plain HTML, without an error. `@await Component.InvokeAsync("LuceneSuiteResults", new { indexName = "SiteSearch" })` works too.

## How the page search works

All components for one index on a page share **one** search per request. The search is driven by the URL:

| Parameter | Meaning |
|---|---|
| `q` | Query text, at most 256 characters |
| `page` | 1-based page, within the first 1 000 results |
| `f.{dimension}` | Selected facet values, repeatable (at most 10 dimensions × 20 values) |
| `sid`, `seq` | Continuation of a search, written into paging and facet links |

- **Nothing to search:** with no `q` and no filter, nothing is searched unless `SearchOnEmptyQuery = true`.
- **Results per page:** `PageSize` (default 10).
- **Rules and contexts:** rules apply. With `ContactGroupContexts` on, the current contact's contact group code names are sent as [visitor contexts](rules-and-synonyms#visitor-contexts), cached 5 minutes per contact; a contact is never created for this.
- **Statuses:** the results component shows one of four:
  - nothing (not searched);
  - the results;
  - "This search could not be run" (the URL held values the search rejects);
  - "Search is unavailable right now" (the search threw; the error is logged). Unconfigured `SearchFields` for the index end up here.
- **Caching:** a page that ran a search is sent with `Cache-Control: no-store` and is excluded from ASP.NET output caching. Do not wrap the components in `<cache>` and do not cache search URLs in a CDN. Cached HTML would replay one visitor's `sid`/`seq` links and context-specific results to everyone.

The analytics side of these parameters is described in [Analytics client contract](analytics-client-contract#server-rendered-search-components).

### Result links

- **Link URL:** taken from the URL field. `~/` paths are resolved. Only relative paths and http/https URLs become links.
- **Tracked links:** same-site links of hits with an `ItemGuid` get `sr=<searchId>.<sequence>.<position>` (for [conversions on a later page](analytics-client-contract#conversions-on-a-later-page)) and click-beacon attributes.
- **Title:** the title falls back to the URL, then the item GUID.

## Overriding the markup

Copy a view into the site at the same path and edit it:

| View | Model |
|---|---|
| `Views/Shared/Components/LuceneSuiteSearchBox/Default.cshtml` | `LuceneSuiteSearchBoxModel` |
| `Views/Shared/Components/LuceneSuiteResults/Default.cshtml` | `LuceneSuiteResultsModel` |
| `Views/Shared/Components/LuceneSuiteFacets/Default.cshtml` | `LuceneSuiteFacetsModel` |

Keep the `data-lucene-suite-*` attributes if you want click tracking and suggestions to keep working.

### Class names

The results and filters ship **without CSS**; style these classes yourself:

| Component | Classes |
|---|---|
| Search box | `lucene-suite-search-box`, `__fields`, `__label`, `__label-text`, `__input`, `__submit` |
| Results | `lucene-suite-results`, `__status`, `__list`, `__item`, `__title`, `__summary`, `__placeholder`; paging `lucene-suite-pagination`, `__list`, `__previous`, `__page`, `__next` |
| Filters | `lucene-suite-facets`, `__group`, `__title`, `__list`, `__item`, `__link`, `__link--selected`, `__label`, `__count`, `__state`, `__placeholder` |

Suggestions ship with `lucene-suite-suggest.css`; their classes and CSS custom properties are listed in [Query suggestions](suggestions#classes-and-theming).

## Scripts and styles

All are under `/_content/XperienceCommunity.Lucene.Suite.Widgets/`. They are dependency-free ES modules with no inline script or style, so a strict Content-Security-Policy works.

| File | Referenced by | Purpose |
|---|---|---|
| `lucene-suite.js` | `lucene-suite-results.js`; your own UI | Analytics client: `loadSettings`, `createSearchTracker`, `sendClick`, `sendConversion`, `newGuid`, `formatResultReference`, `parseResultReference`, `norm`, `osa`, `typo` |
| `lucene-suite-results.js` | the results view, when it renders at least one tracked link and the events endpoint is mapped | Sends a click beacon when a tracked result link is clicked or middle-clicked. Exports `clickPayload`, `clip`. |
| `lucene-suite-suggest.js` | the first search box with suggestions on a page | Suggestions combobox: `fetchSuggestions`, `createSuggestState`, `enhanceSearchBox`, `autoInit`, `MAX_INPUT_LENGTH` |
| `lucene-suite-suggest.css` | the same, unless `SuggestionStyles = false` | Suggestion list styling |

Using `lucene-suite.js` from your own front end is walked through in [Samples](samples#e-custom-front-end-with-analytics).

## Endpoints

### Events: `MapLuceneSuiteEvents(pattern = "/lucenesuite/events")`

`GET {pattern}/settings` returns the analytics settings a client must use, cached for 5 minutes:

```bash
curl -i https://localhost/lucenesuite/events/settings
```

```
HTTP/1.1 200 OK
Cache-Control: public, max-age=300

{"textChangeTimeoutMs":30000,"typoOneEditMinLength":4,"typoTwoEditMinLength":8,"deepBackspaceRatio":0.5,"attributionWindowMs":3600000,"eventsUrl":"/lucenesuite/events"}
```

`POST {pattern}` takes one click or conversion as JSON (`application/json` or `text/plain`, as `navigator.sendBeacon` sends it), at most 1 024 bytes:

```bash
curl -i -X POST https://localhost/lucenesuite/events -H "Content-Type: text/plain" \
  -d '{"type":"click","searchId":"6f9619ff-8b86-4d01-b42d-00cf4fc964ff","sequence":1,"itemGuid":"3f2504e0-4f89-41d3-9a0c-0305e82c3301","position":3,"eventId":"0b5bd4a2-35f3-4b8c-9d0e-6a1f3d8a7c11","query":"coffee grinder","title":"Hario V60"}'
```

| Field | Required | Meaning |
|---|---|---|
| `type` | yes | `click` or `conversion` |
| `searchId` | yes | The response's search id |
| `sequence` | yes | Sequence of the request whose response listed the item |
| `itemGuid` | yes | The hit's item GUID |
| `position` | clicks | 1-based absolute position, 1–1000 |
| `eventId` | no | GUID minted once per event and resent on retry, so a retry counts once |
| `query`, `title` | no | Clicks only; at most 100 characters each, used only for the contact's [search activity](activities). A wrong type or longer value is dropped, not rejected. |

| Answer | When |
|---|---|
| 202 | Accepted. The event is buffered; whether it is attributed is decided by the rollup. |
| 400 `Invalid event.` | Unknown `type`, a click without `position`, empty GUIDs, `sequence` < 1, position out of range, bad JSON |
| 413 | Body over 1 024 bytes |
| 429 | Over `EventsPerMinute` for the client IP |

The full contract, including when to mint a `searchId`, is in [Analytics client contract](analytics-client-contract).

### Suggestions: `MapLuceneSuiteSuggestions(pattern = "/lucenesuite/suggest")`

```bash
curl -i "https://localhost/lucenesuite/suggest?index=DancingGoatSample&q=co"
```

```
HTTP/1.1 200 OK
Cache-Control: public, max-age=60

["colombia carlos imbachi","coffee processing techniques","coffee","colombia","coffee plunger"]
```

Status codes, caching and the blocklist are described in [Query suggestions](suggestions#endpoint-contract).

### Rate limits and proxies

Both POST events and suggest requests are limited per client IP (`EventsPerMinute`, `SuggestionsPerMinute`), in memory, without touching the host's own rate limiter configuration. The limit uses `HttpContext.Connection.RemoteIpAddress`. Behind a reverse proxy, load balancer or CDN, configure ASP.NET Core forwarded headers. Otherwise every visitor shares the proxy's IP and one budget. If the proxy already rate-limits, set the option to 0.

Both mapping methods return the endpoint builder, so you can add host conventions such as `.RequireRateLimiting(...)` or `.RequireHost(...)`.

### Related pages

- [Getting started](getting-started)
- [Query suggestions](suggestions)
- [Search activities](activities)
- [Analytics client contract](analytics-client-contract)
- [Samples](samples)
