# Approval checks (deferred wrap-up)

Human-verify checkpoints no longer block APPLY (user decision 2026-10-11). Each one is recorded here with what to review; work continues, and everything below is reviewed together at the end of the milestone (v0.1, after 06-03). Answer each item with "approved" or the change wanted; changes found here go into a follow-up plan.

| # | Plan | Check | Status |
|---|------|-------|--------|
| 1 | 06-01 | Public API + endpoint shape | Approved 2026-10-11 (before this process) |
| 2 | 06-02 | Search component markup, parameters and URL names | **Pending** |
| 3 | 06-03 | Page Builder widgets, host Site search page, end-to-end evidence | **Pending** |

---

## 2. 06-02 — Search component markup, parameters and URL names

**Built:** server-rendered search box, results and facets view components sharing one search per request; tracked result links (`sr=`, click beacons); toggle-link facets; contact-group contexts. Summary: `.paul/phases/06-widgets-package/06-02-SUMMARY.md`.

**Evidence at the time of the check:** `dotnet build` 0 warnings; `dotnet test` 574 passed, 3 skipped (opt-in); `npm test` (Widgets/js) 27/27; `dotnet pack` ships `lucene-suite.js` + `lucene-suite-results.js`, views compiled into the DLL.

**Review:**
1. **Component parameters** (public once sites use them):
   ```
   <vc:lucene-suite-search-box action-url? label="Search" placeholder? />
   <vc:lucene-suite-results index-name="…" title-field="Title" url-field="Url" summary-field? />
   <vc:lucene-suite-facets index-name="…" />
   SuiteWidgetsOptions { EventsPerMinute = 60; PageSize = 10; SearchOnEmptyQuery = false; ContactGroupContexts = true; }
   ```
2. **URL parameter names** (public once sites link to them): `q`, `page`, `f.{dimension}`, `sid`, `seq`, plus `sr` on result links.
3. **Public types** overriding views and custom pages use: `SuitePageSearch.For(HttpContext, indexName)` / `EventsUrl(HttpContext)`, `SuitePageSearchResult`, `SuitePageSearchStatus`, `SuiteSearchUrl`, `LuceneSuiteSearchBoxModel`, `LuceneSuiteResultsModel` (+ `LuceneSuiteResultHit`, `LuceneSuitePageLink`), `LuceneSuiteFacetsModel` (+ `LuceneSuiteFacetGroup`, `LuceneSuiteFacetOption`).
4. **Default markup** — regenerate samples with the rendering tests, or see the excerpt below:
   ```html
   <a class="lucene-suite-results__title" href="/coffee/grinder?x=1&amp;sr=8b7e…6b.1.11#frag"
      data-lucene-suite-item="1f2e…5b" data-lucene-suite-position="11">&lt;script&gt;… &amp; co</a>
   <span class="lucene-suite-results__title">JS</span>                <!-- javascript: URL, not linked -->
   <a class="lucene-suite-results__title" href="https://external.example/p">External</a>   <!-- no sr, not tracked -->
   <li><a … href="?q=grinder&amp;f.ContentType=Product%20%26%20Co&amp;page=2&amp;sid=8b7e…6b&amp;seq=2"
          rel="nofollow" aria-current="page">2</a></li>
   <a class="lucene-suite-facets__link lucene-suite-facets__link--selected" href="?q=grinder&amp;sid=…&amp;seq=2" rel="nofollow">
     <span class="lucene-suite-facets__label">Product &amp; Co</span> <span …count>(5)</span> <span …state>selected</span></a>
   ```
5. **Open question:** facet headings are `<h2>` with the raw dimension name (e.g. "ContentType") and all default text is English (sites override the views). Want a display-name mapping option instead?
6. **Deviations to accept:** `SuitePageSearch` is public (view components need public constructor types); the search box has no `indexName` (it never searches); contacts read via `ICurrentContactProvider.GetExistingContact()`; Kentico `~/` URLs resolved with `Url.Content`; `data-lucene-suite-events=""` renders empty when no events endpoint is mapped; one-search-per-request rendering test is a single request (search + facets), not a two-component page.
7. **Not yet verified, carried into 06-03:** the Kentico contact-group membership query against a real database; a real-browser pass on the host.

---

## 3. 06-03: Page Builder widgets, host Site search page, end-to-end evidence

**Built:**
- Three Page Builder widgets that wrap the 06-02 components.
- Preview and edit mode are never tracked.
- Facet dimension labels.
- CommunityProjects wiring and a permanent **Site search** page.

Summary: `.paul/phases/06-widgets-package/06-03-SUMMARY.md`.

**Evidence at the time of the check:**
- `dotnet test`: 588 passed, 3 skipped (opt-in), including the contact-group query test on `comm_projects_test`.
- `npm test`: 27/27.
- Pack ships both JS files.

**Review:**
1. **Widgets as marketers see them:**

   | Name | Icon | Properties |
   |---|---|---|
   | Lucene Suite search box | magnifier | Results page (page selector), Label, Placeholder |
   | Lucene Suite search results | list | Index (drop-down), Title field, URL field, Summary field |
   | Lucene Suite search filters | funnel | Index, Dimension labels |

   Identifiers: `XperienceCommunity.LuceneSuite.SearchBox`, `.SearchResults`, `.SearchFilters`. All three have AllowCache off.
2. **Host page:** "Site search" at `/site-search`, under Landing pages, published. Its WebPageItemGUID is `e13638b9-f089-4a73-bb17-74f1f2d1e9ac` (item ID 69). Layout:
   - Area `top`: a 1-column section with the search box (results page = itself).
   - Below it, a 25/75 section: filters in the side panel (index DancingGoatSample, label "ContentType = Content type") and results in the main zone.

   The old XpSearch "Search" page (`/Landing_pages/Search`, 14 dead XpSearch widgets) is untouched, per your choice. It is part of the pending XpSearch cleanup.
3. **End-to-end run** (browser pane, 2026-10-11 22:10 UTC, search id `bdefc913-877e-4c5d-87b5-76eea755c16c`):

   | Step | Recorded |
   |---|---|
   | Submit "coffee" | `LuceneSuite_SearchEvent` seq 1: 28 results, IsSubmit 1 |
   | Next page | seq 2: page 2, IsSubmit 0 |
   | Product page facet | seq 3: 1 filter, 24 results, IsSubmit 0 |
   | Click "AeroPress Filters" | `LuceneSuite_SearchInteraction` seq 3, position 2, item `e420ae5d…`; beacon POST → 202; target URL `/products/aeropress-filters?sr=bdefc913….3.2` |

   Every search response had `Cache-Control: no-store`.
4. **Preview and edit mode:**
   - Opening the page in Page Builder (read-only preview), and in edit mode via a draft that was then reverted to published, added **no** analytics rows.
   - Edit mode showed both placeholders and a disabled search box.
   - Read-only preview shows a disabled search box.
5. **Your decisions to confirm:**
   - **Host edits** (untracked, backed up in `project-sidecar/backups/widgets-2026-10-11`):
     - `CommunityProjects.csproj`: Widgets project reference.
     - `Search/SearchStartupExtensions.cs`: `AddLuceneSuiteWidgets()`.
     - `Program.cs`: `MapLuceneSuiteEvents()`.
     - `Helpers/AreaRestrictionHelper.cs`: allows `XperienceCommunity.LuceneSuite.` widgets on landing pages. This was not in the plan; without it the landing-page template refuses the widgets.
   - **Search box disabled in all of preview**, not only edit mode. Kentico signs preview URLs, so a submitted search 404s inside the preview frame.
   - **Facet values are raw** content type code names (e.g. `DancingGoat.ProductPage`). Only dimension headings can be relabelled. Want value labels too?
   - **Test traffic is kept** in `comm_projects`: 5 search events (3 from the browser run, 2 from my `curl` probes) and 1 click. `curl` is not on the bot list.
6. **Deviations to accept:**
   - The plan's page "Search" at `/search` became "Site search" at `/site-search`, so it doesn't clash with the XpSearch page.
   - The widget view components are named `LuceneSuite*Widget`, which also makes them usable as `<vc:lucene-suite-results-widget>`.
   - `LuceneSuiteFacetGroup` gained a positional `Label`. That is a breaking change to the 06-02 record, but it is unreleased.
