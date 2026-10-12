# Enterprise Plan Audit Report

**Plan:** .paul/phases/11-search-insights/11-02-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The plan reuses every proven piece:
- the handoffs;
- the BlockSuggestion command and its focus guard;
- the returnFocus side-panel pattern;
- the 03-07 edit-link pattern.

It also keeps the page's trust boundaries: the index comes from the page, and the command is View-gated and read-only.

As written, though, it **removed shipped functionality without a decision**. The 08.1-03 "queries without results" list would disappear, and below the default thresholds (20 searches, 30%) small sites would get an empty card where they used to get actionable queries. The user was asked and didn't object, but silence isn't a decision, and the safe default keeps the capability.

## 2. What Is Solid

- **The index is resolved on the server; edit URLs come from IPageLinkGenerator,** so there is no client-built path to another index.
- **The card and the panel share one response,** so they can't disagree.
- **RowActions has an optional action subset with defaults unchanged,** which protects the query table. It is never fed by request state, applying the 09-03 focus lesson.
- **Mapping is pure and node-tested** (labels, evidence, grouping), separate from markup.
- **Kind labels are text, not colour.**

## 3. Enterprise Gaps Identified

1. Regression: the card loses its no-result list when no finding passes the thresholds (small sites, short ranges).
2. Screen-reader grouping: each item has several buttons, and the item itself is not labelled.
3. Blocking from a finding doesn't change the finding (insights ignore the blocklist by design), which may confuse.
4. English-only labels and evidence (consistent with the rest of the admin).
5. No dismiss/snooze, so long-standing findings repeat.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Lost capability on no findings (gap 1) | Design map, AC-2, Task 2, Task 3, verification | Fall back to the pre-11-02 "queries without results" list (same data/actions); the empty sentence only when both are empty; verified on the host |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Item labelling (gap 2) | AC-2, Task 2 | `<li aria-labelledby>` on the query text, ids unique across card and panel |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Block doesn't alter findings (gap 3) | Correct by design (insights measure search, not suggestions); the toast confirms the block |
| 2 | English-only strings (gap 4) | The whole Suite admin is English; localisation is a release-wide concern |
| 3 | Dismiss/snooze (gap 5) | Needs storage; out of scope by phase decision; annotations already show handled queries |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - node tests for the mapping;
  - a DB test for the command, including edit URLs and no writes;
  - host checks per kind and action, plus focus and width measurements;
  - counts before and after.
- **Silent failures:** a failed load shows an alert with the last data kept; failed annotations show an info bar.
- **Privacy:** query text exposure is unchanged from reports (View-gated, surfaced only).

## 6. Final Release Bar

- **Must be true:**
  - no capability lost (fallback in place);
  - all actions pre-fill correctly;
  - focus returns on every panel close path;
  - no overflow at either width.
- **Remaining risks:** English-only; no dismissal.
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 1 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
