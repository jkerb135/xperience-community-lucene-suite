---
phase: 02-event-capture
plan: 01
subsystem: analytics
tags: [module-class, installer, sp_getapplock, bulk-insert, channels, background-service, cookie-level, pii-scrub]

requires: [01-02 search API, 01-03 query tester]
provides:
  - Resource LuceneSuite + class LuceneSuite.SearchEvent (object type lucenesuite.searchevent, table LuceneSuite_SearchEvent), idempotent web-farm-safe installer (version key LuceneSuiteInstalledVersion = 1)
  - Request/response searchId contract (SearchId, Sequence, IsSubmit, TrackAnalytics; response SearchId)
  - QueryScrubber (normalize + email/number PII scrub)
  - SearchSessionCookie (Visitor/All only, 30-min sliding)
  - SearchCaptureService decorator + bounded SearchEventQueue + SearchEventWriter (BulkInsert) with SuiteSearchOptions.Analytics
  - xbk dev-only SuiteSearchDemoController (untracked test host)
affects: [02-02 interaction endpoint, 02-03 rollups & retention, v0.2 reports]

tech-stack:
  added: ["Kentico.Xperience.WebApp 31.9.1 (Suite core)", "FrameworkReference Microsoft.AspNetCore.App (Suite core + tests)", "Microsoft.Data.SqlClient (transitive, used for sp_getapplock)"]
  patterns: [decorator via TryAddSingleton, bounded Channel DropWrite + BackgroundService bulk writer, sp_getapplock-serialized installer on ApplicationEvents.Initialized, per-call resolution of request-scoped services]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite/Analytics/{AnalyticsModule.cs, AnalyticsModuleInstaller.cs, BotUserAgents.cs, QueryScrubber.cs, SearchCaptureService.cs, SearchEventInfo.cs, SearchEventQueue.cs, SearchEventWriter.cs, SearchSessionCookie.cs}, src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptions.cs, tests/XperienceCommunity.Lucene.Suite.Tests/{QueryScrubberTests.cs, SearchCaptureTests.cs, AnalyticsInstallerDatabaseTests.cs}, ../xbk/Controllers/SuiteSearchDemoController.cs (untracked)]
  modified: [src/XperienceCommunity.Lucene.Suite/Search/{SuiteSearchRequest.cs, SuiteSearchResponse.cs, SearchExecutor.cs}, src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs, src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs, Directory.Packages.props, csproj + packages.lock.json files]

key-decisions:
  - "Cookie registration via Configure<CookieLevelOptions> + CookieLevelConstants: CookieHelper.RegisterCookie and CMS.Helpers.CookieLevel are [Obsolete] in 31.9.1"
  - "Suite core depends on Kentico.Xperience.WebApp: acceptable, the core package is the live-site package"
  - "Request with SearchId but no Sequence → Sequence 1, Origin Client; no SearchId → minted, Sequence 1, IsSubmit, Origin Server"
  - "Empty User-Agent counts as a bot; empty query stored as ''; FacetFilterCount = total selected facet values"

duration: APPLY + orchestrator browser check, commit 2ee3326 at 2026-10-09 08:56 -04:00
completed: 2026-10-09
---

# Phase 2 Plan 01: Capture Summary

**Every live search through `ISuiteSearchService` now produces one identity-free row in `LuceneSuite_SearchEvent`, written in background batches through Kentico's BulkInsert. Capture cannot slow or break search: it is off the request path, drops on overflow, and swallows its own failures. All 8 ACs pass. AC-8(e) (admin query tester leaves no row) was verified by the orchestrator in the browser, not by the APPLY agent.**

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: One raw row per tracked search, off the request path | Pass | Schema (no identity columns): SearchEventID PK, SearchId, Sequence, SessionId NULL, ReceivedUtc, IndexName(200), QueryText(256) NULL, Page, PageSize, FacetFilterCount, ResultCount, LatencyMs, IsSubmit, Origin (1 Client, 2 Server). No contact id, IP, UA or hash. Unit tests: field storage; response carries SearchId. DB: demo rows (a)(b)(d) landed within the flush interval via SearchEventWriter BulkInsert |
| AC-2: searchId / sequence contract | Pass | DB row (a): client id + seq 3 stored unchanged; row (b): minted id, seq 1, IsSubmit, Origin Server. Guid.Empty / Sequence < 1 rejected by validation (unit test + HTTP 400 live) |
| AC-3: Session id only at Visitor/All | Pass | Unit tests: cookie levels, HasStarted reuse without write, no HttpContext → null, per-request resolution. Live: demo at cookie level All (channel default 1000) → SessionId set, Set-Cookie seen; CMSCookieLevel=0 → SessionId NULL, no Suite cookie. Cookie HttpOnly / Lax / Secure-on-HTTPS / 30-min sliding. Concern below on HasStarted |
| AC-4: PII scrub and normalization | Pass | QueryScrubberTests: Normalize 20 cases incl. false-positive guards ("iphone 15", "2025 calendar"), CapsAt256. DB row (d): "order for {email} {number}". `[GeneratedRegex]` with 100 ms timeout |
| AC-5: Exclusions | Pass | Unit: 8 exclusion scenarios (Enabled, TrackAnalytics, empty index, Sec-Purpose prefetch/prerender, Purpose: prefetch, bot UA, empty UA). Live: Googlebot and prefetch calls → no row. QueryTesterPage sends TrackAnalytics = false; orchestrator ran the query tester twice (empty browse + "grinder", 7 hits): row count stayed 5 → AC-8(e) |
| AC-6: Never slows or breaks search | Pass | Unit: overflow drop counted (DropWrite itemDropped), capture-failure isolation (search result unchanged), inner-search failure still propagates, truncation before mapping. Writer: failed batch logged once per minute and discarded; shutdown flush. No synchronous DB call on the search path |
| AC-7: Module class hygiene | Pass | TypeInfo: CI disabled, LogEvents / TouchCacheDependencies / SupportsInvalidation / SupportsCloning false. Installer serialized with sp_getapplock on ConnectionHelper.ConnectionString; runs on ApplicationEvents.Initialized; failure logged, startup not crashed. AnalyticsInstallerDatabaseTests: 4 concurrent + rerun → exactly 1 resource / class / version key / table / index (88/88 with LUCENESUITE_TEST_CONNECTIONSTRING set). Prefix lucenesuite.* does not collide with xpsearch.* |
| AC-8: End to end in xbk | Pass | 5 rows in LuceneSuite_SearchEvent covering (a), (b), (d) plus the extra empty-query and cookie-level-0 calls; no rows for Googlebot, prefetch or (e) query tester. Controller 404 in Production. Suite Release build 0 warnings / 0 errors (re-run at UNIFY); `dotnet test -c Release` 87 passed, 1 skipped (DB-gated installer test), 88 total; xbk scratch builds normal + SeparatedAdmin 0 errors, 279 warnings; `dotnet restore --locked-mode` OK (re-run at UNIFY) |

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build -c Release` | 0 warnings, 0 errors (UNIFY re-run) |
| `dotnet test -c Release` | 87 passed, 1 skipped, 88 total; 88/88 with DB connection string |
| Mutation: remove `\|\| !request.TrackAnalytics` | `ExcludedSearches("untracked")` fails; reverted; `git diff` clean; 87 pass |
| Mutation: remove installer lock | Not done (gap, deferred) |
| Locked-mode restore | OK |
| xbk scratch builds (normal + SeparatedAdmin) | 0 errors, 279 warnings each; xbk lock file restored to default shape |
| DB row count after 2 query-tester runs | 5 → 5 (orchestrator, browser) |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: Module, SearchEvent class, idempotent installer | Done; index IX_LuceneSuite_SearchEvent_ReceivedUtc_SearchId via TableManager.CreateIndex (deviation 2) | `2ee3326` |
| 2: Contract, scrubber, session cookie | Done (deviation 1) | `2ee3326` |
| 3: Decorator, bounded queue, bulk writer | Done; options SuiteSearchOptions.Analytics {Enabled, QueueCapacity 10000, FlushInterval 2 s, BatchSize 500} | `2ee3326` |
| 4: xbk demo endpoint + E2E | Done; (e) verified by orchestrator (deviation 3) | n/a (xbk untracked; rollback = delete SuiteSearchDemoController.cs) |

## Tests Added (53 cases)

| File | Covers |
|------|--------|
| QueryScrubberTests.cs | Normalize (20 cases incl. false-positive guards), CapsAt256 |
| SearchCaptureTests.cs | Field storage, minting, scrub, truncation, 8 exclusion scenarios, overflow drop, capture-failure isolation, inner-failure propagation, validation, session cookie (levels, HasStarted, no context, per-request) |
| AnalyticsInstallerDatabaseTests.cs | Concurrent install (4 parallel + rerun) → single resource/class/key/table/index; skipped unless LUCENESUITE_TEST_CONNECTIONSTRING is set |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Cookie registered with `Configure<CookieLevelOptions>` + `CookieLevelConstants` instead of `CookieHelper.RegisterCookie` / `CMS.Helpers.CookieLevel`; cookie written via `Response.Cookies` with an explicit level check. Added Kentico.Xperience.WebApp 31.9.1 to Suite core, FrameworkReference Microsoft.AspNetCore.App to core + tests | The planned APIs are `[Obsolete]` in 31.9.1. (RegisterCookie's bool = isSensitive, recorded as asked; unused) | Suite core now depends on the live-site WebApp package (PROJECT Tech Stack updated) |
| 2 | `TableManager.CreateIndex` ascending flag is an undocumented tuple bool (true = ascending). First install produced a DESC index; dropped by hand on the dev DB and fixed | Undocumented API | Index now ascending; dev DB corrected manually |
| 3 | AC-8(e) not exercised by the APPLY agent (admin sign-in needed) | Agent cannot sign in | Orchestrator verified it in the browser: count 5 → 5 after 2 query-tester runs |

Interpretations (not in the plan text): SearchId without Sequence → Sequence 1, Origin Client; FacetFilterCount = total selected values; empty UA = bot; '' for an empty query; scrubber also replaces 10+ space-separated digits and `+` followed by 7 digits.

## Concerns

| Concern | Detail |
|---------|--------|
| HasStarted + GetCurrentCookieLevel | When `Response.HasStarted`, Kentico's `GetCurrentCookieLevel` (documented to set the CMSCookieLevel cookie) is still called. Only tested with a fake provider. Any throw is caught by the capture try/catch: the event is lost, search is unaffected |

## Key Patterns / Decisions

- Decorator registered with TryAddSingleton; request-scoped services (cookie level, HttpContext) resolved per call, never captured.
- Bounded Channel (DropWrite) + BackgroundService writer is the pattern for 02-02 interaction events.
- sp_getapplock-serialized, add-only installer with a version settings key; migrations start when the schema first changes.
- KNOWN-LIMITATIONS appended (project-sidecar/docs/KNOWN-LIMITATIONS.md): naive bot heuristic; NTP drift tolerance; digit-run PII gaps (7–9 digits space-separated); add-only installer, no migrations yet.

## Skill Audit

Skill audit: All required skills invoked ✓
- `/dotnet:csharp-refactoring`: invoked.
- `/kentico-web-development:content-retrieval`: invoked (module-class data access).
- `/dotnet:msbuild` (optional): not recorded.

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| HasStarted path still calls Kentico GetCurrentCookieLevel (may try to set CMSCookieLevel); verify against the real provider | 02-02 (beacon path shares the cookie code) |
| Installer lock-removal mutation not run (concurrency test not proven to fail without sp_getapplock) | 02-03 (installer gains rollup tables) |
| Audit deferrals: language/channel dimensions; clock drift beyond NTP; unit-level DB writer test | When the search API carries them / KNOWN-LIMITATIONS / covered E2E |
| Open product question: Suite search widget/page for real traffic in v0.1? | Unanswered; user decision |

## Next

Phase 2 progress: 1/3 plans. Next: `/paul:plan` 02-02 Interaction endpoint (click/conversion beacon, rate/bot filter, client contract).

---
*Completed: 2026-10-09*
