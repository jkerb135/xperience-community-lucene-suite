# Enterprise Plan Audit Report

**Plan:** .paul/phases/02-event-capture/02-03-PLAN.md
**Audited:** 2026-10-09
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. A pure, deterministic engine fed by shared vectors is the right shape. But the plan had three correctness holes that would have produced silently wrong KPIs: its own retention validation allowed raw rows to be purged before the job rolled their day; "page/filter changes never split" let one logical search run unbounded across midnight with no defined input range for the engine; and duplicate rows with equal timestamps had no deterministic winner, contradicting the determinism AC. Fixed below.

## 2. What Is Solid

- **Pure engine (no I/O, no clock)** with settings passed in: testable, deterministic, reusable by 02-04.
- **Fixed histogram edges and UTC days** protect stored history from configuration drift.
- **Startup validation naming the setting**: misconfiguration fails fast instead of skewing data.
- **Vectors drive both minter and engine**, including non-default settings proving configurability is real.
- **Midnight and multi-node scenarios** explicitly tested.

## 3. Enterprise Gaps Identified

1. Validation rule `RawRetention ≥ AttributionWindow + 1 h` permits purging raw rows before the day is rolled (the job waits ≥ 1 day + window).
2. Unbounded logical searches (paging forever) made day ownership and the engine's input range undefined.
3. No defined input contract for which raw rows the engine needs around a day boundary.
4. Duplicate (SearchId, Sequence) rows with equal ReceivedUtc resolved nondeterministically.
5. Click validity checked only an upper bound on position, so a click on page 1 position 3 credited to a page-2 row passed.
6. Engine memory for very high daily volumes.
7. Admin settings page.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Raw purge before rollup | AC-1 | RawRetention ≥ 1 day + AttributionWindow + RollupInterval + 1 h |
| 2 | Unbounded logical searches | AC-3, Task 2 | MaxLogicalSearchDuration = AttributionWindow; tests for 00:40 and 01:10 cases |
| 3 | Undefined input range | AC-3 | Explicit input contract (SearchIds touched in day ± window; interactions to day end + 2 × window) |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Nondeterministic duplicates | AC-3, Task 2 | Earliest ReceivedUtc, then lowest row id; tested across shuffles |
| 2 | Loose position check | AC-4 | (Page − 1) × PageSize < Position ≤ Page × PageSize |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Engine memory at very high volume | One day per run; 02-04 measures with synthetic volume and can stream per SearchId if needed |
| 2 | Admin settings page | User-deferred with the permissions concern |

## 5. Audit & Compliance Readiness

- **Evidence:** rule-by-rule tests, shuffle determinism, vector agreement under default and override settings, startup-validation tests.
- **Silent failures:** retention misconfiguration that would lose data now fails at startup.
- **Reconstruction:** deterministic engine means any day can be recomputed identically while raw rows exist.

## 6. Final Release Bar

- Must be true: AC-1..AC-7 with tests; validation rule corrected; input contract implemented and documented for 02-04.
- Remaining risk: volume behaviour unmeasured until 02-04.
- I would sign off on the plan as amended.

---

**Summary:** Applied 3 must-have + 2 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
