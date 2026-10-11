# Enterprise Plan Audit Report

**Plan:** .paul/phases/03-rules-engine/03-04-PLAN.md
**Audited:** 2026-10-10
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The layering is right: loading (`RuleProvider`, with Kentico's cache and its object-type dependency), deciding (the pure engine from 03-03) and applying (a query rewrite in the executor) are separate. Hides inside the query keep totals and facets honest.

Two gaps would have let rules break search, which the plan's own fail-open intent forbids:
1. **Unbounded query size.** One `MUST_NOT` clause per hidden item and one nested `BoostingQuery` per boost would hit `BooleanQuery.MaxClauseCount` (default 1024) and deep nesting as rule counts grow.
2. **Fail-open only covered loading.** An exception while building or running the rewritten query would still reach the caller.

With both fixed, I would approve it for APPLY.

## 2. What Is Solid

- **Kentico cache with the `lucenesuite.rule|all` dependency.** RuleInfo already touches dependencies and supports invalidation, so web-farm propagation is Kentico's own mechanism, not hand-rolled.
- **Cache entries are per index.** One index's edit reloads only that index.
- **Validity is checked at request time by the engine, not at cache time.** A cached rule set never fires outside its window.
- **Hides inside the Lucene query (user choice).** Hits, totals, paging and facet counts stay consistent.
- **A multiplicative boost (user choice) is independent of each query's score scale.** It is symmetric for bury, and one documented knob (`MaxBoostFactor`) is validated at startup.
- **`ApplyRules = false` and "no fired rules" give exactly the old query**, which is proven by score equality. This protects existing behaviour.
- **Request validation runs before rules,** so caller errors stay caller errors.
- **No rule or context data goes into analytics,** so the identity-free compliance posture is unchanged.

## 3. Enterprise Gaps Identified

1. **Clause-limit failure.** As planned, the query got one `MUST_NOT` `TermQuery` per hidden guid. With 50 hides per rule on 21+ fired rules, `TooManyClauses` is thrown and the search fails.
2. **Unbounded boost nesting.** One `BoostingQuery` level per (field, value) means thousands of nested levels in the worst case: slow scoring, and a risk of stack depth problems.
3. **Fail-open didn't cover applying rules.** Only `GetRules` failures were caught.
4. **Silent unruled results.** When rules fail, callers (the query tester, the preview sandbox) can't tell that results are unruled. A marketer would think a hide rule was broken.
5. **Log flood with `CacheMinutes = 0`.** The "one warning per cache fill" becomes one warning per request.
6. **Delete invalidation wasn't tested,** although AC-2 promises create, update and delete.
7. **The overhead benchmark uses a fake provider,** so the Kentico cache-hit cost isn't measured.
8. **Web-farm invalidation is proven in one process only.**
9. **Collation dependence.** `WhereEquals(IndexName)` relies on the database collation for case-insensitivity; the engine re-checks with `OrdinalIgnoreCase`.
10. **A boost on a field that isn't in the index silently matches nothing.**

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Clause limit and nesting (gaps 1, 2) | AC-3, Task 2 action + tests | All hides go in ONE constant-score `TermsFilter` `MUST_NOT` clause. Boosts are grouped by strength (at most 199 levels), with one `TermsFilter` for exact terms and phrase clauses capped below `MaxClauseCount` (overflow logged). Added a test with 500 rules × 50 hides × 50 boosts. |
| 2 | Fail-open when applying rules (gap 3) | AC-3, Task 2 action + tests | Executor failures on the rules path rerun the search without rules; validation errors still reach the caller. Tests force a rules-path failure through `MaxClauseCount` and check that invalid requests still throw. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Silent unruled results (gap 4) | AC-2, AC-3, Task 1 response | Public `RulesFailed` flag on the response. |
| 2 | Log flood (gap 5) | AC-2, Task 1 provider | The invalid-rule warning is throttled per index (1 minute). |
| 3 | Delete not tested (gap 6) | Task 1 DB test | A delete through the provider must disappear from the next `GetRules`. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Cache-hit cost not in the benchmark (gap 7) | An `IProgressiveCache` hit is an in-memory lookup, orders of magnitude below the 10 ms budget. The checkpoint exercises the real cache. |
| 2 | Web-farm invalidation proven single-process (gap 8) | It uses Kentico's documented object-type dependency and SupportsInvalidation. Revisit on a real farm, as with the 02-04 lock deferral. |
| 3 | Collation dependence (gap 9) | Kentico databases use case-insensitive collations by default, and the engine re-checks. A case-sensitive collation is unsupported for Kentico itself. |
| 4 | Boost on a missing field matches nothing (gap 10) | Already a scope limit. 03-06 can validate fields against the index when editing. |

## 5. Audit & Compliance Readiness

- **Evidence:** every AC clause maps to a pure test, a database test or a checkpoint step. The benchmark value goes in the SUMMARY.
- **Silent failures:** rule failures now produce a throttled warning plus a `RulesFailed` flag on the response. Invalid stored rules are named by id.
- **Reconstruction:** the outcome is deterministic for (rules, request, time). Rule edits appear in the Kentico event log (LogEvents from 03-03).
- **Privacy:** contexts are caller-supplied code names. They're never stored or logged, and analytics is unchanged.

## 6. Final Release Bar

- **Must be true before this ships:**
  - The rewritten query has bounded size.
  - No rule failure can fail a search.
  - Hidden items are absent from hits, totals and facets.
  - The boost factor is exact and the overhead stays below 10 ms at p95.
  - The host checkpoint is approved and the temporary rules are deleted.
- **Remaining risks:**
  - Web-farm invalidation is proven in one process only.
  - Phrase matches beyond the clause limit per strength group are dropped (logged).
  - Missing-field boosts silently do nothing.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 2 must-have + 3 strongly-recommended upgrades. Deferred 4 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
