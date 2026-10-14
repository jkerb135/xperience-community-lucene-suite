# Enterprise Plan Audit Report

**Plan:** .paul/phases/14-item-popularity-data/14-02-PLAN.md
**Audited:** 2026-10-13
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The read side is sound:
- one aggregation over ItemDaily;
- titles resolved per page;
- missing items kept, so totals reconcile;
- the same View gate as the other report commands.

The write-adjacent shortcut is the risk. "Pin in a new rule" opens a create form with the item pinned at position 1 and no conditions. The engine treats a rule with no conditions as matching **every search** (`RuleEngine`: `Conditions.Count == 0 || …`), and nothing on the form says so. A marketer who clicks Pin then Save pins that item to the top of every query on the site. With Hide, the item disappears from every search. That is a plausible one-click mistake with site-wide effect, so it is release-blocking.

## 2. What Is Solid

- **Nothing is written by the report:** the shortcut only pre-fills an unsaved form, and saving goes through the normal rule validation, permissions and the 13-05 audit trail.
- **The handoff reuses the hardened 08-02 contract** (one entry, target, index, exact path, expiry, consumers fill only empty lists), with GUID validation on read.
- **Items no longer in the index are shown, not dropped,** so the card agrees with the item totals; their actions are disabled.
- **Stable sorting with an ItemGuid tie-break** keeps paging deterministic.

## 3. Enterprise Gaps Identified

1. A condition-less rule from the shortcut silently applies to every search.
2. A client-chosen sort column on a SQL aggregation is an injection or invalid-SQL risk unless the sort is mapped to fixed expressions.
3. View users see titles of indexed items they might not otherwise browse. This already holds for the query tester (KNOWN-LIMITATIONS 01-03/03-07).
4. Average click position on few clicks is noisy (one click at position 9 = 9.0).
5. Seeded demo items dominate the card on the dev host.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Condition-less rule applies to every search (gap 1) | AC-3, Task 2, Task 3 | A visible, polite "No conditions: this rule applies to every search." notice in the rule form whenever conditions are empty (create and edit); saving stays allowed; node test; host check |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Client sort reaching SQL (gap 2) | Task 1 | Fixed switch to constant ORDER BY expressions; unknown sort or size → error result |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Titles visible to View users (gap 3) | Same exposure as the query tester since 01-03; already recorded |
| 2 | Noisy average position (gap 4) | The clicked-searches count is shown beside it; the default sort is by clicked searches |
| 3 | Seeded items dominate on the dev host (gap 5) | Demo data, approved in #4 |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - DB tests (range, index isolation, sorts, paging, missing items, refused input);
  - handoff and warning node tests;
  - host comparison with SQL and the shortcut flows without saving.
- **Changes stay accountable:** a rule saved from the shortcut is logged by the 13-05 change log like any other rule.

## 6. Final Release Bar

- **Must be true:**
  - the card matches SQL;
  - shortcuts pre-fill only;
  - the every-search warning is visible;
  - the sort is whitelisted.
- **Remaining risk:** a marketer can still choose to save a condition-less rule, now deliberately.
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 1 strongly-recommended upgrade. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
