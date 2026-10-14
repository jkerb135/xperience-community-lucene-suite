---
phase: 14-item-popularity-data
plan: 01
subsystem: analytics
tags: [rollup, analytics, kentico-module-class, installer, seed]

requires:
  - phase: 02-event-capture
    provides: rollup engine, job, watermark, retention, interaction capture
  - phase: 07-report-data
    provides: dev-only seed runner
provides:
  - LuceneSuite.ItemDaily (schema v8): ClickedSearches, ClickPositionSum, Conversions per day/index/item
  - Rollup job gate installed != Version (stands down on a newer schema)
  - Seeded item history on the host (2026-08-12..2026-10-10)
affects: [14-02 Top clicked items card, 19 Popularity Boost]

tech-stack:
  added: []
  patterns:
    - "New rollup outputs are counted inside the existing attribution pass and written in the same transaction, guarded by a sum invariant against SearchDaily"
    - "Background jobs run only on the exact schema version they were built for"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Analytics/ItemDailyInfo.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/ItemKpi.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite/Analytics/AnalyticsModuleInstaller.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/RollupEngine.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/DailyRollupStore.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/AnalyticsRetention.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/AnalyticsRollupJob.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/Seeding/SearchSeeder.cs

key-decisions:
  - "User: clicked searches + conversions per item (raw repeat clicks not stored: the engine counts an item once per logical search, as every report does); click-position sum added at no cost"
  - "User: start fresh at v8 (no backfill); seed runner seeds items; report card in 14-02"
  - "Audit: job gate installed != Version"

duration: ~1.5h (implementation delegated; host upgrade and re-seed by me)
completed: 2026-10-13T00:00:00Z
description: "Per-item daily popularity (LuceneSuite.ItemDaily, schema v8) recorded by the existing rollup in the same pass and transaction as SearchDaily, with seeded demo history and a version gate that stops older nodes from rolling against a newer schema"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 14 Plan 01: Item Popularity Data Summary

**From now on, every rolled day records how often each item was clicked and converted per index, consistent with the existing reports.** The 60 seeded demo days carry the same history.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Schema v8 | Pass | Host installer reached v8; class + unique index (Day, IndexName, ItemGuid) verified by DB test; gate stands down on a newer DB (warning hourly) and waits on an older one (tests) |
| AC-2: Item rows from the same pass | Pass | Counted inside the existing click/conversion branches; invariant tests per index; shared vectors file unchanged; deterministic order |
| AC-3: Same transaction and retention | Pass | One CMSTransactionScope (failure test for both inserts); batched retention with the same cutoff; no backfill (KNOWN-LIMITATIONS) |
| AC-4: Seeded history | Pass | Host re-seed: identical totals (7,725 / 2,344 / 203), 1,027 item rows, invariant 0 mismatching days; real days untouched |
| AC-5: Identity-free, within budget | Pass | Day, index, item and counts only; no search-path change; Debug + Release 0 warnings; 868 .NET passed / 6 skipped; client 103/103; Widgets JS 45/45 |

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Task 1–2 (code) | `65dc845` | feat | ItemDaily, v8, engine, store, retention, gate, seeder, tests |
| Task 2 (docs) | `4f4b212` | docs | Retention and seed runner docs |
| Task 3 | (UNIFY commit) | docs | APPROVAL-CHECKS #22 |

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Spec clarification | 1 | "Clicks" and "clicked searches" are one number per item; stored as ClickedSearches + ClickPositionSum + Conversions (recorded before APPLY) |
| Extra docs file | 1 | docs/search-reports.md seeder lines updated (two lines) |
| Host restart | 1 | The user's running host was stopped to load v8; the first restart timed out in Kentico's startup update; the second started cleanly |
| Folded rows | 1 | Re-seeded days came back unfolded (3,477 rows vs 3,386, same totals); retention will refold |

## Next Phase Readiness

**Ready:** 14-02 can read ItemDaily for any range (title lookup via the existing SuiteItemLookup/Describe path).

**Concerns:** item history before 2026-10-13 exists only for the seeded range; real history starts at the next rolled day.

**Blockers:** None

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Session |
| /kentico-web-development:content-retrieval | ✓ | Session (ObjectQuery/bulk on the new class) |
| /update-docs | ✓ | Docs commit `4f4b212` (phase-end docs in 14-02) |

---
*Phase: 14-item-popularity-data, Plan: 01*
*Completed: 2026-10-13*
