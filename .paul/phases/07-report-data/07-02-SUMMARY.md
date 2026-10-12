---
phase: 07-report-data
plan: 02
subsystem: testing
tags: [seed-data, sqlbulkcopy, lucene-net, rollup-engine, dev-tooling]

requires:
  - phase: 07-report-data
    provides: SearchReportService (verifies the seeded data)
  - phase: 02-event-capture
    provides: RollupEngine, RollupLock, SearchDaily schema, watermark key
provides:
  - Dev-only seed runner (tests/Seeding) with range-based clear
  - 60 days of demo history on the CommunityProjects host (DancingGoatSample)
affects: [08-reports-admin, 09-suggestions]

tech-stack:
  added: []
  patterns:
    - "Kentico-free writes to a live host DB: SqlBulkCopy in a serializable transaction under the farm-wide RollupLock"
    - "Destructive dev runners require LUCENESUITE_SEED_CONFIRM = target database name"

key-files:
  created:
    - tests/XperienceCommunity.Lucene.Suite.Tests/Seeding/SearchSeeder.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/Seeding/SeedVocabulary.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/Seeding/SeedRunnerTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/Seeding/SearchSeederTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/Seeding/SearchSeederDatabaseTests.cs
  modified:
    - docs/search-reports.md
    - .paul/APPROVAL-CHECKS.md

key-decisions:
  - "Seeded texts normalized (lowercase, single-spaced, trimmed, ≤ 100 chars): SearchDaily's unique index is case-insensitive and ignores trailing spaces, while the engine compares ordinally"
  - "Host seeded 2026-08-12..2026-10-10 (3,477 rows); removal command recorded in APPROVAL-CHECKS #4"

patterns-established:
  - "Seeded/synthetic data goes only into days no real process can own (before min(today, watermark+1, earliest raw, first daily))"

duration: ~50min
completed: 2026-10-12T00:00:00Z
description: "Dev-only seed runner: contract-faithful synthetic searches from index titles → real RollupEngine → SearchDaily on never-rolled empty days (lock, confirm, range clear); host seeded with 60 days / 7,725 searches"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 7 Plan 02: Seed Generator Summary

**An opt-in test runner generates realistic search traffic from an index's titles and rolls it with the real engine. It writes the result only into empty days the rollup job will never touch. The CommunityProjects host now has 60 days (2026-08-12..2026-10-10) of report history.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~50 min |
| Tasks | 3 of 3 completed |
| Files | 5 created, 2 modified (+ STATE, paul.json, KNOWN-LIMITATIONS) |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Contract-faithful generation through the real engine | Pass | Same seed gives identical KPIs. Rolled Searches per day = generated logical searches. No results 5–25 %, CTR 15–45 %. Clicks stay on the returned page and never follow 0 results. Weekday > weekend. Long tail: some queries surface, others don't. |
| AC-2: Vocabulary from the index (or built-in) | Pass | FromIndex reads the newest `i-g*` generation and skips `_taxonomy` and older generations (temp-index test). Built-in list with case-insensitive Match. The host gave 39 documents and 114 texts. |
| AC-3: Safe target range, write and clear | Pass | Range limits (today, watermark + 1, earliest raw day, first daily day) tested one by one. Missing or mismatched confirm refused. A held rollup lock refuses and writes nothing. Rows outside the range refused and nothing written. Watermark, raw rows and other indexes untouched. Clear is exact, including a NULL "(other)" row. Runner prints range, seed and clear value. |
| AC-4: Reports work on seeded data | Pass | SearchReportService on 60 seeded test-DB days: non-zero Searches, CTR and no-result rate; 60 trend days; named rows plus an Other row; p50 < p95 (or overflow). |

## Verification Results

- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test --filter FullyQualifiedName~Seeding` with the test DB → 13 passed, 1 skipped (runner without its variable).
- Full suite with the test DB → 621 passed, 5 skipped, 1 failed. The failure is `RulesOverhead_StaysUnderTenMillisecondsAtP95` at 10.19 ms; the same test run alone gave 9.75 / 10.19 / 8.92 ms. It is pre-existing and unrelated; see Issues.
- Host run: `Seeded 'DancingGoatSample' 2026-08-12..2026-10-10: 3477 SearchDaily rows from 39 documents / 114 texts, 150 searches per weekday, random seed 2026.`
- Host SQL after the run:
  - 3,477 rows across 60 distinct days, 0 rows outside the range.
  - 7,725 searches, 1,031 with no results (13 %), 2,335 clicked (30 %), 203 conversions.
  - Watermark still empty; raw rows still 17.

## Accomplishments

- Phase 8 report screens can be built and checked against 60 days of plausible host history today.
- Seeding is safe on a running host:
  - no Kentico initialization against the target;
  - the rollup lock is held;
  - the range is re-read in a serializable transaction;
  - the target database name must be confirmed.
- The data matches the client contract by construction, because the real `RollupEngine` produces every stored number.

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Tasks 1–3 | `d3c2661` | feat | Seed runner, tests, docs, host seeded, APPROVAL-CHECKS #4 |

Plan metadata: `2cc0722` (docs: plan + audit)

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Seeding/SearchSeeder.cs` | Created | Generation, per-day roll, range, guarded SqlBulkCopy write, range clear |
| `Seeding/SeedVocabulary.cs` | Created | Read-only index title reader, built-in catalogue, text normalization, Match |
| `Seeding/SeedRunnerTests.cs` | Created | `[SeedFact]` runner driven by `LUCENESUITE_SEED_*` variables |
| `Seeding/SearchSeederTests.cs` | Created | 8 unit tests (generation fidelity, index reader, confirm) |
| `Seeding/SearchSeederDatabaseTests.cs` | Created | 5 DB tests (range, write isolation + reports, out-of-range refusal, lock, clear) |
| `docs/search-reports.md` | Modified | "Seeding demo history" section |
| `.paul/APPROVAL-CHECKS.md` | Modified | Entry #4 (host seeding, clear command, decisions to confirm) |

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Normalize every seeded text | The unique index on (Day, IndexName, QueryText) is case-insensitive and ignores trailing spaces; un-normalized texts could collide on insert | Seeded queries are lowercase |
| Searches start 00:10–23:00 UTC, and each day is rolled from that day's rows only | Keeps every logical search and its attribution inside its day | Simpler roll; no cross-midnight seeds (the engine's cross-midnight logic is covered by its own tests) |
| Defaults: 150 searches per weekday, random seed 2026 | Enough volume for a long tail over 60 days | Overridable per run |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 1 | Text normalization (collision safety) |
| Scope changes | 1 | One DB test replaced (see below) |
| Deferred | 0 new | Audit deferrals in KNOWN-LIMITATIONS |

### Details

**1. "Refuses a non-empty range" test replaced.**
- The range always ends before the index's first SearchDaily day, so the seeder itself can never produce a non-empty range. The in-transaction count check stays as a guard against concurrent writers.
- The DB test instead covers the reachable refusal: generated rows outside the range are rejected and nothing is written.

**2. Index-reader test moved to the unit test file.** It needs no database.

**3. Typing gap.** The plan said 2–5 s; the code uses min(2 s, TextChangeWindow / 5) × 1–2.5, which equals 2–5 s with the defaults and stays inside a shorter configured window.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| `BaseDocumentProperties` lives in `Kentico.Xperience.Lucene.Core`, not `.Indexing` | Fixed the using |
| `DatabaseFact` tests silently skipped when the variable wasn't exported in the shell | Re-ran with the variable set; note for future runs |
| Rules-overhead perf test (p95 < 10 ms) now borderline even when run alone (8.92–10.19 ms) with the host and admin dev server running | No Suite source changed in this plan. Logged as recurring in STATE Deferred Issues: decide on a CI budget or a quieter measurement. |

## Skill Audit

All required skills invoked ✓ (/dotnet:csharp-refactoring, /kentico-web-development:content-retrieval).

## KNOWN-LIMITATIONS added

- Clear trusts the operator's range (no marker on seeded rows).
- Seeded history counts toward the surfacing of real queries with the same text.
- Result counts are title substrings, the vocabulary mixes languages, and titles are read once at seed time.

## Next Phase Readiness

**Ready:**
- Phase 8 (Reports Admin) has `SearchReportService` plus 60 days of host data covering surfaced queries, (other), zero-result queries, conversions and the latency tail.

**Concerns:**
- APPROVAL-CHECKS #4: keep or clear the seeded host history, and accept its effect on real-query surfacing.
- The real rollup job on the host has not rolled a day yet (watermark empty since the 06-03 traffic). It should roll 2026-10-11 on its next tick; Phase 8 should confirm that.
- Rules-overhead perf test flakiness.

**Blockers:** None

---
*Phase: 07-report-data, Plan: 02*
*Completed: 2026-10-12*
