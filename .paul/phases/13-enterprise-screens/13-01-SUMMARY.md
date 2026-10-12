---
phase: 13-enterprise-screens
plan: 01
subsystem: ui
tags: [kentico-admin, react, rules, bulk-actions, event-log]

requires:
  - phase: 03-rules-engine
    provides: RuleInfo, RuleOwnership, RuleEditMapping version token, rule edit pages
  - phase: 08.1-admin-ui-consistency-audit
    provides: pageStyle gutter, row-action style, latestRequest/adminHref helpers
provides:
  - Custom rules listing template (status, counts, search, sort, paging, sessionStorage view state)
  - Bulk Enable / Disable / Change priority / Delete with partial-failure results and per-rule audit log
  - RuleListing.Of derived-status rule (reused by 13-02)
affects: [13-02 rule edit (status helper), 13-03, 13-04]

tech-stack:
  added: ["react-router-dom 7.18.4 (devDependency, types only — external at runtime)"]
  patterns:
    - "Custom templates with child pages wrap their content in RoutingContentPlaceholder"
    - "In-admin navigation via react-router-dom useNavigate + adminHref(path, location.pathname)"
    - "Bulk commands: per-rule IInfoProvider.Set/Delete, version re-check, ExpectedCount for filter mode, IEventLogService audit with before → after"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleListing.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/RuleListingTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/listing.ts
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleListingTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/RuleListingPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/PublicAPI.Unshipped.txt

key-decisions:
  - "Priority stays ≥ 1 in bulk changes (RuleSerializer rejects lower)"
  - "Bulk bar replaces the toolbar row at the same height (no table shift on select)"
  - "Date columns drop out below 1,100 px table width (npm Table ignores visible)"

duration: ~1 day (2026-10-12)
completed: 2026-10-12
description: "Rules listing rebuilt as a custom template: derived status with counts, search/sort/paging over 500 rules, bulk enable/disable/priority/delete with partial-failure reporting and per-rule event-log audit"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 13 Plan 01: Rules listing Summary

**The Rules page handles 500 rules: derived status with filter counts, search, sort and paging, and bulk Enable / Disable / Change priority / Delete that report every failure and write a reconstructable event-log entry.**

## Performance

| Metric | Value |
|--------|-------|
| Completed | 2026-10-12 |
| Tasks | 2 auto + 1 checkpoint (approved) |
| Files modified | 11 (+ PAUL docs) |
| List command | ≈ 14 ms on the host with 501 rules |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Derived status at boundaries | Pass | Unit tests; host counts 391 + 35 + 25 + 50 = 501, seeded dates matched |
| AC-2: Search/filter/sort/paging | Pass | Host: "zz-seed-01" → 10 rules, List ≈ 14 ms; state restore checked by user |
| AC-3: Bulk with partial failure | Pass | Host: Disable 3 → toast "3 rules disabled."; stale-version path unit-tested, checked by user |
| AC-4: Change priority | Pass | Unit tests (shift/set/overflow, ≥ 1); checked by user |
| AC-5: Select all matching | Pass | Filter re-applied server-side (unit test); checked by user |
| AC-6: Authorization and ownership | Pass | AdminRegistrationTests (MANAGE_RULES on all bulk commands, VIEW on List); foreign ids fail; View role checked by user |
| AC-7: Changes reach search | Pass | Per-rule Set/Delete (code review); checked by user |
| AC-8: States | Pass | Spinner / two Callouts / error bar with Retry; checked by user |
| AC-9: Filter-mode refusal | Pass | ExpectedCount check unit-tested; checked by user |
| AC-10: Reconstructable audit | Pass | Host event log: RULES_BULK_DISABLE with id · name · before → after per rule (Information level is written) |

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Plan + audit | `ea1c9d9` | docs | Phase 13, 13-01 plan + audit, 13-02 plan |
| Tasks 1–2 | `7a169b7` | feat | RuleListing core, page commands, template, tests |
| Checkpoint fix | `827fdce` | fix | Navigate inside the admin router (no full reload) |
| Checkpoint fix | `57cd7ac` | fix | Child pages render through RoutingContentPlaceholder |
| Checkpoint fix | `f101d60` | fix | Blank row-action header |
| Checkpoint fix | `6b63a7a`, `eb76e5b`, `2ed2b49`, `2132269` | fix | Bulk bar replaces toolbar; date columns drop on narrow tables; column minimums |

All fast-forwarded to `main` (user choice: verify on the host from main); not pushed.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed at checkpoint | 5 | Essential: edit links were broken, layout shift, truncated headers |
| Implementation choices | 6 | Within the plan's intent |
| Deferred | 1 | Checkbox accessible names (npm) |

**Checkpoint fixes (code issues; plan was right):**
1. **Edit/Create "refreshed the page"** — full page loads, and the custom template had no `RoutingContentPlaceholder`, so child pages (`/rules/{id}`, `/rules/create`) never rendered. Fixed with `useNavigate` (react-router-dom is external in Kentico's webpack config) and the placeholder. Verified on the host: edit/create open without reload.
2. **Row-action header showed "rowActions"** — npm Table shows the name for an empty caption → no-break space.
3. **Bulk bar shifted the table** on first selection (misclicks) → bar takes the toolbar's slot at the same minimum height; measured no shift.
4. **Headers truncated / table wider than its area** → date columns dropped below 1,100 px (npm Table ignores `visible`, so columns and cells are filtered), column minimums trimmed.

**Implementation choices (from APPLY):** priority must stay ≥ 1; ChangePriority value parsed server-side from text; `Error` on the bulk result for whole-command refusal; `Succeeded` includes rules already in the target state; sort keeps the selection; no bulk-bar shadow (`--shadow-m` absent in 31.9.1); version token via a new `RuleEditMapping.VersionOf(DateTime)` overload.

**Deferred:** selection checkboxes have no accessible name (npm Table ignores `selectRowLabel`) — KNOWN-LIMITATIONS.

## Skill Audit

All required skills invoked ✓ (design-screen in PLAN and at APPLY start; csharp-refactoring and content-retrieval loaded in the main session and passed to the implementer; design validation done in the browser pane — gaps, headers, overflow, toast, focus not fully exercised: recorded as user-checked).

## Next Phase Readiness

**Ready:** 13-02 (rule edit) can reuse `RuleListing.Of` and the client status mapping; the RoutingContentPlaceholder and useNavigate patterns apply to any custom template with child pages.

**Concerns:** the reports and query-tester links still use full page loads (`window.location.assign`) — 13-03/13-04 should switch them to `useNavigate`.

**Blockers:** None.

---
*Phase: 13-enterprise-screens, Plan: 01*
*Completed: 2026-10-12*
