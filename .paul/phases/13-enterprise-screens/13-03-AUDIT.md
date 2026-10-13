# Enterprise Plan Audit Report

**Plan:** .paul/phases/13-enterprise-screens/13-03-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The plan is low-risk by construction:
- the numbers, SQL and insights data are frozen;
- sparklines reuse the series already loaded (no new request);
- the bars are `aria-hidden` with the number kept as text;
- the only server change is a validated page size.

One handoff instruction, copied as-is, would have shipped an accessibility regression: **the last report at 60 % opacity** fades text as well as graphics. Low-emphasis text (about 7.8:1) drops to about 3:1, below WCAG AA and below the Suite's own conventions (08.1, deltas and tiles at ≥ 4.5:1).

Two behaviours were also underspecified:
- whether Retry respects latest-request-wins (a slow retry could overwrite a newer period);
- what triggers the empty state (failed or loading must never show "No searches").

## 2. What Is Solid

- **Insights card kept** (user decision), with styling only, so no Phase 11 behaviour is at risk.
- **Patterns are pure and node-tested** (ratio clamping, nulls, gaps, footer text); colours come from runtime tokens.
- **Page size validated server-side to {10, 25, 50}** and remembered in the existing per-index state.
- **The 09-03 lesson carried forward:** cells built from stable component types, never from request state.
- **Spinner instead of skeletons:** the Phase 13 deviation is applied consistently.

## 3. Enterprise Gaps Identified

1. 60 % opacity on text breaks contrast (WCAG 1.4.3).
2. Retry could bypass latest-request-wins and race a newer period change.
3. The empty-state condition was unspecified (risk of "No searches" on failure or while loading).
4. The Searches bar is scaled to the page maximum, so bar lengths aren't comparable across pages (handoff design).
5. A 12-month range draws about 365 points in a 64 px sparkline (dense but cheap).

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Text contrast under the stale overlay (gap 1) | AC-4, Task 2, Task 3 | Opacity only on graphics; text at full colour plus a "Showing the last loaded report." line; contrast measured on the host |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Retry race (gap 2) | AC-4, Task 2 | Retry uses the latest-wins request helper and is disabled while in flight |
| 2 | Empty-state trigger (gap 3) | AC-4, Task 2 | Reuse the existing "No searches" condition; never on failed or loading |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Page-relative Searches bars (gap 4) | Handoff design; the number is always shown; the footer states the page |
| 2 | Dense sparklines on long ranges (gap 5) | Decorative, `aria-hidden`; polyline cost negligible at 365 points |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - node tests for patterns and finding bars;
  - a DB test for page size;
  - host checks for both widths, runtime colours, the contrast measurement, and confirmation that nothing is written.
- **Silent failures:** none; error states are explicit and retryable.

## 6. Final Release Bar

- **Must be true:**
  - identical numbers;
  - text contrast ≥ 4.5:1 in every state;
  - Retry never overwrites newer data;
  - no overflow at both widths.
- **Remaining risk:** page-relative bar scaling (by design).
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 2 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
