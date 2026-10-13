---
phase: 13-enterprise-screens
plan: 03
subsystem: ui
tags: [kentico-admin, reports, data-viz, accessibility]

requires:
  - phase: 08-reports-ui / 08.1
    provides: Search reports template, KPI tiles, TrendChart, QueryTable, per-index report state
  - phase: 11-insights
    provides: Needs attention card and insights findings (kept, restyled)
provides:
  - Local report patterns (DataBar, Sparkline, RangeFooter, Stale context) in reports/patterns.tsx
  - Rows per page 10/25/50 on the Queries command (validated server-side, remembered per index)
  - Empty-period Callout with Open query tester; Retry on a failed report with a graphics-only stale fade
affects: [13-04 query tester (score bars, range footer, states reuse patterns.tsx)]

tech-stack:
  added: []
  patterns:
    - "Stale context per card: each card fades its graphics only when its own request failed; text never fades"
    - "Data bars and sparklines are aria-hidden; the number is always rendered as text"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/patterns.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/patterns.test.ts
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/SearchReportsTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/QueryTable.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/NeedsAttention.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/TrendChart.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchReportsPage.cs

key-decisions:
  - "User: keep the 11-02 insights card, apply the handoff's styling (the handoff predates Phase 11)"
  - "Audit: stale fade on graphics only, plus a 'Showing the last loaded report.' line (handoff fades text to ~3:1)"
  - "Empty Callout hidden while stale (it would describe an earlier period)"

patterns-established:
  - "Reuse patterns.tsx (DataBar/Sparkline/RangeFooter/Stale) for 13-04"

duration: ~3h (implementation delegated; host verification by me)
completed: 2026-10-12T00:00:00Z
description: "Search reports match the handoff: tile sparklines, data bars in the query table and findings, rows per page 10/25/50, empty-period Callout, and Retry with a graphics-only stale fade"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 13 Plan 03: Search Reports per Handoff Summary

**The search reports page now looks like the handoff: sparklines on the tiles, data bars on the tables, page-size control, and designed empty and error states.** The numbers, SQL and insights logic are unchanged.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Sparklines | Pass | 6 tiles, 30 points for 30 days, from the same response; aria-hidden; white on the selected tile; tile names and aria-pressed unchanged |
| AC-2: Data bars + range footer | Pass | Searches bar scaled to the page max; No results and CTR scaled 0–100 %; "1–25 of 141 queries"; 10/25/50 persists; the server refuses anything else (DB test) |
| AC-3: Insights restyled | Pass | Query + "{n} searches" + 160 px DataBar per finding kind (`findingBar`, node-tested); fallback, All insights panel and Response time kept |
| AC-4: Empty and error states | Pass | Callout + Open query tester navigates; Retry is latest-wins and disabled in flight; fade 0.6 on graphics only, text #525252 at full opacity (≈7.8:1); never empty while failed or loading |
| AC-5: No regressions | Pass | ≈670 and 1269 px: no overflow, 50 px rows, actions on one line; tokens resolve at runtime; Debug + Release 0 warnings; .NET 838 passed / 6 skipped; client 100/100; tsc clean; DB counts unchanged |

## Verification Results

- `dotnet build` Debug and Release: 0 warnings, 0 errors.
- `dotnet test`: 838 passed, 6 skipped, 0 failed.
- Client `npm test` 100/100; `tsc --noEmit` clean.
- Host evidence is in APPROVAL-CHECKS #18: failures were simulated by blocking the Report and Queries commands in the browser, and the counts are back at baseline (30 / 3389 / 1 / 0 / 0).

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Tasks 1–2: patterns, page size, sparklines, insights styling, states | `c142a5d` | feat | Search reports per handoff |
| Task 3: host verification + approval entry | (UNIFY commit) | docs | APPROVAL-CHECKS #18 |

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Design clarification | 1 | The stale fade is scoped **per card**: the query table and insights card load through their own commands, so each fades only when its own request fails. A failed Report command doesn't fade a table that just loaded fresh |
| Design clarification | 1 | If the last report that loaded had no searches, a failed load shows only the alert and stale line. The empty Callout is suppressed while stale |

**Total impact:** both follow from the audit-added rules; there was no scope creep.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| The first stale probe found no graphics to fade | The last report that loaded was an empty period, so the template correctly showed no tiles or charts. Re-tested from a 30-day report: the fade is correct |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | At PLAN and again before markup in APPLY |
| Admin UI design validation | ✓ | Browser pane: both widths, runtime tokens, contrast, states |
| /dotnet:csharp-refactoring | ✓ | Loaded in session (small additive SearchReportsPage change) |
| /update-docs | deferred | Runs at Phase 13 completion (after 13-04); search-reports.md should mention rows per page and the Retry state |

## Next Phase Readiness

**Ready:** 13-04 (query tester), which can reuse `patterns.tsx` for score bars, the range footer and the stale states.

**Concerns:**
- Searches bars scale to the page maximum, so lengths aren't comparable across pages (handoff design, listed for approval).
- Still open from 13-02: the "(UTC)" date labels on the rule form.

**Blockers:** None

---
*Phase: 13-enterprise-screens, Plan: 03*
*Completed: 2026-10-12*
