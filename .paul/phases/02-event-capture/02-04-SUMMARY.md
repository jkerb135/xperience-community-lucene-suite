---
phase: 02-event-capture
plan: 04
subsystem: analytics
tags: [hosted-service, sp_getapplock, watermark, cms-transaction-scope, bulk-delete, bulk-insert, retention, fold, collation, volume, e2e]

requires: [02-01 SearchEvent capture, 02-02 SearchInteraction, 02-03 options + SearchDaily (installer v3) + pure RollupEngine with input contract]
provides:
  - AnalyticsRollupJob (BackgroundService, registered by AddLuceneSuite): 1 min initial delay via TimeProvider, PeriodicTimer(RollupInterval), per-tick try/catch with errors logged at most once per hour, exits when Analytics.Enabled = false
  - RollupLock: non-blocking session sp_getapplock "LuceneSuite.Rollup" (@LockTimeout = 0) on a dedicated Pooling=false connection; helper shared with the installer (AnalyticsModuleInstaller.GetAppLock)
  - RollupScheduler: pure day selection (eligibility, MaxDaysPerRun, completeness floor with SkipThrough)
  - DailyRollupStore: input-contract loads, uncached watermark read, BulkDelete → watermark → BulkInsert in one CMSTransactionScope, collation-aware query-text merge
  - AnalyticsRetention: raw purge tied to the next day's input window, RawHardLimit ceiling, SearchDaily purge, batched deletes, sub-threshold fold into per-day (other) rows
  - Settings LuceneSuite:Analytics:Job:MaxDaysPerRun (7) and LuceneSuite:Analytics:Retention:RawHardLimit (7 days); hidden settings key LuceneSuiteRollupWatermark (installer, version stays 3)
affects: [v0.2 reports (read SearchDaily incl. (other) rows; SessionCount of (other) / merged texts is an upper bound), Phase 6 Widgets (real traffic now rolls and expires), xbk (its DB is subject to retention when it next runs this code)]

tech-stack:
  added: []
  patterns: [single-runner hosted job via session applock on an unpooled connection, pure scheduler with injected TimeProvider, watermark in a hidden settings key read uncached inside the lock, Kentico bulk ops enlisted in CMSTransactionScope, batched TOP-n id deletes, totals-preserving fold]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/{AnalyticsRollupJob.cs, RollupScheduler.cs, RollupLock.cs, DailyRollupStore.cs, AnalyticsRetention.cs}, tests/XperienceCommunity.Lucene.Suite.Tests/{RollupSchedulerTests.cs, RollupJobDatabaseTests.cs, RetentionDatabaseTests.cs, RollupVolumeTests.cs}]
  modified: [src/XperienceCommunity.Lucene.Suite/Analytics/{AnalyticsModuleInstaller.cs, SearchEventInfo.cs, SearchInteractionInfo.cs}, src/XperienceCommunity.Lucene.Suite/Search/{SuiteSearchOptions.cs, SuiteSearchOptionsValidator.cs}, src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs, docs/analytics-client-contract.md, tests/XperienceCommunity.Lucene.Suite.Tests/{AnalyticsInstallerDatabaseTests.cs, AnalyticsOptionsTests.cs}]

key-decisions:
  - "RawRetention minimum tightened to 1 day + 3 × AttributionWindow + FlushInterval + 5 min + RollupInterval + 1 h so the completeness floor can never skip every day"
  - "Watermark = hidden settings key, read by direct uncached ObjectQuery inside the lock, written via IInfoProvider<SettingsKeyInfo>.Set inside the day's transaction"
  - "Kentico BulkDelete/BulkInsert enlist in CMSTransactionScope (proven by test) — no plain SQL transaction needed"
  - "Query texts equal under the column collation (e.g. straße = strasse under Latin1_General_CI_AS) are merged in one window-function query (BIN2 tiebreak) so the unique index cannot reject a day"
  - "Volume within budget (engine 0.63 s / 95 MB; store 6.42 s / 358 MB) → whole-day-in-memory kept, no streaming"

duration: APPLY → commit 18add8d at 2026-10-09 13:17 -04:00
completed: 2026-10-09
---

# Phase 2 Plan 04: Job & Retention Summary

**Raw events now become SearchDaily KPIs once per completed UTC day on any number of nodes. A hosted job holds a non-blocking applock, rolls days in order behind a transactional watermark, enforces retention without deleting rows a future rollup needs, and folds rare queries into per-day "(other)" rows with totals unchanged. All 8 ACs pass on a disposable copy. Two findings matter beyond this plan: the 02-03 RawRetention minimum was too loose (tightened), and the SessionId/Position getters had silently returned null for database values (fixed).**

Evidence attribution: **[O]** = orchestrator-verified after APPLY (commit, clean tree, `dotnet test -c Release` counts, no *suitetest* DB left). **[A]** = APPLY report (DB tests on the copy, mutations, volume, xbk builds); not re-run at UNIFY because UNIFY does not touch databases or start xbk.

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Single runner, never blocks startup | Pass | [A] BackgroundService with 1 min initial delay (TimeProvider), PeriodicTimer(RollupInterval), per-run try/catch, errors logged ≤ once/hour, returns immediately when Enabled = false. RollupLock: session sp_getapplock "LuceneSuite.Rollup", @LockTimeout = 0, own Pooling=false connection (released on dispose or connection drop); helper extracted from the installer (AnalyticsModuleInstaller.GetAppLock). Concurrent-tick DB test: one rolls, the other skips without error. Mutation "lock timeout counted as acquired" failed the expected tests |
| AC-2: Day selection | Pass | [A] RollupScheduler pure over (watermark, earliest raw day, now, settings): eligible when now ≥ D + 1 day + 2W + FlushInterval + 5 min; no watermark → earliest raw day; ≤ MaxDaysPerRun ascending; completeness floor ceil(now − RawRetention + W) — earlier days returned as SkipThrough (watermark advances, one error logged, existing KPIs untouched). Installed version < 3 → RunOnce returns. Watermark read uncached (direct ObjectQuery) inside the lock, written inside the day's transaction. RollupSchedulerTests (8): first run, catch-up capped, exact boundary, nothing eligible, reset/stale watermark; validator tests for MaxDaysPerRun ≥ 1 and RawHardLimit ≥ RawRetention. Mutation "remove 2W term" failed a boundary test |
| AC-3: Idempotent day persistence | Pass | [A] Loads per contract (WhereIn subquery on SearchId, projected columns, GetEnumerableTypedResult streaming); one CMSTransactionScope: BulkDelete(Day = D, incl. (other)) → watermark write → BulkInsert → Commit. CmsTransactionScope_RollsBackBulkDeleteAndBulkInsert passed (bulk ops enlist — audit deferral 2 resolved). Roll twice → identical rows; failure atomicity (duplicate KPI forces failure) → previous rows + watermark intact. Mutation "watermark write before the transaction" failed the atomicity test. Late rows after roll not counted (KNOWN-LIMITATIONS) |
| AC-4: Retention ordering | Pass | [A] Raw searches cut at min(now − RawRetention, M + 1 day − W), interactions at min(now − RawRetention, M + 1 day − 2W); no normal raw purge without a watermark. RawHardLimit purges older than now − RawHardLimit regardless, error names the stuck day (DB test: stuck watermark → old rows purged, newer kept). SearchDaily < today − DailyRetention purged. Batched: TOP n ids then BulkDelete by id. Mutation "drop −W from raw cut-off" failed the survive test |
| AC-5: Sub-threshold fold into (other) | Pass | [A] Candidates via GROUP BY/HAVING (QueryText not '' / NULL, MAX(Day) < today − SubThresholdPruneAfter, SUM(Searches) < MinSearches OR COUNT(*) < MinDistinctDays); transactional batches ≤ min(BatchSize, 1000) groups; all count columns + ClickPositionSum + 10 latency buckets + SessionCount added to the day's NULL row; originals deleted. All 19 column totals per day identical before/after; browse ('') kept; existing (other) absorbs (3 + 2 = 5); second run no-op |
| AC-6: End to end on a disposable copy | Pass | [A] EndToEnd_TwoNodesThreeDays_JobTicksDayByDay (2030-03-01..03; two nodes, duplicates, cross-midnight search, clicks in/out of window, duplicate EventIds, conversions; injected clock ticks): watermark advances; unfolded rows equal engine output; ≥ 1 (other) row; per-day totals equal; raw rows before 2030-03-02 03:00 gone, later kept. `xperience`: only SELECTs (installed version already 3) and one COPY_ONLY BACKUP; copy dropped (SINGLE_USER then DROP), .bak deleted. [O] no *suitetest* database remains |
| AC-7: Volume measured | Pass | [A] Opt-in (LUCENESUITE_VOLUME_TEST=1); 100,000 search rows (25,000 searches × 4 over 5,000 queries) + 20,000 interactions: engine 0.63 s / 95 MB allocated / 69 MB peak heap / 4,970 KPI rows; store load + engine + replace 6.42 s / 358 MB allocated / 191 MB peak heap (~320 MB working set); setup bulk insert 10.0 s. Under the 30 s / 500 MB trigger → no streaming. Recorded in KNOWN-LIMITATIONS (~10× headroom) |
| AC-8: No regressions | Pass | [O] commit 18add8d on main, tree clean; `dotnet test -c Release` 233 passed, 21 skipped (19 DB-gated + 2 opt-in volume), 254 total. [A] 254/254 on the copy with volume on; new DB tests green 3 consecutive runs; xbk scratch builds normal + SeparatedAdmin 0 errors / 279 warnings; Suite.Admin.dll absent in SeparatedAdmin; xbk lock file byte-identical |

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet test -c Release` | [O] 233 passed, 21 skipped, 254 total (228 → 254) |
| DB-gated + volume tests on disposable copy | [A] 254/254; new DB tests 3 consecutive runs |
| Mutations (each failed expected tests, reverted, clean diff) | [A] 2W eligibility term removed; watermark write moved before the transaction; −W dropped from raw cut-off; lock timeout counted as acquired |
| xbk scratch builds (normal + SeparatedAdmin) | [A] 0 errors, 279 warnings each |
| Writes to `xperience` | [A] None: SELECTs + one COPY_ONLY BACKUP; copy and .bak removed. [O] no *suitetest* DB |
| Git | [O] 18add8d on main, clean, not pushed |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: Scheduler, lock, hosted job, settings | Done (deviation 1) | `18add8d` |
| 2: Day store, transactional replace, watermark | Done (deviations 2, 3) | `18add8d` |
| 3: Retention + (other) fold | Done | `18add8d` |
| 4: End to end + volume | Done (no streaming needed) | `18add8d` |

## Tests Added

| File | Covers |
|------|--------|
| RollupSchedulerTests.cs (8) | Eligibility boundary, first run, catch-up cap, nothing eligible, completeness floor / reset watermark |
| RollupJobDatabaseTests.cs (9) | Transaction enlistment, idempotency, failure atomicity, lock, concurrent ticks, reset watermark, version gate, collation merge, 3-day two-node E2E |
| RetentionDatabaseTests.cs (5) | Next-day window survives, hard limit, daily purge, fold totals (19 columns) + browse kept + (other) absorb, second fold no-op |
| RollupVolumeTests.cs (2, opt-in) | Engine and store volume timing/memory |
| AnalyticsOptionsTests.cs / AnalyticsInstallerDatabaseTests.cs (modified) | New settings + tightened RawRetention boundary (renamed RawRetention_MustKeepEveryDayRollableForOneIntervalPlusOneHour: 1.07:35:02 passes, 1.07:35:01 fails); installer asserts/deletes the watermark key; shared DatabaseTestHost; all DB tests in one non-parallel collection |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | RawRetention minimum tightened from 02-03's 1 day + W + RollupInterval + 1 h to 1 day + 3W + FlushInterval + 5 min + RollupInterval + 1 h | Under the old minimum the completeness floor could skip every day (eligibility needs 1 day + 2W + flush + 5 min, the floor needs D − W ≥ now − RawRetention) | Configs between the old and new minimum now fail startup validation; default 48 h still passes |
| 2 | Behaviour fix: SearchEventInfo.SessionId and SearchInteractionInfo.SessionId/Position used ValidationHelper.GetValue<Guid?>/<int?>, which returned null for database values; now `GetValue(...) as Guid?/int?` | Found by DB tests; would have zeroed sessions and click positions in KPIs | Public getters now return stored values; covered by DB tests |
| 3 | Collation merge in the store: texts equal under the column collation are grouped (window function, BIN2 tiebreak) before insert | Latin1_General_CI_AS treats "straße" = "strasse"; the unique index would reject the whole day | Merged rows' SessionCount is an upper bound (KNOWN-LIMITATIONS) |
| 4 | SearchDailyInfo.cs not modified (listed in plan files) | Not needed | None |
| 5 | Other implementation details: EarliestRawDay uses GetScalarResult(DateTime.MinValue) (GetScalarResult<DateTime?> always returns null); watermark written before BulkInsert inside the transaction; failure test forces failure with a duplicate KPI; contract doc settings line updated; DatabaseTestHost lives in AnalyticsInstallerDatabaseTests.cs | Kentico API behaviour / test design | None |

## Key Patterns / Decisions

- Single-runner without a scheduler service: every node runs the job; a session-scoped, zero-timeout applock on an unpooled connection decides who works each tick.
- The watermark is the only cross-node state; it is read fresh inside the lock and moves in the same transaction as the day's KPIs, so a crash can never leave KPIs and watermark out of step.
- Retention is derived from the watermark (never ahead of the next day's input window) with an absolute privacy ceiling (RawHardLimit) for a stuck job.
- Fold preserves every per-day total exactly; reports must treat QueryText NULL as "(other)" and '' as browse.
- KNOWN-LIMITATIONS appended (project-sidecar/docs/KNOWN-LIMITATIONS.md): volume numbers + ~10× headroom; late rows after roll not counted; floor-skipped days lose KPIs by design + new RawRetention minimum; Enabled = false also stops retention incl. hard limit; stale cached watermark harmless; collation merge → SessionCount upper bound; fold details (string-built HAVING, 1000-group cap, partial-fold history, (other) SessionCount upper bound); batched deletes take two round-trips; unpooled lock connection, concurrency proven within one process only.

## Skill Audit

Skill audit: All required skills invoked ✓
- `/dotnet:csharp-refactoring`: invoked.
- `/kentico-web-development:content-retrieval`: invoked.

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| Resolved: whole day in memory — measured (engine 0.63 s / 95 MB at 100k rows), no streaming needed | Closed |
| Resolved: segmentation after partial purge — completeness floor never re-rolls a day whose input window may be purged | Closed |
| Audit deferral 1: query crossing the threshold after part of its history was folded | KNOWN-LIMITATIONS; reports show "(other)" explicitly |
| No config change tracking / live reload | v0.2 reports |
| Admin settings page | With the Phase 3 permissions concern |
| Cross-process lock contention proven within one process only | KNOWN-LIMITATIONS; observe on a real web farm |

## Environment Note

No xbk process was running during APPLY; `xperience` is at LuceneSuiteInstalledVersion = 3. When xbk next runs this code, the installer adds the watermark key and the rollup job applies retention to xbk's raw rows (by design, user decision 2026-10-09).

## Next

Phase 2 complete (4/4 plans). Next: Phase 3 Rules Engine — `/paul:plan`. Resolve the open permissions concern (custom permission for rule-editing pages vs. Lucene app UPDATE) during Phase 3 planning.

---
*Completed: 2026-10-09*
