---
phase: 01-foundation
plan: 03
subsystem: admin
tags: [kentico-admin, page-extender, admin-client, webpack, proxy-mode, embedded-client, react]

requires: [01-02 search API]
provides:
  - Suite.Admin module registering client module xperience-community/lucene-suite
  - Per-index "Search tuning" section under Kentico Lucene's IndexListingPage (static search-tuning → parameterized section → query-tester)
  - IndexListingPageExtender (table link per index; RowAction untouched)
  - QueryTesterPage + React QueryTesterTemplate over ISuiteSearchService
  - Client pipeline: Proxy (port 3011) in Development, npm build embedded into Suite.Admin.dll for every non-Debug configuration
  - AdminClientEmbeddingTests, QueryTesterValidationMessage test
affects: [Phase 2 event capture, Phase 3 rules pages, Phase 4 synonyms pages, Phase 5 preview page]

tech-stack:
  added: ["@kentico/xperience-admin-base/-components/-webpack-config 31.9.1", "react/react-dom 18.3.1", "typescript + @types/react (typecheck only)", "Kentico.Xperience.Lucene.Admin >= 15.0.5"]
  patterns: [static + parameterized SECTION_LAYOUT pair under a listing page, PageExtender table link, Release-only npm MSBuild target with stamp-file incrementality]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite.Admin/LuceneSuiteAdminModule.cs, src/XperienceCommunity.Lucene.Suite.Admin/UIPages/*.cs, src/XperienceCommunity.Lucene.Suite.Admin/Client/**, tests/XperienceCommunity.Lucene.Suite.Tests/AdminClientEmbeddingTests.cs]
  modified: [Directory.Packages.props, .gitignore, src/XperienceCommunity.Lucene.Suite.Admin/XperienceCommunity.Lucene.Suite.Admin.csproj, tests/XperienceCommunity.Lucene.Suite.Tests/*, ../xbk/Program.cs (untracked), ../xbk/appsettings.Development.json (new, untracked)]

key-decisions:
  - "Section is a sibling of IndexEditPage under IndexListingPage (user option A, 2026-10-09): Kentico UITree.ValidateNodeSettings forbids non-SidePanel/Dialog children of Edit-template pages"
  - "Entry is a TableActions.AddLink (cogwheel) per row; the listing's RowAction stays upstream's/XpSearch's"
  - "Explicit [UIEvaluatePermission(VIEW)] on all three pages; no custom permission yet"
  - "Validation text shown to the user drops the runtime ' (Parameter ...)' / 'Actual value' suffixes; exception types unchanged"

duration: ~1.5 h APPLY + checkpoint/fix loop (commits 2026-10-09 06:06 → 07:33 -04:00)
completed: 2026-10-09
---

# Phase 1 Plan 03: Admin application + client pipeline + query tester Summary

**Each Lucene index now has a "Search tuning" section in Kentico's Lucene admin with a working query tester over the suite search API. The React client is served by the webpack dev server in Development and is embedded into Suite.Admin.dll on every non-Debug build. All seven 01-02 shell behaviours are verified in a running host. AC-5 (permissions) was approved by the user, but the user did not report what they observed.**

## Acceptance Criteria Results

AC-3 and AC-5 were written when the section was planned as a child of IndexEditPage. AC-3 was re-planned on 2026-10-09 (user option A). AC-5's Given clause ("children of Lucene's IndexEditPage") is stale. Its intent, that access follows the Lucene app's VIEW permission, is unchanged.

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Release embeds; Debug needs no npm | Pass | Clean Release build ran `npm ci --ignore-scripts` (813 packages) + `npm run build`; second Release build skipped both targets; Debug with dist deleted: no npm, 0/0; broken entry.tsx → Release fails with the named-folder message (reverted, identical); Node missing → "Node.js is required…" error; empty dist → clear error; `-c Staging` embeds |
| AC-2: Proxy mode in development | Pass | Checkpoint 7: heading text change visible after browser refresh with no xbk restart; reverted, git diff clean. 7b: dev server down → rest of admin loads; only the Suite page shows Kentico's contained "Something went wrong / Reload page" |
| AC-3: Section under a single index (re-planned) | Pass | Checkpoint 3: link → `/admin/lucene/indexes/search-tuning/1/query-tester`; breadcrumb Lucene Search › List › dancinggoat › Query tester; nav "Query tester"; no stray menu from the static layer. Checkpoint 8: id 99999 → "This object doesn't exist." |
| AC-4: Query tester via suite API | Pass | Checkpoint 4: empty query → 38 docs; "coffee" → 28 hits ranked (top score 1.45, 54.5 ms); page 2 = ten different hits, 0 overlap. 5: faceted; Coffees → 8 hits with sideways counts kept (Brewers 6, Accessories 6). 6: 257 chars → "Search rejected" callout + toast, text exactly "Query must not exceed 256 characters." |
| AC-5: Permissions follow the Lucene app | Pass (approved by user, observations not reported) | Step 10 was done by the user. The orchestrator could not do it without signing the admin out, and email is not configured. The user replied "approved" but did not describe what they saw. Code side: explicit `[UIEvaluatePermission(SystemPermissions.VIEW)]` on all three pages; Search command is `Permission = VIEW` |
| AC-6: Nothing regressed | Pass | Suite Release build 0 warnings / 0 errors; `dotnet test -c Release` 35/35 (incl. AdminClientEmbeddingTests, QueryTesterValidationMessage_DropsRuntimeSuffixes); `dotnet restore --locked-mode` OK after commit (re-run at UNIFY); xbk scratch builds normal + SeparatedAdmin: 0 errors, 279 warnings (= baseline); Suite.Admin.dll absent from SeparatedAdmin output. Checkpoint 9: Release + Proxy entry removed → bundle served embedded from `/admin/adminresources/xperience-community.lucene.suite/entry.kxh.*.js`, 38 docs |

## Checkpoint Results (run in the browser by the orchestrator, user signed in)

| Step | Result |
|------|--------|
| 3 Row action → section | PASS |
| 4 Search, paging, empty query | PASS |
| 5 Facets | PASS |
| 6 257-char validation | PASS (text cleaned by fix 3) |
| 7 Proxy hot edit | PASS |
| 7b Dev server down | PASS: Kentico contains the failure to the Suite page |
| 8 Unknown index id | PASS |
| 9 Embedded mode | PASS |
| 10 Permissions (real role) | Done by the user: "approved", details not reported |

## Fixes from the Checkpoint (user: "fix 1-3 now")

| # | Issue | Fix | Verified | Commit |
|---|-------|-----|----------|--------|
| 1 | Facet checkboxes' accessible name was "on" | `aria-label "<Dimension>: <Label> (<count>)"` | DOM | `4370053` |
| 2 | At ~790 px: stats ran together, facets overlapped results, table clipped | Stats in separate Columns; facets stack above results below 1366 px (results header top 1338 > last facet bottom 1290); Kentico Table scrolls internally (770 in 357) | Browser | `4370053` |
| 2b | Search button clipped at narrow widths | Input Col12 / colsMd Col10, button in a content-sized Column (QueryTesterTemplate.tsx 142/150) | Button inside content area | `b3c4ecf` |
| 3 | Validation text carried runtime suffixes | `QueryTesterPage.ValidationMessage` strips them; exception types unchanged | Browser + unit test | `4370053` |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 0: Back up xbk/Program.cs | Done: project-sidecar/backups/01-03/Program.cs; appsettings.Development.json did not exist before (rollback = delete it) | n/a (outside repo) |
| 1: Admin module + client + Proxy/Embedded build | Done | `4aa42df` |
| 2: Section, query tester, extender, React template | Done (re-planned placement, deviations below) | `4aa42df`, fixes `4370053`, `b3c4ecf` |
| 3: Wire xbk | Done: `using XperienceCommunity.Lucene.Suite;` + AddLuceneSuite for dancinggoat (displayTitle ×3, ArticlePageSummary, ArticlePageText, ProductFieldDescription, found read-only with a DirectoryReader); new appsettings.Development.json with Proxy port 3011 | n/a (xbk untracked) |
| Checkpoint | Approved | n/a |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Placement re-planned: IndexListingPage → static `search-tuning` (SECTION_LAYOUT, hidden) → parameterized section (IndexIdentifier) → query-tester; entry via `TableActions.AddLink` | Kentico UITree.ValidateNodeSettings forbids non-SidePanel/Dialog children of Edit-template pages (user option A) | URL `/admin/lucene/indexes/search-tuning/{id}/query-tester`; AC-3/AC-5 wording updated |
| 2 | Root page lives in SearchTuningSectionPage.cs | Fewest files | None |
| 3 | Explicit `[UIEvaluatePermission(VIEW)]` on all three pages | Not inheriting from IndexEditPage any more, so the permission is stated explicitly | Same access rule as planned |
| 4 | Page size fixed at 10; 1000 window limit duplicated from SearchExecutor | Keeps the template simple | Logged in project-sidecar/docs/KNOWN-LIMITATIONS.md |
| 5 | Results reuse `SuiteSearchHit` | No separate DTO needed | None |
| 6 | Added `typecheck` script + typescript/@types/react | Babel strips types without checking them | Dev-only |
| 7 | Null facet value lists coerced to empty | Defensive client handling | None |
| 8 | Extra `using` in xbk/Program.cs | Required for the AddLuceneSuite extension | One line beyond the planned call |
| 9 | Fix commit `b3c4ecf` was made by the orchestrator directly, not by a subagent | Small in-loop layout fix | Recorded here |
| 10 | Orchestrator added solution-root `.claude/launch.json` (suite-admin-client, xbk, xbk-release) | Needed to run the checkpoint in the browser | Outside the Suite repo |

## Skill Audit

Skill audit: All required skills invoked ✓
- `/dotnet:csharp-refactoring`: invoked in both APPLY runs and in the fix run.
- Admin design kit consulted. Components used: Stack, Row, Column, Headline, Input, Button, Callout, TextWithLabel, Checkbox, Table, Pagination, Card; `usePageCommandProvider`.
- content-retrieval: not applicable (ILuceneIndexManager lookup).

## 01-02 SHELL_UNTESTED: Closed

| # | Behaviour | Verified by checkpoint step |
|---|-----------|------------------------------|
| 1 | Unknown index → error | 8 |
| 2 | Strategy via GetRequiredService + cast | 3–4 |
| 3 | Facet vs plain path from FacetsConfigFactory() | 5 |
| 4 | Index analyzer passed through | 4 |
| 5 | Elapsed from the shell's Stopwatch | 4 (54.5 ms shown) |
| 6 | Validation before GetIndex | 6 |
| 7 | AddLuceneSuite after AddKenticoLucene resolves | 3 |

All 7 now verified in a running host. The STATE deferred row is removed.

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| Permissions follow the Lucene app: marketers need Lucene app access, and future editing needs UPDATE, which also allows index edit/rebuild/delete | Before Phase 3 editing pages |
| AC-5 step-10 observations were not reported | Re-check with a real role when custom permissions land (Phase 3) |
| Stored fields visible to anyone with Lucene VIEW | With the Phase 3 permission work / field allow-list |
| Node.js required for every non-Debug build | Release/CI phase |
| npm vulnerability scanning in CI (dev-only: 5 highs, logged in KNOWN-LIMITATIONS; `npm audit --omit=dev` = 0) | Release/CI phase |
| No client unit tests | When the client gains logic (Phase 3+) |
| Fixed page size 10 / duplicated 1000 limit | KNOWN-LIMITATIONS |

## Next

Phase 1 is complete (3/3). Next: Phase 2, Event Capture (`/paul:plan`).

---
*Completed: 2026-10-09*
