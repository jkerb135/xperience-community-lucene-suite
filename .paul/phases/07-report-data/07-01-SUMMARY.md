---
phase: 07-report-data
plan: 01
subsystem: analytics
tags: [kentico-objectquery, aggregatedcolumn, sql-server, reports, histogram]

requires:
  - phase: 02-event-capture
    provides: SearchDaily rollups (fixed latency histogram, "(other)" NULL rows, watermark settings key)
provides:
  - Internal SearchReportService (Overview, Trend, Queries, Latency) registered in AddLuceneSuite
  - docs/search-reports.md metric definitions
affects: [08-reports-admin, 07-02 seed generator]

tech-stack:
  added: []
  patterns:
    - "SUM aggregates via Kentico AggregatedColumn(AggregationType.Sum, col).As(col), read back as typed SearchDailyInfo rows"
    - "Surfaced membership via WhereIn(QueryText, GROUP BY … HAVING subquery); (other) = range totals − named totals"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/SearchReportService.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/SearchReportModels.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/LatencyPercentiles.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchReportTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchReportDatabaseTests.cs
    - docs/search-reports.md
  modified:
    - src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs

key-decisions:
  - "(other) computed as range totals minus named totals instead of NOT IN (timed out at volume)"
  - "Percentile rank computed in decimal (0.95 × 20 = 19 exactly)"
  - "Deltas are relative for counts and rates alike"

patterns-established:
  - "Avoid NOT IN over the nullable QueryText column; partition by subtraction"

duration: ~45min
completed: 2026-10-12T00:00:00Z
description: "Internal SearchReportService over SearchDaily: overview + previous period, trend with completeness, surfaced query tables + (other), histogram percentiles; ~100 ms per section at 200k rows"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 7 Plan 01: Report Query Service Summary

**Internal `SearchReportService` turns SearchDaily rollups into overview (with previous-period deltas), daily trend, surfaced query tables with one "(other)" row, and latency p50/p95/p99. Each section takes about 100 ms at 400 days × 500 queries.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~45 min |
| Tasks | 2 of 2 completed |
| Files | 6 created, 1 modified |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Request validation | Pass | Blank index / From > To / To > today / range > DailyRetention throw ArgumentException naming the parameter. CompleteThrough = watermark; Trend IsComplete (unit + DB tests) |
| AC-2: Overview | Pass | Exact totals, rates, deltas, FirstDataDay; PreviousIsPartial nulls every delta; other index excluded (DB tests) |
| AC-3: Trend | Pass | One row per day including zero days (Ctr null) |
| AC-4: Query table | Pass | Surfaced decided in SQL; "Coffee"/"coffee" merged under the collation. Near-miss, one-day and NULL rows summed into Other. All 4 sorts with ordinal tie-break; skip/take validation; TotalCount; IsBrowse |
| AC-5: Latency | Pass | Percentile cases (empty, single bucket, exact boundary, overflow, p99 of 1) plus DB histogram |
| AC-6: Efficient queries | Pass (method changed) | Overview: 2 aggregates + watermark key; trend 1; queries 2 (named with surfaced subquery + range total); latency 1. All values are parameters or validated ints in Having. Volume: overview 104 ms, trend 105 ms, queries 119 ms, latency 86 ms |

## Verification Results

- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` with `LUCENESUITE_TEST_CONNECTIONSTRING` → 609 passed, 4 skipped (opt-in volume tests).
- `--filter SearchReport` with `LUCENESUITE_VOLUME_TEST=1` → all pass. Setup: 200,000 rows in 5.2 s.

## Accomplishments

- Phase 8 can render every report section for any index and range through one internal service.
- The surfacing threshold (k-anonymity) is enforced at the reporting layer, with text equality defined only by the database collation.
- Unrolled days and partial comparisons are flagged, so the screens can show them instead of a false drop or fake growth.

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Tasks 1 + 2 | `b4fd37f` | feat | Service, models, percentiles, DI, unit/DB/volume tests, metric doc |

Plan metadata: `0ed544e` (docs: plan + audit)

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Analytics/Reports/SearchReportService.cs` | Created | Validation and the aggregate queries for the four sections |
| `Analytics/Reports/SearchReportModels.cs` | Created | Immutable report records, rates, QuerySort |
| `Analytics/Reports/LatencyPercentiles.cs` | Created | Histogram percentile helper |
| `LuceneSuiteServiceCollectionExtensions.cs` | Modified | `TryAddSingleton<SearchReportService>` |
| `tests/.../SearchReportTests.cs` | Created | 14 pure tests (percentiles, rates, deltas, previous period, validation) |
| `tests/.../SearchReportDatabaseTests.cs` | Created | 7 DB tests + 1 volume test |
| `docs/search-reports.md` | Created | Metric definitions, surfacing, UTC days, percentile method |

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| "(other)" = range totals − named totals | `NOT IN (surfaced) OR IS NULL` timed out (30 s) on 200k rows. Named and Other partition the range, so subtraction is exact. Surfaced membership is still decided by SQL. | Queries section runs in 119 ms at volume |
| `TimeProvider` + `DailyRollupStore` injected | Reuses the existing watermark reader and makes "today" testable | No new abstractions |
| Relative deltas for rates too | Matches "null when previous is null or zero" | Documented in search-reports.md |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 1 | Essential (performance); AC-6 met |
| Scope additions | 0 | — |
| Deferred | 0 new | Audit deferrals recorded in KNOWN-LIMITATIONS |

### Auto-fixed Issues

**1. Performance: Other bucket via NOT IN timed out**
- **Found during:** Task 2 (volume test)
- **Issue:** SQL Server's plan for NOT IN over the nullable column with a GROUP BY/HAVING subquery exceeded the 30 s command timeout.
- **Fix:** One unfiltered range aggregate; Other = all − Σ named.
- **Files:** `SearchReportService.cs`
- **Verification:** DB test asserts the exact Other row; volume test runs in 119 ms.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Hand-computed test expectations used the wrong bucket bounds (bucket 3 is ≤ 100 ms) | Expectations corrected; code was right |
| xunit tuple compare of `List` vs array failed despite equal values | Split into separate asserts |

## Skill Audit

All required skills invoked ✓ (/dotnet:csharp-refactoring, /kentico-web-development:content-retrieval).

## KNOWN-LIMITATIONS added

- Session-days are a sum of daily session counts.
- Percentiles are limited to the histogram bucket bounds.
- Queries surface on all retained history.
- Reads during a rollup are not isolated; int sums can overflow (audit deferrals).

## Next Phase Readiness

**Ready:**
- 07-02 seed generator can fill SearchDaily, and the report service shows the result right away.
- Phase 8 admin pages consume `SearchReportService` through InternalsVisibleTo Admin.

**Concerns:**
- Phase 8 must gate on Site Search View and validate the index, because the service performs no authorization.

**Blockers:** None

---
*Phase: 07-report-data, Plan: 01*
*Completed: 2026-10-12*
