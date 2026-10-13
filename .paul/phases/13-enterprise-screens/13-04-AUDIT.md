# Enterprise Plan Audit Report

**Plan:** .paul/phases/13-enterprise-screens/13-04-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The plan is mostly a client restyle:
- search semantics are untouched;
- Search already accepts PageSize 1–50, so the only server change is a read-only summary of rules the user can already view;
- the user's decision to keep the configurable Compare columns avoids regressing the Phase 5 preview sandbox.

It introduces one new piece of state, the page size, without saying when that state is committed. The existing code commits `page` only on a successful search. If the page size were committed on request instead, a failed or superseded search would leave # positions and the "1–25 of N" footer describing rows that aren't on screen. In a tool marketers use to reason about rank, that is wrong data. This is the release-blocking item.

## 2. What Is Solid

- **The page commit pattern:** `page` is committed only with a successful result (verified in QueryTesterTemplate.tsx), so extending the same pattern to the size is a small change.
- **Patterns reused, not copied:** DataBar, RangeFooter and Stale come from 13-03. The RangeFooter change is additive (an optional `pageLimit`), so reports are unaffected.
- **Chip summaries reuse `RuleSummaries.Actions`:** one source of truth with the rules listing, and no new query: the same ObjectQuery reads one more column.
- **Rule details stay within existing permission:** chips link to the rule edit page, which enforces its own permission (read-only for View). The summary reveals only what a View user can already see on the rules listing.
- **Search rejection stays distinct from request failure,** with separate messages.

## 3. Enterprise Gaps Identified

1. The commit point for page size is unspecified, so # and the footer can disagree with the rows shown after a failed request.
2. Try again was specified for "Search failed" but not excluded from "Search rejected". Retrying a validation rejection with the same input can never succeed and teaches users to click a dead button.
3. Score bars would get a NaN width from non-finite scores or a zero top score (e.g. a page of pinned items scored 0).
4. `PrimaryAction` on invalid stored JSON was unspecified. Stored rules can be invalid, as the provider logs and skips them, and a throw there would fail the whole search response.
5. Chip accessibility: decorative icons inside link names would pollute the accessible names.
6. Rows kept visible during loading have no busy signal for assistive technology.
7. The score bar is relative to the page's top score, so bars aren't comparable across pages (same as 13-03, by design).
8. Applied-filter chips update before the search succeeds, so on failure the chips describe the new filters while the rows are old. This is existing behaviour and not touched by this plan.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Page size committed independently of the result (gap 1) | AC-2, Task 1, Task 3 | Page and size are committed together with a successful result; `position(page, size, index)` is pure and node-tested; a host check while blocked |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Try again on validation rejection (gap 2) | AC-4, Task 2 | Offered only for failed requests |
| 2 | NaN or invalid score ratios (gap 3) | AC-2, Task 1 | Non-finite or non-positive values give an empty bar; node test |
| 3 | PrimaryAction throws or misreports on invalid JSON (gap 4) | Task 1 | Returns null and never throws; a test case table |
| 4 | Chip accessible names (gap 5) | AC-1 | Icons aria-hidden; name = visible text; keyboard reachable |
| 5 | No busy state while old rows stay visible (gap 6) | AC-4, Task 2 | `aria-busy` on the results region |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Page-relative score bars (gap 7) | Handoff design; the exact score is always shown at 4 decimals |
| 2 | Filter chips ahead of a failed search (gap 8) | Pre-existing behaviour outside this plan's boundaries (filters side panel and chips are DO NOT CHANGE); the "Search failed" Callout states that previous results are shown |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - node tests (position, scoreRatio, chip icon, moved labels);
  - a .NET test (FiredRules mapping + PrimaryAction cases);
  - host checks at both widths, including a blocked-request consistency check and unchanged DB counts.
- **Silent failures:** none. Failures and rejections each have a visible state; invalid rule JSON degrades to no icon or summary instead of failing the search.
- **Authorization:** no new command and no new data class exposed.

## 6. Final Release Bar

- **Must be true:**
  - # and the footer always match the rows shown;
  - no NaN or broken bars;
  - invalid rule JSON never breaks a search;
  - Compare keeps its contexts;
  - both widths are clean.
- **Remaining risk:** page-relative bars, by design.
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 5 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
