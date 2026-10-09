---
phase: 02-event-capture
plan: 02
subsystem: analytics
tags: [module-class, installer-v2, sp_getapplock, interaction-logger, client-contract, test-vectors, cookie-level]

requires: [02-01 capture pipeline (queue, writer, installer v1, session cookie, exclusions)]
provides:
  - Class LuceneSuite.SearchInteraction (object type lucenesuite.searchinteraction, table LuceneSuite_SearchInteraction), installer v2 (LuceneSuiteInstalledVersion = 2), additive v1 → v2 step, downgrade guard
  - Public ISearchInteractionLogger (LogClick / LogConversion), SearchInteractionInfo, enum SearchInteractionType
  - Shared internal AnalyticsRequestFilter (bot / prefetch / Enabled) used by search capture and interactions
  - docs/analytics-client-contract.md + docs/analytics-test-vectors.json (17 vectors) with an executable reference mint function (TestVectorTests)
  - xbk dev-only click / conversion / click-after-start / click-on-completed demo actions (untracked)
affects: [02-03 rollups & retention (reads both tables, reuses the vectors), Phase 6 Widgets package (endpoint + JS client over the logger and vectors), v0.2 CTR/conversion reports]

tech-stack:
  added: []
  patterns: [one bounded queue + writer carrying two event kinds, synchronous argument validation + never-throwing storage, version-gated additive installer steps under the existing applock, disposable DB copy for destructive tests]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite/Analytics/{SearchInteractionInfo.cs, ISearchInteractionLogger.cs, SearchInteractionLogger.cs, AnalyticsRequestFilter.cs}, docs/{analytics-client-contract.md, analytics-test-vectors.json}, tests/XperienceCommunity.Lucene.Suite.Tests/{SearchInteractionLoggerTests.cs, TestVectorTests.cs}]
  modified: [src/XperienceCommunity.Lucene.Suite/Analytics/{AnalyticsModuleInstaller.cs, SearchCaptureService.cs, SearchEventQueue.cs, SearchEventWriter.cs, SearchSessionCookie.cs}, src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs, tests/XperienceCommunity.Lucene.Suite.Tests/{AnalyticsInstallerDatabaseTests.cs, SearchCaptureTests.cs, XperienceCommunity.Lucene.Suite.Tests.csproj}, ../xbk/Controllers/SuiteSearchDemoController.cs (untracked)]

key-decisions:
  - "Validation errors (Guid.Empty, sequence < 1, click position < 1 or > SearchExecutor.MaxResultWindow) throw synchronously; storage problems never throw"
  - "AC-4: Kentico's real cookie-level provider does not write CMSCookieLevel on a started response; SearchSessionCookie logic kept, one-line invariant comment added"
  - "Mint rules follow the plan's literal rules where research prose disagrees (2 cases, pending user confirmation)"

duration: APPLY → commit d127aff at 2026-10-09 09:39 -04:00
completed: 2026-10-09
---

# Phase 2 Plan 02: Interactions Summary

**Developer code can now record identity-free clicks and conversions with one injected service, `ISearchInteractionLogger`, through the 02-01 background pipeline into `LuceneSuite_SearchInteraction`. The client contract exists as prose and as 17 executable test vectors. All 7 ACs pass. The 02-01 lock-mutation gap and the HasStarted cookie concern are both closed. Two places where the plan's literal mint rules contradict the research prose await user confirmation.**

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Logger API | Pass | Schema: SearchInteractionID PK, SearchId, Sequence, ItemGuid, Position NULL, Type (1 Click, 2 Conversion), EventId, SessionId NULL, ReceivedUtc; no contact id, IP, UA or hash. Unit tests: field mapping, EventId minting, storage-failure isolation. DB: interaction 1 (click, position 1, client EventId) and 2 (conversion, Position NULL, minted EventId) linked to search event 6 by SearchId/Sequence |
| AC-2: Validation at the trust boundary | Pass | Throws for Guid.Empty searchId/itemGuid/eventId, sequence < 1, click position < 1 or > `SearchExecutor.MaxResultWindow` (constant reused). Mutation: removing position validation → 3 tests fail; reverted; diff clean. Live: position 0 → HTTP 400 |
| AC-3: Exclusions and session parity | Pass | Shared AnalyticsRequestFilter extracted behaviour-neutrally (the 87 original non-DB tests still pass; harness changed only a ctor arg + cast). Unit tests: bot, prefetch, disabled, no HttpContext → SessionId null. Live: Googlebot click → 204, no row; rows 3–10 (AC-4 diagnostics) have SessionId null exactly where expected (level 0; response started without a cookie) |
| AC-4: Deferred cookie-level concern | Pass | Kentico buffers MVC responses (HasStarted stayed false inside actions), so tested in `Response.OnCompleted` (HasStarted true) against the real provider: returned normally for level 1000, 1000 with an existing Suite cookie, and 0; no CMSCookieLevel Set-Cookie emitted. Logic kept; one-line invariant comment; KNOWN-LIMITATIONS entry |
| AC-5: Installer v2 and lock proof | Pass | On disposable copy `xperience_suitetest` (COPY_ONLY backup restored WITH MOVE): v1 → v2 leaves SearchEvent definition and rows untouched; fresh 4-concurrent + rerun → exactly one of each object; installed 3 vs code 2 → warning, no change. Lock-removal mutation: concurrency test failed 3/3 (Kentico in-process connection errors, not the duplicate asserts); restored; diff clean. DB tests skip unless the DB name contains "test". Copy dropped, .bak deleted; no *suitetest* database remains (orchestrator) |
| AC-6: Client contract and test vectors | Pass | docs/analytics-client-contract.md covers minting (30 s, submit, empty transitions, typo tolerance, deep backspace), Sequence, IsSubmit, TrackAnalytics=false, page/filter/sort keep the id, logger calls, 1 h attribution, developer-owned rate limiting, cross-page conversion via ids on result links, stable EventIds. 17 vectors; TestVectorTests 22 cases incl. OSA distance. Non-vacuity: raising the 4-char typo floor to 5 fails "4 characters: one edit continues" |
| AC-7: End to end in xbk | Pass | Click 204, conversion 204, position 0 → 400, Googlebot → 204 with no row. DB `xperience`: search event 6 (CF75B2CD…, seq 2, "coffee", 28 results, Origin Client, session set) + interactions 1–2 as above. Suite Release 0/0 (re-run at UNIFY); tests 133 passed, 3 DB-gated skipped, 136 total (orchestrator); xbk scratch builds normal + SeparatedAdmin 0 errors / 279 warnings; Suite.Admin.dll absent in SeparatedAdmin; xbk lock file restored |

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build -c Release` | 0 warnings, 0 errors (UNIFY re-run) |
| `dotnet test -c Release` | 133 passed, 3 skipped (DB-gated), 136 total |
| DB-gated tests on `xperience_suitetest` | Pass (upgrade, idempotent rerun, concurrency, downgrade no-op) |
| Mutation: remove sp_getapplock | Concurrency test fails 3/3; restored; `git diff` clean |
| Mutation: skip position validation | 3 tests fail; reverted; `git diff` clean |
| Mutation: typo floor 4 → 5 (reference mint function) | Vector "4 characters: one edit continues" fails; reverted |
| xbk scratch builds (normal + SeparatedAdmin) | 0 errors, 279 warnings each |
| DB `xperience` after APPLY | 6 search events, 10 interactions, LuceneSuiteInstalledVersion = 2 (orchestrator) |
| Writes to `xperience` | Only SELECTs, the BACKUP, and the installer's additive v1 → v2 step; all DROP/DELETE ran on the copy |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: SearchInteraction class + installer v2 + lock mutation proof | Done (deviations 1, 2) | `d127aff` |
| 2: ISearchInteractionLogger through the existing pipeline | Done; queue/writer carry both kinds, drop warning reports searches and interactions separately | `d127aff` |
| 3: Client contract, test vectors, xbk end to end | Done (deviations 3, 4) | `d127aff` (xbk untracked; 02-01 controller backed up in scratch as SuiteSearchDemoController.cs.02-01.bak) |

## Tests Added

| File | Covers |
|------|--------|
| SearchInteractionLoggerTests.cs | Field mapping, EventId minting, validation cases, exclusions, overflow, storage-failure isolation, no-HttpContext path |
| TestVectorTests.cs | Reference mint-decision function vs all 17 vectors (22 cases incl. OSA distance) |
| AnalyticsInstallerDatabaseTests.cs (extended) | v1 → v2 upgrade, idempotent rerun, 4-way concurrency, newer-installed-version no-op; guarded to DB names containing "test" |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Backup written to SQL Server's own Backup folder, not the scratch folder | SQL service account got OS error 5 on scratch; folder permissions deliberately not changed | .bak deleted afterwards; none |
| 2 | DB test reset uses raw SQL + `ProviderHelper.ClearHashtables` instead of Kentico deletes | Kentico refuses to delete classes whose Info type is compiled in | Test-only; runs only on a DB whose name contains "test" |
| 3 | AC-4 tested in `Response.OnCompleted`, not after writing the body in an action | Kentico buffers MVC responses; HasStarted stayed false inside actions | Real started-response path exercised |
| 4 | Test csproj links docs/analytics-test-vectors.json | Tests load the shared file; csproj not in the plan's file list | One-line link |
| 5 | First xbk start used an invalid flag; app exited after its installer had already run the additive v1 → v2 step on `xperience` | Operator error | Additive only, same step AC-7 required |

## Pending User Confirmation (spec conflicts)

Both followed the plan's literal rules; flagged in the vectors, contract and KNOWN-LIMITATIONS. Not blockers for 02-03 planning.

| # | Case | Research prose | Literal rule (implemented) |
|---|------|----------------|----------------------------|
| 1 | "ipad" → "ipod" | Two searches | One search (≤ 1 edit at ≥ 4 chars). The orchestrator's earlier chat message to the user also wrongly said "two" |
| 2 | "coffee grinder" → "coffee " → "coffee beans" | One search | Two searches (deep-backspace rule on trimmed input) |

## Key Patterns / Decisions

- One bounded queue + one writer for both event kinds, each written with its provider's BulkInsert; overflow and failure handling identical to 02-01.
- Version-gated additive installer steps inside the existing applock transaction; installed > code version → warning, no change.
- Destructive DB tests only on a disposable copy, enforced by a DB-name guard.
- KNOWN-LIMITATIONS appended (project-sidecar/docs/KNOWN-LIMITATIONS.md): installer re-runs ensure steps (no versioned migrations); single-process concurrency test; shared queue capacity; session-after-start relies on observed Kentico behaviour; mint rules vs research prose.

## Skill Audit

Skill audit: All required skills invoked ✓
- `/dotnet:csharp-refactoring`: invoked.
- `/kentico-web-development:content-retrieval`: invoked.

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| Two mint-rule conflicts (above) | User decision; adjust vectors + contract before the Widgets JS client (Phase 6) or 02-03 merge logic depends on them |
| Audit deferrals: shared queue capacity starvation; direct CMSCookieLevel read (not needed, real provider safe); rate limiting for custom endpoints (developer-owned, Phase 6) | Phase 6 traffic / KNOWN-LIMITATIONS |
| Resolved from 02-01: HasStarted cookie-level concern (AC-4); lock-removal mutation (AC-5) | Closed |

## Next

Phase 2 progress: 2/3 plans. Next: `/paul:plan` 02-03 Rollups & retention.

---
*Completed: 2026-10-09*
