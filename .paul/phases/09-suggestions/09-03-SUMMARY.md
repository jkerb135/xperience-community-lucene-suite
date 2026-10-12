---
phase: 09-suggestions
plan: 03
subsystem: ui
tags: [kentico-admin, listing-page, page-command, suggestions, accessibility]

requires:
  - phase: 09-suggestions
    provides: BlockedSuggestionInfo (v6), blocklist cache dependency, ISuiteSuggestions (09-01)
  - phase: 08.1-admin-ui-consistency-audit
    provides: listing-style RowActions, NotificationBar region, Suite admin UI Conventions
provides:
  - Native Blocked suggestions listing + create + delete per index (Manage synonyms)
  - SearchReportsPage.BlockSuggestion page command + 4th query-table row action
  - Internal BlockedSuggestionValidation shared by both write paths
affects: [10-custom-activities, 11-public-api-audit]

tech-stack:
  added: []
  patterns:
    - "One-click page command from a report row with a Kentico success toast; errors only in the page's NotificationBar"
    - "Never put request state in a dependency of Table ComponentCell rows: rebuilt cells remount the pressed button and drop focus; guard re-entry with a ref"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Suggestions/BlockedSuggestionEditModel.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/BlockedSuggestionListingPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/BlockedSuggestionCreatePage.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/BlockedSuggestionAdminTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/BlockedSuggestionAdminDatabaseTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchReportsPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/QueryTable.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/SearchReportsTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/report.ts
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/report.test.ts
    - tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs
    - docs/suggestions.md

key-decisions:
  - "User: one-click direct block from the report (supersedes the 08-02 handoff choice for this action)"
  - "User: no edit page; delete and re-add"
  - "Block button only in the query table, not Needs attention"
  - "Kentico LogEvents writes no event-log rows for Suite module classes in 31.9.1 → no audit trail (AC-5 failed; decision pending in APPROVAL-CHECKS #12)"

patterns-established:
  - "Every routed Suite page overrides ValidatePage (now enforced by EveryRoutedSuitePage_ValidatesItsIndex)"

duration: ~75min (Tasks 1–2 delegated to an implementer agent; reviewed, fixed and re-verified)
completed: 2026-10-12T00:00:00Z
description: "Marketers can block suggestions per index from a native Blocked suggestions page or with one click on a Search reports row; the effect on the public suggest endpoint is immediate and verified on the host"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 9 Plan 03: Blocked Suggestions Admin Summary

**Marketers with Manage synonyms can now keep a query out of an index's suggestions:**
- one click on a Search reports row (exact match);
- a native Blocked suggestions page (exact or contains-words).

Delete undoes the block. Reports are unaffected.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Listing per index | Pass | Native listing filtered by index; Create and Delete only with Manage synonyms; Delete checks index ownership in SQL. Host: columns, header action, native delete confirmation |
| AC-2: Create with validation | Pass | Trim, non-empty MatchKey, ≤ 100 (text and key), placeholders refused, duplicate per index + key + mode refused, other mode allowed; own ValidatePage; Submit/Change on Manage synonyms. Host: punctuation and duplicate refused, word entry stored |
| AC-3: One-click block | Pass | Toast with undo hint; suggest endpoint dropped the text at once; repeat = "already blocked"; disabled with reasons for browse/placeholder/permission; server re-validates, index from the page. In-flight: re-entry ignored rather than disabled (deviation 2) |
| AC-4: No regressions, native look | Pass | Build 0 warnings; .NET 708 passed / 5 skipped; client 70/70, tsc clean. 4 icons on one line, 50 px rows at 1100/1700 px, no page overflow. Host counts unchanged; test entries deleted |
| AC-5: Who blocked what can be reconstructed | **Fail** | No event-log rows for blocklist creates/deletes (nor for any synonym/rule change since 10-10). Recorded in KNOWN-LIMITATIONS; decision in APPROVAL-CHECKS #12 |

## Verification Results

- `dotnet build XperienceCommunity.Lucene.Suite.slnx`: 0 warnings, 0 errors.
- `dotnet test` on comm_projects_test: 708 passed, 0 failed, 5 skipped. I ran this myself after the implementer's report.
  - The implementer's first full run had one failure: `RulesOverhead_StaysUnderTenMillisecondsAtP95`, the known timing flake.
- Client `npm test`: 70/70. `tsc --noEmit` exit 0.
- Host E2E on comm_projects, with the host rebuilt and restarted:
  - block "colombia carlos imbachi" → `/lucenesuite/suggest?q=co` drops it;
  - "Plunger" (words) → drops "coffee plunger";
  - deleting everything → baseline restored.
- Host counts before and after: SearchEvent 20, SearchDaily 3 389, Rule 0, Synonym 0, BlockedSuggestion 0.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 2 | Focus loss and double error display found on the host; both fixed |
| Spec adjustment | 1 | No in-flight disabled state (ref guard instead); KNOWN-LIMITATIONS |
| Failed AC | 1 | AC-5 (audit trail): platform does not log; plan's fallback applied |

1. **Errors shown twice.** The server added an error toast and the client also showed the NotificationBar. The server now sends only success toasts.
2. **Keyboard focus lost after Block.** The implementer's in-flight `blocking` state was a `useMemo` dependency of the table rows. Rebuilt ComponentCells remounted the pressed button, so focus fell to `<body>`.
   - Fix: `blocking` became a ref in the template that ignores re-entry, and the button is no longer disabled while a request runs.
   - Host: focus stays on the button after Enter; the toast appears in 87 ms.
3. **AC-5.** Kentico 31.9.1 writes no object events for these module classes. The host event log is also capped at 1000 rows and flooded by leftover XpSearch macro errors.
4. **Implementer choices kept:**
   - `Icons.BanSign` for the menu;
   - the RowActions prop `onBlock` (permission via the existing `canManageSynonyms`);
   - placeholder check case-insensitive;
   - actions column 11 → 15 units;
   - registration test `EveryRoutedSuitePage_ValidatesItsIndex`, mutation-checked.

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN and APPLY (before markup) |
| Admin UI design validation | ✓ | Two widths, measurements, DOM labels, focus, states; the accessibility-tree reader misreports native labels (as in 09-02) |
| /dotnet:csharp-refactoring | ✓ | Invoked; classified as feature work (not a refactor), so no refactoring was needed |
| /kentico-web-development:content-retrieval | ✓ | ObjectQuery for module classes confirmed |

## Next Phase Readiness

**Ready:**
- Phase 9 is complete:
  - suggestions core and endpoint (09-01);
  - autocomplete (09-02);
  - blocklist admin and report action (09-03).
- Phase 10 (custom activities) can reuse the capture pipeline. Phase 11 should list `BlockSuggestionArguments` and `BlockSuggestionResult` (public DTOs on the admin page) and the absent public blocklist API.

**Concerns:**
- There is no audit trail for Suite admin changes (rules, synonyms, blocklist). Your decision is pending in #12.
- Approval checks #4–#12 are pending milestone review.

**Blockers:** None

---
*Phase: 09-suggestions, Plan: 03*
*Completed: 2026-10-12*
