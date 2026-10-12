# Enterprise Plan Audit Report

**Plan:** .paul/phases/11-search-insights/11-01-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The architecture is sound:
- it is read-only;
- text identity is decided in SQL;
- range semantics are reused from reports;
- the engine is pure and boundary-tested;
- there is no new public API.

Three things would have hurt in production:
- **Injection boundary.** The SQL boundary was described loosely ("parameterized dates") for a query that builds conditional-sum column expressions. Kentico column expressions often can't take parameters, and the plan didn't pin down what may become a literal.
- **Noise.** Rate thresholds at 20 searches turn tiny samples into findings. A 10-point regression can be 2 searches, and marketers would chase noise.
- **Latency.** The annotation cost was outside the latency budget, even though rule evaluation is the most expensive step: about 0.5 ms per 500 rules, per query.

## 2. What Is Solid

- **One SQL aggregation with conditional sums for current and previous periods.** Text identity stays with the collation, as PROJECT requires (07-01 lesson).
- **Range validation, PreviousIsPartial and CompleteThrough are reused from SearchReportService,** so insights can never disagree with the report on the same screen.
- **Parity test against report Queries** catches drift between the two code paths.
- **Annotations fail open with a flag,** so a broken rule store can't take down reports.
- **Surfacing threshold respected; no storage, so no new retention or privacy surface.**

## 3. Enterprise Gaps Identified

1. The SQL literal boundary for conditional sums was not specified.
2. Small-sample noise: rate rules fire on a handful of searches, and regressions are noisiest.
3. Annotation cost wasn't bounded or included in the performance budget.
4. Thresholds are site-wide; multi-index sites with very different volumes share one MinSearches.
5. A query that crossed the surfacing threshold after part of its history was folded into "(other)" (the 02-04 deferral) has understated previous-period figures, so a regression could be overstated.
6. Insights aren't cached; every page load re-aggregates.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | SQL literal boundary (gap 1) | Task 2 action | Only validated option ints and invariant `yyyy-MM-dd` DateOnly literals may appear; parameters where ObjectQuery allows; a test proves no client text reaches SQL |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Small-sample noise (gap 2) | AC-1, AC-3, Task 1 tests | New `MinAffected` (default 5) for every finding; regressions need it in both periods combined; 2-of-20 test |
| 2 | Annotation cost (gap 3) | AC-4, AC-5, Task 2 | Annotate only capped findings' distinct queries; volume test includes 500 rules + 200 synonyms in the 200 ms budget |
| 3 | Site-wide thresholds (gap 4) | Task 2 docs | Documented as site-wide |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Folded history understates previous period (gap 5) | Rare (only queries that newly crossed the threshold); MinAffected and the previous MinSearches guard dampen it; already a KNOWN-LIMITATION from 02-04 |
| 2 | No caching (gap 6) | Reports recompute on every change by design (PROJECT: "live" = recompute); the 200 ms budget holds |
| 3 | Per-index thresholds (gap 4) | No user demand yet; documented; settings stay in appsettings |

## 5. Audit & Compliance Readiness

- **Evidence:** boundary tests per rule, a DB parity test, a no-writes check and a recorded volume timing.
- **Silent failures:** annotation failure is flagged and logged; aggregation errors surface through the existing report error path (11-02).
- **Reconstruction:** findings are reproducible from SearchDaily, the options and the current rules/synonyms. They are deterministic, with ties broken by kind and text.
- **Privacy:** query text exposure is identical to reports (View-gated, surfaced only).

## 6. Final Release Bar

- **Must be true:**
  - no client text in SQL;
  - findings respect MinSearches and MinAffected;
  - the figures match the report;
  - ≤ 200 ms including annotations.
- **Remaining risks:**
  - site-wide thresholds;
  - folded-history edge cases;
  - context-only rules not shown as fixes.
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
