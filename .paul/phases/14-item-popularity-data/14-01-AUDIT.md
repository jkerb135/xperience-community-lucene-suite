# Enterprise Plan Audit Report

**Plan:** .paul/phases/14-item-popularity-data/14-01-PLAN.md
**Audited:** 2026-10-13
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The design keeps item numbers consistent with every existing report:
- they come from the same attribution pass, transaction and retention;
- an invariant check proves it.

The plan adds a schema version without fixing a latent web-farm hazard in the version gate it relies on. `AnalyticsRollupJob` skips only when the database is **older** than the build (`installed < Version`). During a rolling upgrade, a node still on the previous build sees a newer schema and keeps rolling days. Today that is harmless. From v8 on, an old node that wins the rollup lock writes SearchDaily and advances the watermark without writing ItemDaily, and that day's item history is lost for good (raw events are purged about 48 h later). That is release-blocking for a feature whose whole purpose is unbroken history.

## 2. What Is Solid

- **One pass, one transaction:** items are counted where SearchDaily clicks and conversions are counted, inside the same CMSTransactionScope with the watermark, so the two tables can't drift.
- **The consistency invariant** (Σ item clicks = Σ query clicks, Σ conversions likewise, per index and day) is a strong, cheap correctness check, covered by tests and a host SQL check.
- **Identity-free by construction:** day, index, item and counts only.
- **No search-path change; the shared analytics vectors stay fixed.**
- **Capture already rejects empty item GUIDs** (SearchInteractionLogger), so no item row can have `Guid.Empty` and the invariant has no exception case.

## 3. Enterprise Gaps Identified

1. The version gate lets an older build roll against a newer schema (above).
2. The host re-seed assumes SearchDaily stays unchanged, but the seeder regenerates from the current index. If the index changed since 07-02, the approved demo history (#4) changes silently.
3. Item rows for a renamed or deleted index stay until retention (the same as SearchDaily today).
4. ItemGuid is not checked against the current index (deleted content keeps its history). That's intended for popularity: it ages out with retention.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | The job rolls against a newer schema (gap 1) | AC-1, Task 1 | The gate becomes `installed != Version`. A newer database makes the job stand down with a rate-limited warning; tests cover both directions |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | The re-seed may change approved demo history (gap 2) | Task 3 | Record seeded-range totals before and after; report any difference as a change to #4's history; real days untouched |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Orphan rows after an index rename or delete (gap 3) | Same behaviour as SearchDaily; retention clears them; recorded in boundaries |
| 2 | Items no longer in the index (gap 4) | Intended for history; 14-02 resolves titles and shows missing items as such |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - engine invariant tests and the shared vectors;
  - DB tests for the installer, the transaction rollback, retention, the seed, and the version gate in both directions;
  - host SQL check of the invariant.
- **Silent failures:** the version gate closed the one path where item history could be lost without any error; a stood-down node now logs it.
- **Privacy:** no identifiers; retention matches SearchDaily (25 months by default).

## 6. Final Release Bar

- **Must be true:**
  - the invariant holds on every rolled and seeded day;
  - the job never rolls against a schema it doesn't know;
  - SearchDaily output is unchanged.
- **Remaining risk:** orphan rows until retention (documented).
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 1 strongly-recommended upgrade. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
