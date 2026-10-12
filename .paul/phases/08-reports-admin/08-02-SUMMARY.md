---
phase: 08-reports-admin
plan: 02
subsystem: ui
tags: [kentico-admin, react, sessionstorage, permissions]

requires:
  - phase: 07-report-data
    provides: SearchReportService.Queries (4 sorts, skip/take, TotalCount, Other, IsBrowse)
  - phase: 08-reports-admin
    provides: SearchReportsPage, range bar, report.ts formatters (08-01)
provides:
  - Queries card on the Search reports page (Top / No results / Lowest CTR / Slowest, 25 per page, "(other)" line)
  - Row actions that open the query tester, Create rule or Add synonym prefilled through a one-time sessionStorage handoff
  - Report range/sort/page restored per index after a round trip
affects: [09-suggestions admin, v0.2 milestone review]

tech-stack:
  added: []
  patterns:
    - "Cross-page prefill = one-time sessionStorage handoff bound to target, index, exact page path and a 2-minute expiry; never URL query strings"
    - "Handoff read is side-effect free in render (peek) and cleared idempotently in an effect, so StrictMode double calls cannot lose it"
    - "Labelled Buttons in a ComponentCell instead of npm ActionCell icon buttons (31.9.1 announces every icon as \"button\")"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/QueryTable.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/handoff.ts
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/handoff.test.ts
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchReportsPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/SearchReportsTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/RuleConditionsFormComponent.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/synonyms/SynonymTermsFormComponent.tsx
    - tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchReportsPageDatabaseTests.cs
    - .paul/APPROVAL-CHECKS.md

key-decisions:
  - "Row actions are labelled Buttons stacked in a ComponentCell (npm 31.9.1 ActionCell icon buttons are all aria-label=\"button\")"
  - "31.9.1 ComponentCell takes a component type; passing an element crashes the page"
  - "Handoff split into peek (render) + clear (effect) instead of a single take"
  - "Placeholder texts ({email}/{number}) flagged by the server (HasPlaceholder), never the client"

patterns-established:
  - "Prefill consumers act only when the target form is empty and location.pathname ends with the handoff's target path"

duration: not recorded (APPLY ran in an earlier session)
completed: 2026-10-12T00:00:00Z
description: "Paged query table on the Search reports page with query tester / Create rule / Add synonym row actions prefilled once via a path-bound sessionStorage handoff; verified against SQL on the seeded host with nothing saved"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 8 Plan 02: Query Table and Row Actions Summary

**The Search reports page now has a Queries card:**
- four sorts (Top / No results / Lowest CTR / Slowest);
- 25 rows per page;
- an "(other)" line.

Each row opens the query tester, Create rule or Add synonym with the query already filled in. A marketer goes from a no-result query to a prefilled rule in two clicks.

## Performance

| Metric | Value |
|--------|-------|
| Duration | Not recorded (APPLY in an earlier session) |
| Tasks | 3 of 3 completed |
| Files | 3 created, 8 modified |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Queries command | Pass | VIEW-gated `Queries(from, to, sort, page)`, 25 per page. It returns Error for a bad range, an unknown sort or page < 1, and an empty Rows with the true TotalCount past the end. `HasPlaceholder` is computed on the server. CanManageRules/Synonyms and the three target paths come from client properties. Registration + 4 DB tests. |
| AC-2: Query table | Pass | Sort toggle, paging, browse row label, "(other)" with a tooltip, 08-01 formatters. Range, sort and page are restored per index from sessionStorage (validated with `isAllowed`). Host: Slowest page 2 restored exactly after the round trip. |
| AC-3: Row actions and handoff | Pass | Single use, target/index/path/expiry bound, empty-form only, tolerates garbage or a throwing storage (node tests). The tester's prefilled search is untracked (no raw row written on the host). Placeholder and browse rows are disabled with a reason. |
| AC-4: Verification | Pass with notes | Top/30 days matches SQL. Each action prefilled once (a reload opens empty). Host still has 0 rules / 0 synonyms. 1,100 / 1,700 px, keyboard order checked. APPROVAL-CHECKS #6 Pending. The permission-disabled state and existing editors are code/test-only (the host has no View-only user and no rules or synonyms). |

## Verification Results

Re-run at UNIFY (2026-10-12):
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test --filter AdminRegistration|SearchReportsPage`: 12 passed; the 4 DB tests were skipped without the env var. With `LUCENESUITE_TEST_CONNECTIONSTRING` → comm_projects_test: 4/4 passed.
- Client: `npm run typecheck` clean; `npm test` 50/50.
- Host (APPLY): see APPROVAL-CHECKS #6. Top/30 days first rows equal SQL over surfaced texts (bleached 612, which brewing fits you? 285, anfim super caimano 212, …).

## Accomplishments

- Closes the measure → act loop that Phase 8 was for: report row → prefilled rule or synonym with no URL state and no server state.
- The handoff cannot land in an existing rule's or synonym's edit page: the audit's must-have binds it to the exact target path, and a node test covers the sibling path.
- Following a report link never adds analytics rows.

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Tasks 1–3 | `d0f6c1c` | feat | Command + DTOs, QueryTable, handoff, editor prefill, tests, APPROVAL-CHECKS #6 |

Plan metadata: `ea5aff6` (docs: plan + audit)

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 2 | Accessible row buttons; StrictMode-safe handoff |
| Scope changes | 0 | — |
| Deferred | 0 new | Recorded in KNOWN-LIMITATIONS ("Reports row actions") |

### Details

**1. ActionCell replaced by labelled Buttons.**
- **Planned:** the plan's component map named the npm `ActionCell` with `TableAction` icon buttons.
- **Problem:** in 31.9.1 every one of those buttons is announced as "button".
- **Fix:** each row instead gets three small labelled `Button`s in a `ComponentCell`. The rows are taller; this is up for the user's decision in APPROVAL-CHECKS #6.
- **Related:** `ComponentCell` must be given a component type, not an element.

**2. `take` split into `peek` + `clear`.**
- **Problem:** React StrictMode double-invokes render and effects, so a single destructive `take` could consume the handoff before the form used it.
- **Fix:** reading is now side-effect free and the clear is idempotent.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Disabled buttons can't take focus, so keyboard users don't hear the reason | Hover tooltip only. In KNOWN-LIMITATIONS, with the upgrade path (visible reason text); flagged in APPROVAL-CHECKS #6 |
| The saved report state once showed "7 days" without a click | Not reproduced in 3 deliberate repeats; flagged in APPROVAL-CHECKS #6 to watch |
| The UNIFY shell had no `LUCENESUITE_TEST_CONNECTIONSTRING` | Set inline for the DB test run |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ PLAN / ○ APPLY | No record of the APPLY re-invocation. Props were evidently checked against typings (the ActionCell finding) |
| Admin UI design validation | ✓ | Two widths, keyboard order, labels, states (APPROVAL-CHECKS #6) |
| /dotnet:csharp-refactoring | ○ | No record of the invocation (APPLY ran in an earlier session) |
| /kentico-web-development:content-retrieval | ○ | No record of the invocation. The DB test reads through SearchReportService, with no new content queries |

## Next Phase Readiness

**Ready:**
- Phase 8 is complete. Reports, row actions and the handoff pattern are available to Phase 9, for example a "block suggestion" action from a report row.

**Concerns:**
- APPROVAL-CHECKS #4, #5 and #6 are pending the milestone review.
- The disabled-permission path has not been seen on the host.

**Blockers:** None

---
*Phase: 08-reports-admin, Plan: 02*
*Completed: 2026-10-12*
