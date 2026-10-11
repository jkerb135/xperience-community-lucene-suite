---
phase: 05-preview-sandbox
plan: 01
subsystem: admin-ui
tags: [query-tester, compare, contexts, contact-groups, react]

requires:
  - phase: 03-rules-engine (03-04, 03-07)
    provides: request Contexts, ApplyRules, fired-rule display
  - phase: 04-synonyms (04-02)
    provides: ApplySynonyms, applied-synonym display
provides:
  - QueryTesterArguments.Contexts and PageSize (1–50); never tracked
  - ContactGroups page command (Contact groups View required, recipient lists excluded, 500 cap with Truncated)
  - Query tester Compare mode — Baseline/Tuned columns with own switches and simulated contexts, rank-change markers, missing items
affects: [06-widgets, v0.3 A/B testing (comparison UI ideas)]

tech-stack:
  added: []
  patterns:
    - "Kit breakpoints: md = 1366–1919 px, lg ≥ 1920 px — use colsMd for desktop side-by-side layouts"
    - "--color-text-high-emphasis is the brand purple (#5d0088) in this theme; body text is --color-text-default-on-light"
    - "Cross-app data in Suite commands gated with the Kentico-internal IApplicationPermissionEvaluator against the owning app"
    - "Hit lists as focusable buttons in <ol> (keyboard-reachable, unlike npm Table rows)"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/CompareView.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/ContextPicker.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/compare.ts
    - tests/XperienceCommunity.Lucene.Suite.Tests/QueryTesterDatabaseTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx

key-decisions:
  - "Contact groups visible only with Kentico Contact groups → View (ContactGroupsApplication.IDENTIFIER constant, not a literal)"
  - "Compare columns and the tester toolbar use colsMd so they sit side by side from 1366 px"

duration: ~1 session
completed: 2026-10-11T00:00:00Z
description: "Query tester compare mode: Baseline vs Tuned columns with their own rules/synonyms switches and simulated contexts (contact groups + free text), rank-change markers and missing items"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 5 Plan 01: Query tester compare mode Summary

**Marketers preview, side by side and without a deploy, how rules and synonyms change results for a simulated audience.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1 session (2026-10-11) |
| Tasks | 2 auto + 1 checkpoint, all complete |
| Files | 4 created (3 client, 1 test) + compare.test.ts, 3 modified |
| Tests | .NET 497 → 502 (501 pass in the full run; see perf note); client 29 → 36 |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Contexts + page size | Pass | Request mapping test (contexts, page size, TrackAnalytics false); 0/51 rejected; context-conditioned rules covered by existing 03-04 test |
| AC-2: Contact groups | Pass | Recipient lists excluded, ordered by display name (DB test); Truncated at 500 (test); Contact groups View required (Kentico constant); live: only "Grinder shoppers" listed |
| AC-3: Compare mode | Pass | Two Cards, own latest-request guard, page 1 × 20, states, fired rules/synonyms links, hit buttons → detail panel with focus return |
| AC-4: Contexts per column | Pass | SidePanel Checkbox list (keyboard), Apply/Cancel, focus back to opener, chips with display names, free text with limits |
| AC-5: Rank markers | Pass | ↑/↓/New with aria-labels, missing list, first-occurrence ranking for multilingual duplicates (node tests) |
| AC-6: Design + checkpoint | Pass | 790/1440 px, keyboard pass; user verified a contact-group rule firing only in the simulating column; rule deleted (0 rules, 0 synonyms) |

## Deviations

| Type | Description | Impact |
|------|-------------|--------|
| Finding + fix | Kit breakpoints are md 1366–1919 / lg ≥ 1920 — `colsLg` alone stacked the columns at 1440 px; compare columns and the tester toolbar now set `colsMd` too | Side by side from 1366 px. Earlier `colsLg`-only tweaks (03-07 pin row, 04-02) still only apply from 1920 px — acceptable (they degrade to the stacked layout) |
| Finding + fix | `--color-text-high-emphasis` is the brand purple; compare-mode labels switched to `--color-text-default-on-light` (hit buttons stay purple as clickable) | 03-07 custom-data `<pre>` still purple → KNOWN-LIMITATIONS (outside this plan's boundary) |
| Design | Contact-group query extracted to `LoadContactGroups` for a DB test; identifier from Kentico's `ContactGroupsApplication.IDENTIFIER` | Testable; no drift |
| Test flake | `RulesOverhead_StaysUnderTenMillisecondsAtP95` failed once in the full run (host + dev server + suite in parallel); 3/3 passes in isolation at 7.4–8.8 ms; no search code changed in this plan | Thin margin on shared machines — watch it |

## Skill audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN and APPLY (before markup) |
| Admin UI design validation | ✓ | npm props, tokens (corrected), 790/1440 px, keyboard, states |
| /dotnet:csharp-refactoring | ✓ | Loaded this session |
| /kentico-web-development:content-retrieval | ✓ | ObjectQuery with Columns/OrderBy/TopN for contact groups |

Skill audit: all required skills invoked ✓

## Next Phase Readiness

**Ready:** v0.1 tuning loop complete (rules, synonyms, preview). Phase 6 Widgets package can build on the public search API and ISearchInteractionLogger.

**Concerns:** rules-overhead perf test margin; npm Table rows still not keyboard-reachable in the normal tester view; XpSearch leftovers pending.

**Blockers:** None

---
*Phase: 05-preview-sandbox, Plan: 01*
*Completed: 2026-10-11*
