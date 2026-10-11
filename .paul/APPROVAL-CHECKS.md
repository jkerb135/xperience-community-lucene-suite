# Approval checks (deferred wrap-up)

Human-verify checkpoints no longer block APPLY (user decision 2026-10-11). Each one is recorded here with what to review; work continues, and everything below is reviewed together at the end of the milestone (v0.1, after 06-03). Answer each item with "approved" or the change wanted; changes found here go into a follow-up plan.

| # | Plan | Check | Status |
|---|------|-------|--------|
| 1 | 06-01 | Public API + endpoint shape | Approved 2026-10-11 (before this process) |
| 2 | 06-02 | Search component markup, parameters and URL names | **Pending** |

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
