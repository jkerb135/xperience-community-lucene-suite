---
phase: 14-item-popularity-data
plan: 02
subsystem: ui
tags: [search-reports, admin-ui, react, rules, handoff]
requires:
  - phase: 14-item-popularity-data
    provides: LuceneSuite.ItemDaily (schema v8)
provides:
  - Search reports "Top clicked items" card and "All clicked items" side panel
  - TopItems page command (paged, sortable, VIEW permission)
  - rulePin / ruleHide handoff targets: rule create opens with the item pre-filled
  - "No conditions: this rule applies to every search." warning in the rule form
affects: [19 Popularity Boost]
key-files:
  created: [src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/TopItems.tsx]
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchReportsPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/PublicAPI.Unshipped.txt
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/
    - docs/search-reports.md
    - docs/rules-and-synonyms.md
completed: 2026-10-13
description: "Top clicked items card with sortable columns, All clicked items panel, and Pin/Hide in a new rule handoff into rule create"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 14 Plan 02: Top clicked items Summary

**Search reports shows the top 10 clicked items (sortable, with an all-items panel) and lets marketers open a new rule with an item pinned or hidden.**

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Top items data | DEFERRED | Code passes on read (constant ORDER BY, nulls-last avg position, ItemGuid tiebreak, totals past the end, VIEW permission, validation). SQL tests NOT executed: LUCENESUITE_TEST_CONNECTIONSTRING unset; risk accepted by Josh |
| AC-2: Card | Code pass; host checks deferred | Widths, focus and visuals deferred to APPROVAL-CHECKS #23 |
| AC-3: Pin/hide via new rule | Code pass; host checks deferred | Handoff, one-time prefill, no-conditions warning; host check in #23 |
| AC-4: No regressions | Pass; widths deferred | Debug and Release 0 warnings, client tests and tsc green, PublicAPI.Unshipped updated; widths in #23 |

## Verification

- Debug and Release builds: 0 warnings. tsc clean; client tests 0 failed.
- dotnet test: 877 total, 792 passed, 84 skipped (every Database test), 1 failed (`RulesOverhead_StaysUnderTenMillisecondsAtP95`, p95 10.8-16.3 ms at 48% background CPU; 14-02 touches no search or rule code, environmental).

## Commits

| Work | Commit | Description |
|------|--------|-------------|
| Tasks 1-2 (bundled) | 6e67cc5 | "reporting": data, page command, card, panel, handoff, plan and audit files |
| Review fix | 53505b4 | Top clicked items shows no table before the first successful load |
| Task 3 docs | 7e42481 | docs(14-02): card, panel, pin/hide, warning (already committed before UNIFY) |

## Deviations from Plan

- **No APPLY report; Josh applied directly on main** with one bundled commit instead of per-task commits. History is not rewritten.
- **Task 3 not performed in APPLY.** Docs were committed in 7e42481 and re-verified against source in UNIFY (no change needed); host checks moved to APPROVAL-CHECKS #23.
- DO NOT CHANGE list respected.

## Deferred Items

- DB tests (incl. new TopItems tests) not executed; re-run with LUCENESUITE_TEST_CONNECTIONSTRING before release.
- RulesOverhead p95 test failed under load; re-run on a quiet machine.
- Host checks for AC-2/AC-3/AC-4 (APPROVAL-CHECKS #23).
- Minor: changing the range in the All clicked items panel fires two requests (old page, then page 1); latest-wins discards the first.

## Next

Phase 14 plans are complete. Next: phase security audit, then Josh's phase UAT review. transition-phase not run.

---
*Phase: 14-item-popularity-data, Plan: 02 · Completed: 2026-10-13*
