# Enterprise Plan Audit Report

**Plan:** .paul/phases/07-report-data/07-02-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan is a dev tool that deliberately writes into a live host database, and that host is running a rollup job. Two failure modes were not controlled:

1. **Wrong target.** The only gate is one environment variable holding a connection string. A full `dotnet test` run in a shell where that variable is still set, or a pasted production connection string, would write synthetic history without a second check.
2. **Race with the rollup job.** The seed range is computed from the watermark and the earliest raw day, then written. The running host's job uses the same inputs, and nothing stops it from starting between the read and the write.

With the upgrades below, I would approve it.

## 2. What Is Solid

- **The real `RollupEngine` produces the stored numbers.** The seeded data is exactly what live traffic of the same shape would yield, so reports built on it are honest about the engine.
- **The seeder writes only days the job will never roll, and only empty ones.** It never touches the watermark, the raw tables or other indexes.
- **No Kentico initialization against the target.** This avoids web-farm server registration and cache side effects on a running instance.
- **The host index is opened read-only.** Lucene readers take no write lock, so the host's indexing is unaffected.
- **Clear is a parameterized, index-scoped range delete.** The fold's "(other)" rows inside the range are covered by the same delete.

## 3. Enterprise Gaps Identified

1. No confirmation of the target database; one stray environment variable is enough to write.
2. A rollup job starting concurrently could change the limits between computing the range and inserting.
3. Click positions could exceed ResultCount, or be generated for 0-result searches, which breaks the contract-faithfulness claim.
4. Seeded history counts toward the surfacing threshold of real queries with the same text. That weakens the k-anonymity intent for real traffic, and the plan's docs warning did not say so.
5. Seeded rows carry no marker, so clear trusts the operator's range.
6. A multilingual index (the host has `es` documents) mixes languages into the vocabulary.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Unconfirmed target (gap 1) | AC-3, Task 1, Task 2 tests | `LUCENESUITE_SEED_CONFIRM` must equal the target's database name for both seed and clear; tests cover a missing or mismatched value |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Race with the rollup job (gap 2) | AC-3, Task 1, Task 2 tests | Hold `RollupLock` ("LuceneSuite.Rollup") for the run and refuse when it is held; re-read the limits inside the write transaction; test with a held lock |
| 2 | Click bounds (gap 3) | AC-1, Task 1 | Clicks ≤ min(ResultCount, page end); no clicks on 0-result searches |
| 3 | Surfacing contamination (gap 4) | Task 2 docs, verification | Docs warning plus a KNOWN-LIMITATIONS entry; never seed production |
| 4 | Clear transparency (gap 5) | AC-3 | Runner prints the random seed; clear prints the per-day rows it deleted |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | No seed marker on rows (gap 5) | A marker needs a schema change, which is out of scope for a dev tool. Already mitigated: the database-name confirmation, the printed exact clear value, and the range being recorded in APPROVAL-CHECKS for the host. |
| 2 | Mixed-language vocabulary (gap 6) | Only affects how realistic the demo data looks. Recorded as a limitation; a language filter can be added if the demo needs it. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - Unit tests for generation fidelity and determinism.
  - Database tests for every guard: range limits, non-empty range, confirmation, held lock, untouched watermark, raw tables and other indexes, exact clear.
  - SQL verification on the host, with the result recorded in APPROVAL-CHECKS.
- **Silent failures prevented:**
  - Writes to an unintended database.
  - A seeded day being re-rolled, or a real day being seeded.
  - Impossible click data.
- **Reconstruction:** the printed seed, range, row counts and clear value make a seeding run repeatable and reversible.
- **Ownership:** a dev-only runner in the test project; nothing ships in a package.

## 6. Final Release Bar

- **Must be true:** the confirmation gate, the lock plus in-transaction limits, and every DB test green; the host run verified by SQL and recorded.
- **Remaining risks:**
  - Clear trusts the given range.
  - Seeded text inflates real queries' surfacing on the demo host.
  - The vocabulary mixes languages.
- **Sign-off:** I would sign this plan as amended, for development and demo databases only.

---

**Summary:** Applied 1 must-have + 4 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
