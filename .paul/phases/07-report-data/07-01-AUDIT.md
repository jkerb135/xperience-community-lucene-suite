# Enterprise Plan Audit Report

**Plan:** .paul/phases/07-report-data/07-01-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan reads only rollups, bounds the number of queries, keeps the surfacing threshold and defines every rate, all of which are right.

One flaw would produce wrong numbers that nobody notices. The plan decides whether a query is surfaced by matching strings from two separate SQL result sets in C#, but SQL Server groups text under the database collation (case-insensitive). Texts that differ only by case across days (for example "Coffee" and "coffee") would then miss each other in C#. A named query would silently end up in "(other)", or the reverse.

Two presentation-level traps would also mislead marketers:
- **Unrolled days look like zero traffic.** Today, and days after the watermark, have not been rolled yet, so the trend line would appear to collapse at the end.
- **Half-covered previous periods.** When the previous period starts before the first day with data, the comparison shows growth that isn't real.

With these upgrades applied, I would approve the plan.

## 2. What Is Solid

- **Reports read rollups only and never raw events.** The privacy bound and retention stay consistent.
- **The surfacing threshold is enforced at the reporting layer.** Text that isn't surfaced is never named; it only feeds the "(other)" totals.
- **Zero denominators yield null.** This avoids NaN and divide errors that would otherwise leak into the UI.
- **Percentiles are bounded honestly** by the fixed histogram edges, with an explicit overflow label.
- **A bounded number of aggregate queries per section, plus a volume test,** with no per-row round trips.

## 3. Enterprise Gaps Identified

1. Surfaced membership is decided by C# string matching across result sets, which conflicts with the SQL collation.
2. Days that haven't been rolled up are indistinguishable from days with zero traffic.
3. Previous periods that are only partly covered by data produce misleading deltas.
4. A `To` date in the future reads as zero traffic.
5. The service performs no authorization; callers must enforce it.
6. Reads concurrent with a rollup transaction can see a day before or after its replacement.
7. `SUM(int)` overflow on very large sites.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Collation-unsafe surfaced matching (gap 1) | AC-4, Task 1, Task 2 tests | Surfaced status is decided in SQL in the same statement (a `WhereIn` subquery for named rows, `NOT IN` + NULL for Other). A DB test covers case variants across days. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Unrolled days look empty (gap 2) | AC-1, Task 1, tests | `CompleteThrough` (the watermark) is returned with every result, and Trend rows carry `IsComplete`. Tested. |
| 2 | Partial previous period (gap 3) | AC-2, Task 1, tests | `FirstDataDay` and `PreviousIsPartial` are returned, and every delta is null when the previous period is partial. Tested. |
| 3 | Future `To` (gap 4) | AC-1, tests | `ArgumentException` when `To` is after today (UTC). |
| 4 | Authorization responsibility (gap 5) | Task 1 note | Recorded that the service performs no authorization; Phase 8 pages must gate on View and validate that the index exists. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Read during a rollup transaction (gap 6) | The rollup replaces a day atomically. A reader sees either the old or the new day, and the next refresh is correct. Reports are not an audit ledger. |
| 2 | `SUM(int)` overflow (gap 7) | It needs more than 2.1 billion logical searches per index in range. Fix by summing as bigint if a real site ever approaches it. |

## 5. Audit & Compliance Readiness

- **Evidence:** exact database tests for every section, including the collation, completeness and partial-period cases; pure maths tests; a volume timing test.
- **Privacy:** named text appears only above the surfacing threshold; no raw or identity data is read.
- **Silent failures closed:** collation mismatches, false end-of-range drops and fake growth from partial comparisons.
- **Accountability:** metric formulas are documented in `docs/search-reports.md`, and the authorization responsibility is assigned to Phase 8.

## 6. Final Release Bar

- **Must be true before shipping:**
  - Surfaced status is decided by SQL.
  - Completeness is flagged.
  - Partial comparisons show no deltas.
  - The DB and volume tests are green.
- **Remaining risks:**
  - The per-query view can be momentarily inconsistent during a rollup.
  - Theoretical int overflow.
  - Session-days are a sum, and percentile resolution is limited (both in KNOWN-LIMITATIONS).
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 4 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
