# Enterprise Plan Audit Report

**Plan:** .paul/phases/02-event-capture/02-04-PLAN.md
**Audited:** 2026-10-09
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. Lock, scheduler, transactional replace and the totals-preserving fold are the right design. But two paths could destroy KPIs that were already correct: a stale (cached) or manually reset watermark would make a node re-roll a day whose raw rows had already been purged, replacing good totals with partial ones; and nothing prevented rolling a day whose input window was older than raw retention. Separately, a job that keeps failing would retain raw query text indefinitely, breaking the 48 h privacy promise. Fixed below.

## 2. What Is Solid

- **Non-blocking applock**: one worker per tick, others skip — no queueing, no deadlock risk.
- **Pure scheduler with injected clock**: boundary cases testable.
- **Delete-insert-watermark in one transaction** with an explicit failure-atomicity test.
- **Retention cut-off tied to the next day's input window**: a future rollup can never lose rows to normal retention.
- **Fold into (other)** with an every-column totals assertion.
- **Volume measured with a fallback trigger** rather than assumed.

## 3. Enterprise Gaps Identified

1. Watermark read through Kentico's settings cache can be stale on another node → re-roll after purge → corrupted KPIs.
2. No guard against rolling a day whose raw input window is already past retention (reset watermark, long outage).
3. A permanently failing job keeps raw rows (with query text) forever via the min() cut-off.
4. Job could start before the installer created SearchDaily.
5. A query that later crosses the threshold has part of its history already folded into (other).
6. Whether Kentico BulkInsert/BulkDelete enlist in CMSTransactionScope is unverified (covered by the failure-atomicity test).

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Stale watermark | AC-2, Task 2 | Uncached read inside the lock; write inside the day's transaction |
| 2 | Rolling from incomplete raw input | AC-2, Task 1 | Eligibility floor D − W ≥ now − RawRetention; skip + log; test with reset watermark |
| 3 | Indefinite raw retention when stuck | AC-4, Task 1/3 | New Retention.RawHardLimit (7 days, ≥ RawRetention) with error log; DB test |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Job before installer | AC-2, Task 1 | No work until installed version ≥ 3 |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Threshold crossed after partial fold | Inherent to pruning by age; record in KNOWN-LIMITATIONS; reports show "(other)" explicitly |
| 2 | Transaction enlistment of bulk ops | Proven or disproven by the failure-atomicity test; if disproven APPLY must stop (BLOCKED) |

## 5. Audit & Compliance Readiness

- **Evidence:** scheduler boundary tests, concurrent-tick test, atomicity test, retention/fold DB tests, E2E over 3 days, volume numbers.
- **Silent failures:** skipped days and hard-limit purges log errors naming the day.
- **Privacy:** raw query text has a hard upper bound even under failure.

## 6. Final Release Bar

- Must be true: AC-1..AC-8 with evidence, including the reset-watermark and stuck-job tests.
- Remaining risk: late rows after a day is rolled are not counted (documented).
- I would sign off on the plan as amended.

---

**Summary:** Applied 3 must-have + 1 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
