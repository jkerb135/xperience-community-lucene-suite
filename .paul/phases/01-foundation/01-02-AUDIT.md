# Enterprise Plan Audit Report

**Plan:** .paul/phases/01-foundation/01-02-PLAN.md
**Audited:** 2026-10-08
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable as written. The architecture (testable executor + thin shell, generic hits, replaceable query factory, validation before Lucene) is sound, but the plan had an integer-overflow hole in its own deep-paging guard, an unhandled null from Lucene's QueryBuilder that turns an ordinary query ("the") into a crash, incomplete validation at a public-site trust boundary, and a strategy-resolution call that silently constructs unregistered types. With the upgrades applied I would approve it.

## 2. What Is Solid

- **Executor/shell split** forced by verified fact ([Certain] `LuceneIndex` ctor is internal): the Lucene logic is testable against a real in-memory index instead of mocks.
- **Facet path chosen by FacetsConfig presence**: [Certain] upstream throws without a taxonomy reader, so this avoids a guaranteed failure on non-faceted indexes.
- **Validation before touching Lucene** with explicit limits: correct place for a public-input boundary.
- **No speculative rules hook**; **no QueryParser dependency**.
- **Mutation check** on the facet OR semantics proves the test actually guards behaviour.

## 3. Enterprise Gaps Identified

1. `page × pageSize ≤ 1000` computed in int overflows for large `page` (e.g. int.MaxValue × 2 → negative), bypassing the guard.
2. [Likely] `QueryBuilder.CreateBooleanQuery` returns null when analysis yields no tokens (stop-word-only or punctuation queries); adding null clauses throws.
3. Validation omitted null/empty IndexName, null/empty facet dimensions/values and facet value length.
4. Facet filters sent to a non-faceted index would be silently ignored, returning unfiltered results the caller believes are filtered.
5. `ActivatorUtilities.GetServiceOrCreateInstance` would construct a strategy type that was never registered, masking misconfiguration; [Certain] upstream uses `GetRequiredService`.
6. `AddSingleton` for the service duplicates registrations on repeated `AddLuceneSuite` calls.
7. The mutation check had no proof the deliberate break was reverted.
8. Strategy + FacetsConfig are built per search, twice (suite and upstream).
9. Strategies are resolved from the root provider by a singleton; a strategy with scoped dependencies becomes a captive dependency.
10. Hits return every stored field; large Content fields inflate payloads.
11. No logging on Lucene failures.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Paging-window integer overflow | AC-2, Task 2 executor, Task 3 | Long/division-based check; int.MaxValue test |
| 2 | QueryBuilder null on no-token input | New AC-2b, Task 2 factory, Task 3 | Skip null clauses; all-null → empty BooleanQuery (0 hits); test |
| 3 | Incomplete trust-boundary validation | AC-2, Task 3 | Reject null/empty index, dims, values; value ≤ 256 chars |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Silent facet-filter drop | AC-5, Task 2 executor | ArgumentException for filters on a non-faceted index |
| 2 | Unregistered strategy silently constructed | Task 2 shell | `GetRequiredService`, mirroring upstream |
| 3 | Duplicate registrations | Task 2 registration | TryAddSingleton for the service |
| 4 | Unproven mutation revert | Task 3 verify, verification | `git diff --exit-code -- src/` before re-running green |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Strategy/FacetsConfig built per search (twice) | Correctness unaffected; Phase 2 timing data decides whether caching per index is worth it |
| 2 | Captive scoped dependencies in strategies | Identical to upstream's own behaviour; not introduced by the suite |
| 3 | Full stored-field payloads | Preview/admin needs all fields; a field allow-list can follow real payload measurements |
| 4 | No logging on Lucene failures | Exceptions propagate unswallowed; structured logging arrives with Phase 2 event capture |

## 5. Audit & Compliance Readiness

- **Evidence:** every AC maps to an automated test or command; the mutation check proves the facet test bites, and the clean diff proves it was undone.
- **Silent failures:** removed three (ignored facet filters, silently constructed strategies, overflowed guard). Remaining exceptions propagate to the caller.
- **Reconstruction:** all changes are in the Suite git repo; no untracked host edits in this plan.
- **Ownership:** public API documented (CS1591 enforced).

## 6. Final Release Bar

- Must be true: all ACs (incl. AC-2b) pass with tests; 0 warnings; locked-mode restore; xbk still builds.
- Remaining risk: shell paths (index lookup, facet choice, timing) untested until 01-03's checkpoint.
- I would sign off on the plan as amended.

---

**Summary:** Applied 3 must-have + 4 strongly-recommended upgrades. Deferred 4 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
