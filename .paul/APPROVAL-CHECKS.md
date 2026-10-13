# Approval checks (deferred wrap-up)

Human-verify checkpoints no longer block APPLY (user decision 2026-10-11). Each one is recorded here with what to review; work continues, and everything below is reviewed together at the end of the milestone (v0.1, after 06-03). Answer each item with "approved" or the change wanted; changes found here go into a follow-up plan.

| # | Plan | Check | Status |
|---|------|-------|--------|
| 20 | 13-04 | Query tester per handoff: summary strip with rule chips, score bars, rows per page, Compare Moved column, Try again | Pending |
| 19 | 13-03 fixes | Rule/synonym form: row alignment with errors, red delete icons, friendly type errors, no request-driven Save flashing | Pending |
| 18 | 13-03 | Search reports per handoff: sparklines, data bars, rows per page, empty state, Retry with stale fade | Pending |
| 17 | 13-02.1 | Rule/synonym edit: repeated saves work (form reloads after save); rule conflict alert with confirmed Reload | Pending |
| 16 | 13-02 | Rule edit per handoff: sections, live status, contact-group and facet pickers, pins/hides layouts | Pending |
| 1 | 06-01 | Public API + endpoint shape | Approved 2026-10-11 (before this process) |
| 2 | 06-02 | Search component markup, parameters and URL names | Approved 2026-10-12 (heading labels resolved by 06-03) |
| 3 | 06-03 | Page Builder widgets, host Site search page, end-to-end evidence | Approved 2026-10-12 with one follow-up: host index stores readable facet values |
| 4 | 07-02 | Seeded demo history on the CommunityProjects host (v0.2) | Pending |
| 5 | 08-01 | Search reports page (range bar, KPIs, trend, latency) | Pending |
| 6 | 08-02 | Query table + row actions (query tester / create rule / add synonym) | Pending |
| 15 | 12-01 | Public API: inventory + internalized types, SearchClick, one Widgets namespace, PublicApiAnalyzers baseline, docs/public-api.md | Pending |
| 14 | 11-01/11-02 | Search insights: Fix these first card, All insights panel, actions per kind, ranking by searches affected | Pending |
| 13 | 10-01 | Search activities (3 custom types, installed disabled) + click contract v2 (optional query/title) | Pending |
| 12 | 09-03 | Blocked suggestions admin (listing/create/delete) + one-click report Block suggestion | Pending |
| 11 | 09-02 | Search box autocomplete (ARIA combobox script, opt-in, stylesheet) + admin right gutter (Convention 2b) | Pending |
| 10 | 09-01 | Suggestions public API (ISuiteSuggestions, MapLuceneSuiteSuggestions, options) + endpoint behaviour; seeded history now public | Pending |
| 9 | 08.1-05 | Rule/synonym editors: icon removes, condition blocks, error timing, inline errors; query tester empty column | Pending |
| 8 | 08.1-03 | Search reports dashboard redesign, range stepping, listing-style query row actions | Pending |
| 7 | 08.1-02 | Query tester native action bar + chips row, side-panel focus return, 24 px region gaps | Pending |

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

---

## 7. 08.1-02: Query tester action bar, side-panel focus, spacing (v0.2)

1. **Where:** Site search → DancingGoatSample → Query tester (also Search reports, Create rule).
2. **Captures** (project-sidecar/docs/ui-audit/): before `qt-default-1100.jpg`, `qt-chips-1100.jpg`; after `after-qt-default-1100.jpg`, `after-qt-default-1700.jpg`, `after-reports-top-1100.jpg`, `after-rule-rows-1700.jpg`; native reference `ref-contenthub-chips-1100.jpg`.
3. **What I verified in the browser pane:**
   - **Layout, at 669 px and 1269 px content width:**
     - The search box and the Filter button share one row.
     - Applied filters are on their own row, 40 px under the search.
     - The switches are on their own row.
     - The input's accessible name is "Query" (accessibility tree).
     - The placeholder fits: it needs 285 px of the 484 px available.
     - On the Test index (no facets) there is no Filter button and the search takes the full width.
   - **Focus after closing a panel** (`document.activeElement`):

     | Panel | Close by | Lands on |
     |---|---|---|
     | Filter | Cancel / Apply / X / Escape | Filter button ✓ |
     | Filter opened from a chip | Cancel | that chip ✓ |
     | Filter opened from a chip | Apply that removed the chip | Filter button ✓ |
     | Filter | Cancel, then Tab to a switch within 30 ms | the switch (not pulled back) ✓ |
     | Hit detail | Escape | table area ✓ (rows can't take focus, known since 03-02) |
     | Contact groups, Baseline | Cancel / Escape / X | its button ✓ |
     | Contact groups, Tuned | Escape / X, and Cancel when repeated on its own | its button ✓ |

     - In one scripted run, Tuned → Cancel landed on the Baseline button while the previous panel was still animating closed. A direct repeat landed correctly. Watch for it.
     - While the pane is hidden, focus sits on the page body for about 0.5 s before returning (animation frames throttled in the background). It is immediate in a visible window.
   - **Gaps, at both widths:**
     - Reports: Period → KPI 24 px, KPI → chart 24 px.
     - Compare: note → columns 24 px, columns side by side at 1269 px.
     - Rule editor: condition and boost rows 12 px apart (were 0).
   - **Host data:** SearchEvent 19 / 19, Rule 0 / 0, Synonym 0 / 0, Interaction 2 / 2. Nothing saved.
4. **Your decisions to confirm:**
   - **The visible "Query" label and explanation are gone** (native listing style). The hint "leave empty to list all documents" now lives in the placeholder and disappears while typing.
   - **The rule editors got the spacing fix as well,** although the plan listed them as off-limits. Their rows were collapsed to 0 px gaps by the same Row-in-Stack cause, and the change is spacing only.
   - **New finding for 08.1-03 (M6):** at 669 px, the Remove buttons of facet, context and boost rows overflow the rule form card by 31–47 px (`after-rule-rows-1100-remove-overflow.jpg`). This is not caused by this plan: the wrapper doesn't change the Row's width.

---

## 8. 08.1-03: Search reports dashboard (v0.2)

1. **Where:** Site search → DancingGoatSample → Search reports.
2. **Captures** (project-sidecar/docs/ui-audit/): `after3-reports-top-1700.jpg`, `after3-querytable-1700.jpg`, `after3-reports-7days-stepped-1100.jpg`. Reference: the Community Portal reporting screenshot in the kit README.
3. **What I verified in the browser pane** (669 and 1269 px content width):
   - **Layout** (from your choices):
     - Period toolbar with ‹ › stepping: 7 days shows Oct 5–11 with Next disabled; Previous moves to Sep 28–Oct 4. "7 days" stays selected, and the stepped week is restored after a reload.
     - KPI strip with Volume and Quality tiles (wraps 2+2+2 at 669 px). Selecting a tile charts its metric and restores after a reload.
     - Trend card with "Show breakdown" (side panel) next to Needs attention (top 5 no-result queries, browse row excluded) and Response time ("Response-time details" side panel).
     - Query table with a sort-driven title.
     - The trend "Show as table" view is paginated by week (7 days per page), newest first: 30 days gives 5 pages, page 1 Oct 11–5, page 2 Oct 4–Sep 28. It resets when the range or metric changes.
   - **Numbers, 30 days vs SQL on SearchDaily:** searches 3,681 = 3,681; session-days 1,480 = 1,480; CTR 30.7 % = 30.7 %.
   - **Query table:**
     - 25 rows, content 48 px inside a 50 px row (was 104 px, overlapping). No header clipped.
     - Actions are three icon buttons on one line, named `Open "q" in the query tester` / `Create rule for "q"` / `Add synonym for "q"`.
     - The browse row's rule and synonym buttons are disabled, with the reason as a tooltip and as hidden text.
   - **Focus:** the breakdown panel (Escape) and the response-time panel (Escape, X) return to their buttons.
   - **Contrast:**
     - White on accent: 8.0–11.5:1.
     - Good delta `--color-success-text`: 5.03:1. Bad delta `--color-alert-text`: 4.99:1.
     - Deltas also carry hidden "better"/"worse" text.
   - **Host data:** SearchEvent 19 / 19, Rule 0 / 0, Synonym 0 / 0, Interaction 2 / 2. Nothing saved.
4. **Your decisions to confirm:**
   - **Layout changes:** the insight notes moved into the "Show breakdown" side panel (you asked for side-panel insights; the tile only selects the metric). The latency histogram moved into a side panel.
   - **Row actions** are icon-only buttons like native listings. Hovering gives a tooltip; screen readers hear the full name.
   - **Needs attention** costs one extra Queries request per range change.
   - **Not observed:** View-only users (disabled-by-permission icons); the empty "No searches" state; a range with no final day.

---

## 9. 08.1-05: Rule and synonym editors (v0.2)

1. **Where:** Rules → Create rule (conditions, boosts, pins, hides); Synonyms → Create synonym; Query tester.
2. **Capture:** `after5-rule-condition-1100.jpg`.
3. **What I verified in the browser pane** (669 and 1269 px content width):
   - **Row removes:** a condition with 2 facets and 2 contexts plus 2 boosts gives 7 icon-only removes ("Remove facet 2 of condition 1" etc.), 0 past the card at either width (was 31–47 px over at 669 px).
   - **Remove focus:** after Remove facet 2, focus is on facet 1's remove; after the last facet, on "Add facet".
   - **Condition block:** bordered group; "Condition 1" is Headline S (14 px bold), heading level 3; the trash icon sits in its header.
   - **Error timing:**
     - Adding a condition, facets, contexts and boosts shows no error.
     - After a real click into Text and then onto empty page space, the empty-pattern error appears under the field.
     - Synonym: one term added → nothing; leaving the group → polite inline "Must have 2–20 terms."; no alert bar.
   - **Query tester:** columns Title / Score / Item GUID; the empty Body column is gone.
   - **Host data:** Rule 0 / 0, Synonym 0 / 0, SearchEvent 19 / 19, Interaction 2 / 2. Nothing saved.
4. **Your decisions to confirm:**
   - **Headline S is 14 px** (the kit maps S/M/L to 14/16/24), not the 16 px the plan said. It reads as a bold group title at label size.
   - **Errors appear once focus leaves the whole block or editor,** not only the single field (plus the existing per-field blur, and always after a failed save).
   - **Not observed:**
     - read-only (View) forms (no such user);
     - a failed Save with errors visible: Save stayed disabled for the empty Name, so no save attempt was made.
   - **Accessibility tree:** the read_page tree showed the synonym "Add a term" input without a name. Its label uses the same id mechanism as the rule inputs, which `input.labels` confirms are labelled. Unverified.

---

## 10. 09-01: Query suggestions: public API and endpoint (v0.2)

1. **Public API added** (layered-package rule; your decision):
   - Core: `ISuiteSuggestions.SuggestAsync(indexName, input, maxResults?, ct)`, plus `MaxInputLength = 100` and `MaxResultsLimit = 20`.
   - Options `LuceneSuite:Suggestions`: WindowDays 90, MaxNoResultRate 0.5, MinSearches 5, MaxCandidates 2 000, MinInputLength 2, MaxResults 8, CacheMinutes 60.
   - Widgets: `MapLuceneSuiteSuggestions(pattern = "/lucenesuite/suggest")` and `SuiteWidgetsOptions.SuggestionIndexes` (opt-in), `SuggestionsPerMinute` (300), `SuggestionsMaxAgeSeconds` (60).
   - Module class `LuceneSuite.BlockedSuggestion` (installer v6).
2. **Host** (CommunityProjects; files backed up to project-sidecar/backups/09-01):
   - `SearchStartupExtensions.cs`: `AddLuceneSuiteWidgets(o => o.SuggestionIndexes = [DancingGoatSample])`.
   - `Program.cs`: `app.MapLuceneSuiteSuggestions()` plus `using XperienceCommunity.Lucene.Suite.Widgets.Suggestions;`.
   - The installer upgraded comm_projects 5 → 6, and the table exists.
3. **What I verified with curl:**
   - `co` returns colombia carlos imbachi, coffee processing techniques, coffee, colombia, coffee plunger.
   - `bl` returns bleached, bleached paper filters v60-01, black, t-shirt black (the later-word group).
   - `grinder` returns grinder, hario skerton hand grinder, porlex tall ceramic burr grinder.
   - `x` returns `[]` (input too short).
   - Headers: `Content-Type: application/json`, `Cache-Control: public, max-age=60`.
   - Another index → 404; 101 characters → 400; no `q` → `[]`.
   - Host data unchanged: SearchEvent 19 / 19, SearchDaily 3 389 / 3 389.
   - The 429 path is proven by the endpoint tests; it was not run on the host.
4. **Your decisions to confirm:**
   - **Seeded demo history is now public** on the host via suggestions; these strings come from it. Clearing #4 shrinks suggestions to real traffic.
   - **Developer friction:** each endpoint family has its own namespace, so the host needed an extra `using` for `MapLuceneSuiteSuggestions`. Candidate for the Phase 11 API audit, which should consider one namespace for all `MapLuceneSuite*` mappings.
   - `maxResults` outside 1–20 throws `ArgumentOutOfRangeException` from the C# API; the endpoint never passes it.
   - Accents are not folded ("creme" does not match "crème"), as with synonyms (MatchKey).

---

## 11. 09-02: Search box autocomplete (v0.2)

1. **Where:** http://localhost:27340/site-search (CommunityProjects; no host content edited).
2. **What I verified on the live page** (the real script in the real page, driven by dispatched input and keydown events because the browser pane was hidden):
   - **Markup:**
     - the single-index fallback enabled DancingGoatSample;
     - the form carries `data-lucene-suite-suggest`, `-index` and `-min-length`;
     - ids `lucene-suite-q-1`, `-listbox`, `-status`;
     - assets `lucene-suite-suggest.js/.css?v=…`, referenced once.
   - **Typing:** "c, co, cof, coff" in quick succession → **one** request (`q=coff`). It shows 3 options with "coff" highlighted (`<mark>`) and a polite "3 suggestions available".
   - **Keys:** ↓↓ → option 2 active (`aria-activedescendant`, `aria-selected`); ↑×3 wraps correctly; Escape closes (value kept); a second Escape clears.
   - **Enter:** on "coffee" → `/site-search?q=coffee`, 10 results.
   - **Analytics:** SearchEvent 19 → 20, exactly the submitted search. The suggest calls wrote nothing.
   - **Accessibility:** the input is `role="combobox"`, labelled "Search" by its `<label>`. The accessibility-tree reader showed the placeholder as its name; the DOM label is correct.
3. **Automated checks:**
   - node 41/41 (incl. the XSS test: `<img onerror>` shown as text; the 429 back-off; nothing over 100 characters);
   - .NET 683 passed, including golden-HTML tests proving the off path is byte-identical;
   - two boxes on a page get unique ids, with the assets referenced once.
4. **Also in this step:** admin **right gutter** (Convention 2b, your request). Custom templates (Query tester, Search reports) now keep native's 104 px between content and the scrollbar, measured at 669 and 1269 px content width. The page has no horizontal overflow.
5. **Your decisions to confirm:**
   - **Auto-index:** used when exactly one index is configured.
   - **Shipped stylesheet:** `lucene-suite-suggest.css`; turn off with `SuggestionStyles = false`.
   - **Not observed:**
     - real OS keyboard and mouse input (pane hidden);
     - whether the widget's optional "Suggestions index" dropdown can be cleared once a value is picked (needs Page Builder);
     - after closing, the status text keeps "3 suggestions available". It is not re-announced, but could be cleared.

---

## 12. 09-03: Blocked suggestions (v0.2)

1. **Where:** http://localhost:27340/admin/site-search/indexes/4/blocked-suggestions, and Search reports → query table on the same index.
2. **What I verified on the host** (comm_projects; host rebuilt and restarted on the new code):
   - **Report action:** clicking Block on "colombia carlos imbachi" stored one exact entry and showed the toast "Suggestions for this index no longer include … Remove it under Blocked suggestions to undo."
     - `/lucenesuite/suggest?q=co` dropped it at once.
     - A second click reported "already blocked" and stored nothing.
   - **Keyboard focus:** after Enter on the block button, focus stays on that button. This was fixed during APPLY; it first fell to `<body>`.
   - **Listing:** a native listing with columns Text / Match / Last modified (UTC), "Block suggestion" in the header and native delete with confirmation.
   - **Create:**
     - "Plunger" + Contains words removed "coffee plunger" from the `co` suggestions;
     - " !! " was refused ("Enter a text that contains at least one letter or digit");
     - "plunger" (exact) was refused while "PLUNGER!" (exact) existed: "…already blocked in this index with the same match".
   - **Layout:**
     - 4 row icons on one line with 50 px rows at 1100 and 1700 px viewports, and no page overflow;
     - at 1100 px the table scrolls inside its card, as it already did with 3 icons.
   - **Labels:** DOM labels "Blocked text" and both match options are correct. The accessibility-tree reader shows them unnamed, as it misnamed the combobox in #11.
   - **Cleanup:** all 3 entries were deleted through the UI and the suggestions are back to the baseline. Host counts were unchanged: SearchEvent 20, SearchDaily 3 389, Rule 0, Synonym 0, BlockedSuggestion 0.
3. **Automated checks:**
   - .NET 708 passed, 5 skipped. This includes DB tests for store, already-blocked, cross-index isolation, refusals, a missing index, and immediate removal from cached suggestions.
   - Client 70/70; tsc clean.
4. **Your decisions to confirm:**
   - **No audit trail of who blocked what.** Kentico wrote no event-log entry for blocklist (or synonym/rule) changes despite `LogEvents = true`, so AC-5 failed and is recorded in KNOWN-LIMITATIONS. Do you want explicit event-log entries (small change: create, delete and report block log the text, index and user)? Note that the host's event log holds only 1000 rows and the leftover XpSearch macro errors fill it in about two days.
   - **No edit page:** delete and re-add instead.
   - **No confirmation** on the one-click report block; it is undone from the listing.
   - **Not in Needs attention.**
   - **No "working" state** while a block request runs (repeat clicks are ignored) — see KNOWN-LIMITATIONS.
   - **Not observed:**
     - an empty Save shows no "required" message when driven by a DOM click with the pane hidden (the same on the native Synonyms create page; nothing is stored);
     - real mouse input (pane hidden);
     - a View-only role (read-only listing, disabled report button — code and tests only).

---

## 13. 10-01: Search activities for Kentico contacts (v0.2)

1. **Where:**
   - Contact management → Activity types (three "Search …" types);
   - a contact's Activities;
   - http://localhost:27340/site-search.
2. **What I verified on the host** (comm_projects moved to v7 on restart):
   - **Install:** the types were created disabled and custom: Search performed, Search without results, Search result clicked.
   - **Disabled:** a search plus a click recorded analytics (SearchEvent +1, Interaction +1) and 0 Suite activities.
   - **Enabled** (all three switched on in the admin, no restart), for one tracked contact in the browser pane. The pane was already tracked, so no consent action was needed:
     - `mail jane@example.com` → `Searched for "mail {email}"` and `Search without results: "mail {email}"` (value `mail {email}`, comment DancingGoatSample);
     - `coffee` → one Search performed;
     - the page-2 continuation → none;
     - clicking "Hario V60" at position 11 → `Search result clicked: "Hario V60" (position 11)`, value `coffee`, linked to the Hario V60 page (WebPageItemGUID matched).
   - **URL privacy:** every Suite activity URL is `http://localhost:27340/site-search` with no query string. For the click it is the results page, not the beacon endpoint. A SQL search for "jane" across title, value, comment, URL and referrer of the Suite activities found 0 rows.
   - **Switching off** in the admin took effect without a restart: a new search logged no activity. All three types are disabled again.
   - **Cleanup:** the 4 Suite verification activities were deleted. Kentico's own page-visit/landing activities from the browsing were kept, as in earlier sessions.
   - **Kentico's own** page visit stored `/site-search?q=coffee` raw (documented in docs/activities.md).
3. **Automated checks:**
   - .NET 739 passed, 5 skipped. This includes v6 → v7 install, re-run preserving a marketer-enabled type, the no-HTTP-context path, the `?q=jane@example.com` URL test and endpoint v1/v2/malformed bodies.
   - Widgets JS 45/45, including a worst-case v2 body of 825 bytes (limit 1024).
   - Rules-overhead latency test passed when run on its own.
4. **Your decisions to confirm:**
   - **Disabled by default:** marketers must enable the types.
   - **Title formats** (see docs/activities.md).
   - **Click text is browser-supplied** (your choice). It is forgeable only for the forger's own contact, never stored in analytics, and the public events contract is extended (v2, optional fields).
   - **Click activity URL** = the results page. During APPLY this was changed from the beacon endpoint, which would be meaningless.
   - **`ISearchInteractionLogger.LogClick` gained two optional parameters.** Custom implementers must update; Phase 11 should review this.
   - **Internal Kentico type `WebPageItemInfo`** is used for the page link.
   - **Not observed:**
     - a visitor without consent (Kentico's own check; the pane was already a tracked contact);
     - a real web farm.

---

## 14. 11-01 + 11-02: Search insights (v0.2)

1. **Where:** Site search → DancingGoatSample → Search reports, in the **Fix these first** card and the **Show all insights** panel.
2. **What I verified on the host** (seeded 60 days, 30-day range):
   - **Findings:**
     - 7 findings out of 36 queries checked; the top 5 are the 100%-no-result queries ("returns policy" 26/26, "wifi password", "job openings", "parking", "pizza delivery");
     - the panel groups "No results (6)" and "Got worse (1)" as h3 headings with explanations.
   - **Ranking fix during APPLY** (your call to confirm):
     - The first run ranked four "Got worse" findings and one "Slow" finding above the 100%-no-result queries. A regression counted *every* search of the query as affected. "Slow" fired for a p95 range of 500 ms–1 s at a 1 s setting, because it compared the range's upper edge.
     - Now a regression's affected count = searches × the change. A query is slow only when its whole p95 range is at or above the setting, and its affected count = the searches in ranges at or above it.
     - Tests and docs/insights.md were updated.
   - **Actions:**
     - "Add synonym" on "returns policy" opened Create synonym with the term filled in;
     - a temporary synonym (returns policy ↔ refund policy) then showed as "Already applies: Synonym" linking to its edit page;
     - the synonym was deleted afterwards.
   - **Fallback:** with the 7-day preset, no query reached the thresholds and the card showed "No findings at your thresholds. Top queries without results:" with the old list. Changing the range reloads.
   - **Layout and data:**
     - at 1100 and 1700 px viewports, no overflow, action icons on one line (5 card items, 7 panel items), and a correct heading outline (h1 page, h2 card, h3 kinds);
     - each finding is an `li` labelled by its query;
     - host counts unchanged (SearchEvent 28, SearchDaily 3 389, Rule 1, Synonym 0, BlockedSuggestion 0).
     - The 1 rule ("Boost items for test") existed before this check; it isn't mine.
3. **Automated checks:** .NET 798 passed, 6 skipped; client 77/77; tsc clean; build 0 warnings.
4. **Your decisions to confirm:**
   - **Ranking:** "searches affected" as now defined (the fix above).
   - **Card title** "Fix these first"; kind labels (No results / Results not clicked / Answer buried / Slow / Got worse); the buttons per kind.
   - **Fallback** to the old no-result list (audit must-have).
   - **11-01 latency:** 47 ms warm, but 284 ms on the first call after a restart.
   - **Not observed:**
     - focus return on panel close: animation frames don't run in the hidden browser pane, and the existing response-time panel behaves the same way there;
     - Create rule / Query tester / Block suggestion from a finding: they share the query-table handoff and command; no Relevance-miss finding existed for the Block button;
     - a View-only role.

---

## 15. 12-01: Public API audit (v0.2)

1. **Where:**
   - .paul/phases/12-public-api-audit/12-01-INVENTORY.md;
   - src/*/PublicAPI.Unshipped.txt;
   - docs/public-api.md.
2. **What changed:**
   - **Public types:** 163 → 147 (Core 50 → 35, Admin 86 unchanged, Widgets 27 → 26).
   - **Made internal (17):**
     - the rule model and serializer types (RuleDefinition, RuleCondition, RuleActions, RulePin, RuleBoost, RuleFacet, RuleAnchoring, RuleMetadata, RuleLimits, RuleParseResult, RuleSerializer);
     - the synonym model and serializer types (SynonymDefinition, SynonymKind, SynonymLimits, SynonymParseResult, SynonymSerializer);
     - Widgets SuiteClientSettings (only its JSON is the contract);
     - plus RuleInfo/SynonymInfo.ToDefinition().
   - **Classes:** 48 Supported; 98 Framework-required (public only for Kentico/ASP.NET: admin pages, page-command DTOs, form components, Info classes and so on).
   - **`LogClick(SearchClick click)`** replaces the 7-parameter method. Validation and exceptions are unchanged; `LogClick(null)` now throws ArgumentNullException.
   - **One namespace:** `MapLuceneSuiteEvents` and `MapLuceneSuiteSuggestions` moved to `XperienceCommunity.Lucene.Suite.Widgets`. The host now needs one using; its Program.cs was edited byte-preserving, with the backup in project-sidecar/backups/12-01.
   - **Baseline:**
     - Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0 in the three packages. Shipped is empty (nothing released); Unshipped holds the whole surface.
     - Proof: a throwaway public method failed the build with `error RS0016: Symbol 'static XperienceCommunity.Lucene.Suite.ApiProbe.Probe() -> void' is not part of the declared public API`; it was then removed.
3. **Verified:**
   - build 0 warnings with the analyzer active; .NET 797–798 passed (the RulesOverhead timing test flaked once in a full run and passed 3/3 alone; the implementer measured the same flake rate at HEAD); client 77/77; Widgets js 45/45; host builds.
   - **Host smoke check after restart:**
     - all Suite admin pages load: index listing, Search reports with insights, query tester, rules listing + rule edit (stored values shown) + create, synonyms + create, blocked suggestions;
     - /site-search renders 10 tracked results;
     - suggest 200, events/settings 200, events POST 202.
   - **Smoke-test data removed:** the 1 fake click row and the 2 curl searches. The first rules-listing load after the restart was slow and looked empty; it showed the rule on reload.
4. **Your decisions to confirm:**
   - The 17 internalized types (the rule/synonym JSON models are no longer usable from host code).
   - `SearchClick` shape.
   - Which namespace the endpoints live in.
   - `SiteSearchPermissions` classed Supported.
   - SuiteResultReference stays in `Widgets.Events`.
   - docs/public-api.md wording (incl. the "Other public surfaces" list).

---

## 16. 13-02: Rule edit (enterprise handoff Screen 4, v0.2)

1. **Where:** Site search → DancingGoatSample → Rules → any rule, or Create rule.
2. **What I verified on the host** (host rebuilt on 13-02; admin dev server restarted, see note):
   - **Sections** in this order: General / Conditions / Actions / Custom data. The status line is at the top, with the handoff copy (priority, dates, custom data).
   - **Unedited save of rule 4:** every stored field (conditions, actions, name, description, enabled, priority, dates) byte-identical by SQL; only LastModified changed.
   - **Live status:**
     - Active → Disabled → Active as Enabled toggles, unsaved, in a `role="status"` region;
     - Valid from 2026-10-13 10:00 in a New York browser → "Scheduled · starts 2026-10-13 14:00 UTC".
   - **Contexts:**
     - the picker lists "Grinder shoppers" (Contact group) and "Custom context…"; the recipient list "Dancing goat recipient list" is absent;
     - a saved rule stores the bare code name `["CoffeeGrinders"]` and reopens as "Grinder shoppers".
   - **Facets:**
     - Facet lists `ContentType`; Value lists "Product page 32", "Article page 7" and "Other value…";
     - 32 + 7 = 39 index documents, so no synonym inflation;
     - the stored value is `{"dimension":"ContentType","value":"Product page"}`.
   - **Reveal inputs:** "Other value…" and "Custom context…" show inputs labelled "Facet value" / "Custom context", and focus moves into them.
   - **Errors:**
     - a 201-character text gives "At most 200 characters." on the Text input (aria-invalid);
     - a rule without actions gives "actions: At least one pin, hide, boost or userData is required."
   - **Unsaved changes:** Kentico's native "You have unsaved changes… DISCARD" prompt exists in 31.9.1.
   - **Layout:** the condition box has no overflow at 1100 / 1700 px (box 501 / 903 px) and the page has no horizontal overflow.
   - **Cleanup:**
     - temporary rule 505 deleted through the listing dialog;
     - temporary synonym #6 deleted;
     - rule 4 content restored (identical);
     - counts: Rule 1, Synonym 0, Blocked 0.
     - SearchEvent rows 31–32 (22:09, "coffee") are not from these checks.
3. **Automated checks:**
   - .NET 835 passed, 6 skipped;
   - client 92/92, also under TZ=America/New_York;
   - tsc clean; Debug and Release builds 0 warnings (Release embeds the bundle).
4. **Your decisions to confirm:**
   - **Pre-existing defect found (not 13-02):** a second Save without reloading is rejected as "changed by someone else" on rule **and** synonym edit pages. Reproduced on the untouched Synonym page.
     - Cause: the save handler sets `LoadedVersion` on the model, but the form items sent back still carry the old token.
     - Fix proposal: set the hidden LoadedVersion item's value before `GetClientProperties` (a small quick fix for rules and synonyms).
   - **"Valid from (UTC)" labels are misleading (since 03-06):** Kentico's DateTimeInput works in browser-local time and shows "10:00 AM EDT". The status line converts correctly (14:00 UTC), but the label says UTC. Proposal: drop "(UTC)" from the labels, or explain "your local time".
   - **Status line:** asks the server (`RuleListing.Of`, same as the listing) instead of a client copy of the rule.
   - **Cosmetic:** "Conditions" and "Custom data" appear twice (category title + field label).
   - **Handoff deviations:** no sticky save bar (native Save); stacked fields; CheckBox not Switch; category subtext shown as a tooltip (FormCategory has no subtext in 31.9.1); facet dimension has no "Other…" option.
   - **Not observed:**
     - a role without Contact groups → View (server test only);
     - the stale facet label ("not in the index") on the host, covered by node tests;
     - FacetValues / ContactGroups failure bars.
   - **Environment note:** the 13-02 Release build's `npm ci` hit a locked folder (my shell's working directory), and node_modules was restored by the implementer. The long-running admin dev server had cached the broken state and crashed the admin until restarted. If the Suite admin shows "Something went wrong" in Debug, restart `npm start`.

---

## 17. 13-02.1: Save-conflict quick fix (v0.2)

1. **Where:** any rule or synonym edit page (Site search → DancingGoatSample → Rules / Synonyms).
2. **Root causes found:**
   - **Primary:** Kentico 31.9.1's admin client does **not** apply returned form values after a successful save (verified by capturing requests and with a debug line). The hidden concurrency token stayed at its page-load value forever. It *does* apply them after a failed save.
   - **Secondary:** right after `Set()`, the saved object's LastModified is local-kind, so it formats with an offset unlike the DB-read value.
   - **Your decision:** a successful **edit** save now navigates to the same edit page (`NavigateTo(..., refetchAllTemplates: true)`, the way Kentico's own field editor refreshes). The form reloads with a fresh token from the DB. Create is unchanged.
3. **What I verified on the host:**
   - **Repeated saves:**
     - rule: three saves in a row → "Rule saved." ×3, the toast survives the reload, the DB holds the last description;
     - synonym: three saves (Enabled toggled) → "Synonym saved." ×3, the DB matches.
   - **Real conflict (simulated by a SQL LastModified bump while the form was open):**
     - rule and synonym: two saves in a row are both refused and nothing is stored;
     - a retry cannot overwrite the other editor's change.
   - **Conflict alert (rule):**
     - "This rule was changed by someone else." + RELOAD (accessible name "Reload the rule") in an aria-live polite region;
     - Reload opens the dialog "Reload the rule? | Your unsaved changes are lost. | CANCEL | RELOAD", with focus in the dialog;
     - Cancel returns focus to Reload and keeps the edit;
     - confirming reloads from the DB and the alert is gone.
   - **Unedited save of rule 4:** stored content byte-identical.
   - **Cleanup:** temporary rule 506 and synonym #7 deleted through the UI; counts Rule 1 / Synonym 0 / Blocked 0.
4. **Automated checks:**
   - .NET 836–837 passed, 6 skipped. The only failure is the known RulesOverhead timing flake, which passed alone.
   - 2 new DB tests: three save cycles with the reloaded token; refusals keep the old token with Conflict = true.
   - Client 92/92; tsc clean; Release build embeds the bundle.
5. **Your decisions to confirm:**
   - **The form reloads after every successful edit save,** so scroll position and focus go back to the top.
   - **The synonym page has no Reload alert** (message only).
   - **Still open from 13-02:** the "Valid from/until (UTC)" labels vs Kentico's browser-local date input.

---

## 18. 13-03: Search reports per handoff (v0.2)

1. **Where:** Site search → DancingGoatSample → Search reports.
2. **What changed (numbers, SQL and insights are unchanged):**
   - **KPI tiles:** a sparkline from the series already loaded (no extra request), aria-hidden, white on the selected tile.
   - **Query table:** data bars for Searches (scaled to the page maximum), No results and CTR; footer "1–25 of N queries" with Rows per page 10/25/50 (validated on the server, remembered per index).
   - **Needs attention:** each finding shows the query, "{n} searches" and a data bar.
   - **Empty period:** a Callout "No searches in this period" with "Open query tester".
   - **Failed load:** the alert gets a Retry button, and "Showing the last loaded report." appears under it.
3. **What I verified on the host:**
   - **Sparklines and colours:** 6 tiles, 30 points for 30 days, data-bar track colour resolves at runtime.
   - **Layout at both widths:**
     - ≈670 px: rows 50 px, row actions on one line, the table scrolls inside its card;
     - 1269 px: no overflow, rows 50 px, the table fits.
   - **Rows per page:** footer "1–25 of 141 queries"; 10 works and persists across a reload.
   - **Empty Callout:** shows, and the button navigates to the query tester.
   - **Simulated failure (requests blocked in the browser):**
     - alert + Retry + stale line;
     - sparklines and trend bars at 0.6 opacity;
     - text at full opacity (#525252 on white, ≈7.8:1);
     - the query table and the insights card fade only when their own request fails, so fresh cards stay at full colour;
     - Retry is disabled while its request is in flight, and on success the alert, stale line and fade go away.
   - **Nothing written:** SearchEvent 30, SearchDaily 3389, Rule 1, Synonym 0, Blocked 0 (unchanged).
4. **Automated checks:** client 100/100; tsc clean; SearchReportsPage DB tests 7/7, including the page-size validation test.
5. **Your decisions to confirm:**
   - **Stale fade is on graphics only** (the handoff fades the whole report to 60 %, which would drop low-emphasis text to about 3:1).
   - **Searches bars are scaled to the page maximum,** so bar lengths aren't comparable across pages (handoff design; the number is always shown).
   - **If the last report that loaded was empty,** a failed load shows only the alert and stale line: the empty Callout is hidden because it would describe an earlier period.

---

## 19. Rule and synonym form fixes after 13-03 (v0.2, your feedback)

1. **Where:** rule create/edit (conditions, boosts, pins) and synonym create/edit.
2. **What changed:**
   - **Row alignment:** boost, facet and context rows align to the top, so an error under one field no longer pushes its neighbours down. The delete icon is centred on the input box.
   - **Delete icons are red:** Kentico destructive style on all rule-form row deletes.
   - **Wrongly typed values** (empty pin position, `-` as strength): the field's own message ("Must be 1–300.", "Must be −100..100 and not 0.") instead of the raw serializer text.
   - **Save flashing:**
     - Live validation and the status line now use page commands (`ValidateSection`, `RuleStatus`, `ValidateTerms`). Kentico marks the form busy for every form-component command, but not for page commands.
     - Only the field being edited validates, once per typing pause; before, every keystroke sent 6 requests.
3. **What I verified on the host:**
   - alignment measured with errors showing; icon colour rgb(225, 0, 7);
   - `-` in strength shows the friendly message;
   - typing sends one `ValidateSection` request for the edited field only;
   - the rule create page validates and shows "Active";
   - the synonym create page validates ("Must have 2–20 terms.");
   - nothing was saved.
4. **Automated checks:** .NET 847 passed / 6 skipped (the RulesOverhead flake passed alone); client 100/100; tsc clean; Debug + Release 0 warnings.
5. **Your decisions to confirm:**
   - **Kentico's own Save flicker remains:** a few ms per keystroke with no request. Kentico's user edit form does the same.
   - **`RuleStatus` needs View permission only,** so read-only viewers still see the status.
   - **Pins keep typed positions:** drag order was considered and dropped.
   - **Strength stays Kentico's numeric input** (type=text, inputMode numeric). Phones get no minus key, which matches Kentico's own integer fields.

---

## 20. 13-04: Query tester per handoff (v0.2)

1. **Where:** Site search → DancingGoatSample → Query tester (try "test coffee" for rule 4, and Compare).
2. **What changed:**
   - **Summary card:**
     - Total hits and Elapsed;
     - "Fired rules (n)" as chips: icon + name + " · 1 pin", linking to the rule;
     - "Synonyms applied (n)" as Tags that open the synonym.
   - **Results table:**
     - # (continues across pages);
     - Title with a "Pinned #n" tag;
     - Score as a blue bar relative to the page's top score, number at 4 decimals;
     - Item GUID in small monospace.
   - **Range footer:** "1–10 of N documents", Rows per page 10/25/50, Pagination (stops at the result window).
   - **Compare:**
     - Baseline and Tuned keep their own switches and contexts (your decision);
     - each is now a table: Baseline # / Title / Score, Tuned # / Title / Moved (arrow + n, "—", "New").
   - **Failure:** "Search failed" gets Try again; the previous results stay with faded bars.
   - **Server:** fired rules carry the rule's action summary.
3. **What I verified on the host:**
   - **Summary card:**
     - chip "Boost items for test · 1 pin" → /rules/4: product-hover text, 8 px radius, 4/10 px padding, icon hidden from screen readers;
     - headings at levels 1 and 2.
   - **Results table:**
     - columns # / Title / Score / Item GUID, rows 50 px;
     - pinned Chemex score 0 → empty bar;
     - bars in rgb(61, 93, 255) (`--color-info-icon`) relative to the top score;
     - Pinned tag on `--color-background-selected`.
   - **Paging:** page 2 numbers 11–20 ("11–20 of 28 documents"); 25 per page → page 1, 25 rows.
   - **Blocked search requests (audit check):**
     - page 2 and a change to 50 per page left # and the footer describing the rows shown;
     - Try again was disabled in flight, the region was aria-busy, and it recovered;
     - bars at 0.6 opacity, text at 1;
     - the empty state was not shown.
   - **Compare:**
     - Chemex "New" (pinned by the rule);
     - others "1 place down" with an aria-hidden arrow + number in the alert colour at 600 weight, and visually hidden "1 place down";
     - explainer shown;
     - row → details panel; closing returns focus to the Tuned table (checked with a requestAnimationFrame shim, since the hidden pane doesn't run it).
   - **Empty:** "No documents match" + "Try a different query.".
   - **Synonyms:** temporary synonym (plunger ↔ zzpress) → tag "plunger → zzpress" opens /synonyms/8; deleted through the listing afterwards.
   - **Layout:** 669 and 1269 px content width, both modes: no overflow; Compare stacks when narrow; titles truncate with an ellipsis.
   - **Nothing written:** counts unchanged (SearchEvent 35, SearchDaily 3389, Rule 1, Synonym 0, Blocked 0, SearchInteraction 8).
4. **Fixed during the check:**
   - after a failed page-size change, Kentico's Select kept showing the new size; it now remounts after a failure (RangeFooter `resetKey`);
   - the "No documents match" callout no longer shows next to "Search failed".
5. **Found and fixed by the implementer (pre-existing):** a stored rule with duplicate JSON keys made `RuleSummaries` throw inside Search ("Search rejected"); summaries now degrade to "Invalid".
6. **Automated checks:** .NET 857 passed / 6 skipped (the RulesOverhead flake passed alone); client 103/103; tsc clean; Debug + Release 0 warnings.
7. **Your decisions to confirm:**
   - **"Pinned #n" shows where the pinned item actually landed** (hits don't carry the configured pin position).
   - **A rule with no actions shows " · None";** invalid JSON shows " · Invalid — open to fix".
   - **Score bars are relative to the current page's top score.**
   - **Rows per page isn't remembered** after leaving the page.

