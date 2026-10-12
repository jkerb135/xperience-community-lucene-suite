# Approval checks (deferred wrap-up)

Human-verify checkpoints no longer block APPLY (user decision 2026-10-11). Each one is recorded here with what to review; work continues, and everything below is reviewed together at the end of the milestone (v0.1, after 06-03). Answer each item with "approved" or the change wanted; changes found here go into a follow-up plan.

| # | Plan | Check | Status |
|---|------|-------|--------|
| 1 | 06-01 | Public API + endpoint shape | Approved 2026-10-11 (before this process) |
| 2 | 06-02 | Search component markup, parameters and URL names | Approved 2026-10-12 (heading labels resolved by 06-03) |
| 3 | 06-03 | Page Builder widgets, host Site search page, end-to-end evidence | Approved 2026-10-12 with one follow-up: host index stores readable facet values |
| 4 | 07-02 | Seeded demo history on the CommunityProjects host (v0.2) | Pending |
| 5 | 08-01 | Search reports page (range bar, KPIs, trend, latency) | Pending |
| 6 | 08-02 | Query table + row actions (query tester / create rule / add synonym) | Pending |

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

---

## Milestone v0.1 review (2026-10-12)

| Item | Decision |
|---|---|
| #2 public names (components, options, URL parameters q / page / f.{dimension} / sid / seq / sr, model types) | Approved as is |
| #3 widgets, 4 host edits (incl. AreaRestrictionHelper allow-list), search box disabled in all Kentico preview, /site-search page (XpSearch page untouched) | Approved |
| #3 facet values show raw code names | Follow-up: the CommunityProjects indexing strategy stores readable content-type facet values (host-only change + index rebuild; no Suite change; the host has no rules referencing facet values) |
| Test traffic in comm_projects | Keep |

---

## 4. 07-02: Seeded demo history on the host (v0.2)

1. **What was written:**
   - `comm_projects`, table `LuceneSuite_SearchDaily`, index `DancingGoatSample`, days **2026-08-12..2026-10-10** (60 days, UTC), 3,477 rows. Nothing else was touched.
   - Generated from the host index (39 documents, 114 texts) with 150 searches per weekday, random seed 2026, and rolled by the real rollup engine.
   - Totals: 7,725 searches, 1,031 with no results (13 %), 2,335 clicked (30 %), 203 conversions.
2. **Verified by SQL after the run:**
   - No rows outside the range.
   - The watermark is still empty.
   - Raw searches are still 17, all real traffic from 2026-10-11/12.
   - The rollup job starts at 2026-10-11, so it never rolls a seeded day.
3. **What you can look at:**
   - SQL for now, e.g. `SELECT QueryText, SUM(Searches) FROM LuceneSuite_SearchDaily GROUP BY QueryText ORDER BY 2 DESC`.
   - The Phase 8 report screens once they exist.
4. **To remove the seeded days** (run from the Suite repo root, in one shell):

   ```bash
   export LUCENESUITE_SEED_CONNECTIONSTRING="Data Source=localhost;Initial Catalog=comm_projects;Integrated Security=True;Encrypt=False" LUCENESUITE_SEED_CONFIRM=comm_projects LUCENESUITE_SEED_INDEX=DancingGoatSample LUCENESUITE_SEED_CLEAR=2026-08-12..2026-10-10
   dotnet test tests/XperienceCommunity.Lucene.Suite.Tests --filter SeedRunner --logger "console;verbosity=detailed"
   ```

5. **Your decisions to confirm:**
   - Keep the seeded history on the host (yes/no).
   - Seeded texts count toward surfacing. Real queries that match a seeded text (e.g. "burr", "bleached") are named in reports and will seed Phase 9 suggestions after fewer real searches.

---

## 5. 08-01: Search reports page (v0.2)

1. **Where:** Site search → Indexes → DancingGoatSample → **Search reports** (`/admin/site-search/indexes/4/reports`). It is the first page in the index's section menu.
2. **What I verified in the browser pane** (America/New_York browser, UTC−4):
   - **30 days:** Sep 12–Oct 11 vs Aug 13–Sep 11.
     - 3,681 searches, the same as SQL `SUM(Searches)`; 1,480 session-days, the same as SQL.
     - −5.6 %, which is (3,681 − 3,900) ÷ 3,900.
     - Rates show percentage points, e.g. "+0.9 pp".
   - **90 days:** the previous period starts before 2026-08-12, so "No comparison" shows on every tile plus a notice. 7,731 = 7,725 seeded + 6 real.
   - **Custom range:** Aug 13 → Aug 20 picked in a UTC−4 browser reports "Aug 13, 2026 – Aug 20, 2026" (no off-by-one). 1,050 searches, the same as SQL.
   - **Empty range** (May 4–10): the "No searches" card shows.
   - **"Not final" notice:** shown while the watermark was empty. It went away once the job rolled 2026-10-11 during the check.
   - **Views and layout:** table view for each day; latency histogram + table.
     - Headings: h1 → h2 × 3.
     - No horizontal overflow at a 1100 px viewport (~650 px content) or 1700 px (~1,220 px content).
     - KPIs show 2 per row below 1366 px and 3 per row from 1366 px.
     - Tab order: presets → Custom → switches.
3. **Your decisions to confirm:**
   - **The selected range is not kept in the URL.** Writing `?from&to` raced Kentico's admin router (the URL became `/admin/?from=...` and a reload went to the admin home), so it was dropped as the audited plan prescribed. A reload returns to the last 30 days.
   - **Kentico's date-range picker brings its own shortcuts** ("Last 48 hours", "Last 7 days" ... "Last 60 days") next to our presets. The npm typings cannot hide them. They work (any range is converted to days), but they overlap our presets.
   - **Chart colours** come from the tokens the 31.9.1 runtime defines: bars `--color-icon-highlighted` (brand purple), no results `--color-alert-icon` (red), not-final days hatched with `--color-background-selected`. The kit repo's newer tokens (`--color-background-primary`, spacing and font-size variables) resolve to nothing in 31.9.1 and first rendered the bars black.
   - **Average click position** shows 1.0 on the seeded data. Seeded queries mostly match one or two documents, so this comes from the seed data, not from the page.

---

## 6. 08-02: Query table and row actions (v0.2)

1. **Where:** Site search → DancingGoatSample → Search reports → card **Queries** (under Latency).
2. **What I verified in the browser pane:**
   - **Top, 30 days:** the first rows match SQL over surfaced texts (bleached 612, which brewing fits you? 285, anfim super caimano 212, burr 183, colombia carlos imbachi 115, injerto 92).
   - **Table layout:**
     - The browse row reads "(browse – no query text)", and its Create rule / Add synonym are disabled.
     - "(other): 195 searches …" shows with an explanation tooltip.
     - Paging works (25 per page).
   - **Query tester:** opened with "bleached" and ran it (2 hits). No raw analytics row was written (untracked). Reloading the tester opens it empty (single use).
   - **Create rule:** opened with condition 1 = "Is" + "bleached", name empty. Reloading Create rule opens it empty. **Nothing was saved.**
   - **Add synonym** (from Slowest, page 2): opened with "ethiopia yirgacheffe (decaf)" as the first term. Leaving and coming back restored the report exactly (30 days, Slowest, page 2, same first row).
   - **The host still has 0 rules and 0 synonyms.**
   - **Layout and keyboard:**
     - At 1700 px the table fits.
     - At 1100 px it scrolls horizontally inside its own frame (as Kentico listings do); no cell overflows.
     - Tab order runs sort toggles → row buttons; each button is announced by its label.
3. **Your decisions to confirm:**
   - **Labelled buttons instead of Kentico's row-action icons.** The npm 31.9.1 `ActionCell` announces every icon button as just "button", so each row has three small labelled buttons stacked in the Actions column. They are taller rows than an icon strip.
   - **The reason a button is disabled** (missing permission, browse row, `{email}`/`{number}` text) shows as a hover tooltip. A disabled button can't take keyboard focus, so keyboard users don't hear the reason.
   - **The permission-disabled state** is verified in code and tests only. The host has no user without Manage rules / Manage synonyms, and the editors of existing rules and synonyms couldn't be exercised because the host has none. The guard against filling them is covered by a node test (sibling edit path).
   - **Unexplained, not reproduced:** once during testing the saved report state showed "7 days" without that preset being clicked. Three deliberate repeats restored and saved correctly. Watch for it.
