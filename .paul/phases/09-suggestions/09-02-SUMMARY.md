---
phase: 09-suggestions
plan: 02
subsystem: ui
tags: [javascript, aria-combobox, accessibility, razor, csp]

requires:
  - phase: 09-suggestions
    provides: /lucenesuite/suggest endpoint and ISuiteSuggestions (09-01)
provides:
  - lucene-suite-suggest.js (fetchSuggestions, createSuggestState, enhanceSearchBox, autoInit) + themable lucene-suite-suggest.css
  - Search box opt-in (indexName / widget "Suggestions index", single-index fallback), SuiteSuggestPath, SuggestionStyles
  - Admin Convention 2b right gutter (layout.ts pageStyle, 104 px like native)
affects: [09-03 blocklist admin, 11-public-api-audit, v0.4 instant search]

tech-stack:
  added: []
  patterns:
    - "Progressive enhancement: server renders data-* + hidden listbox/status; script adds ARIA roles; off path byte-identical (golden-tested)"
    - "CSP-safe widgets: no inline script/style; hidden attribute and classes"
    - "Custom admin templates keep the native 104 px right gutter"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Widgets/wwwroot/lucene-suite-suggest.js
    - src/XperienceCommunity.Lucene.Suite.Widgets/wwwroot/lucene-suite-suggest.css
    - src/XperienceCommunity.Lucene.Suite.Widgets/js/lucene-suite-suggest.test.mjs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/layout.ts
  modified:
    - src/XperienceCommunity.Lucene.Suite.Widgets/Components/LuceneSuiteSearchBoxViewComponent.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Views/Shared/Components/LuceneSuiteSearchBox/Default.cshtml
    - src/XperienceCommunity.Lucene.Suite.Widgets/PageBuilder/LuceneSuiteWidgets.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Suggestions/SuiteSuggestEndpoints.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/LuceneSuiteWidgetsServiceCollectionExtensions.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/SearchReportsTemplate.tsx
    - tests/XperienceCommunity.Lucene.Suite.Tests/SuiteWidgetsRenderingTests.cs
    - docs/suggestions.md

key-decisions:
  - "Single-index fallback: no index set + exactly one SuggestionIndexes entry → that index (no host content edit)"
  - "Razor renders data-* even when null → form start tag in two @: branches, closing via Html.Raw (FormTagHelper adds nothing for GET; golden-proven identical)"
  - "User (mid-apply): right gutter = native 104 px (Convention 2b)"

patterns-established:
  - "Measure gutters on painted elements; a padded wrapper still reaches the edge"

duration: ~70min (script/C# delegated to an implementer agent; reviewed, re-verified, host-tested)
completed: 2026-10-12T00:00:00Z
description: "Accessible, CSP-safe autocomplete for the search box (ARIA 1.2 combobox, debounced, latest-wins, textContent only, 429 back-off) as a reusable ES module; opt-in per index with byte-identical HTML when off; live on /site-search with only submitted searches tracked; plus the native 104 px right gutter on custom admin pages"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 9 Plan 02: Search Box Autocomplete Summary

**Visitors on /site-search now get accessible suggestions while typing.** Picking one submits that search, and the box is unchanged anywhere suggestions are off. The script is a reusable ES module for custom UIs.

In the same step, at the user's request, custom admin pages gained the native 104 px right gutter.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Opt-in rendering | Pass | All conditions enforced. Unique ids per request; assets emitted once. Off path byte-identical to pre-change goldens (normal, preview, unlisted, unmapped, ambiguous). The goldens caught a real leak during implementation |
| AC-2: ARIA combobox | Pass | Host: 1 request for 4 fast inputs; 3 options with `<mark>` highlight; polite count; ↓/↑ wrap; Escape ×2; Enter submits the chosen query. Node: stale/abort, cache, errors, 429 back-off, > 100 chars, XSS literal text |
| AC-3: Public JS API | Pass | Four exports (plus MAX_INPUT_LENGTH), JSDoc, no dependencies or globals; docs include a custom-UI example |
| AC-4: Styling | Pass | Themable custom properties; active state ≈ 8.3:1; reduced motion; SuggestionStyles opt-out |
| AC-5: Verification | Pass with gaps | Host as above; SearchEvent 19 → 20 (only the submitted search). Not observed: real OS keyboard/mouse (pane hidden; events dispatched to the real script), clearing the widget dropdown in Page Builder |

## Verification Results

- `dotnet build`: 0 warnings. `npm test` (Widgets js): 41/41. Full .NET suite on the test DB: 683 passed, 5 skipped. Re-run independently.
- No `innerHTML` in the script, no `style=` in the view.
- Right gutter measured (painted elements): Search reports and Query tester 104 px at 669 and 1269 px = native. The page has no horizontal overflow.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Scope addition (user) | 1 | Convention 2b right gutter + layout.ts on the two custom templates; skill updated |
| File placement | 1 | SuiteSuggestPath lives in SuiteSuggestEndpoints.cs (mirrors SuiteEventsPath) instead of its own file |
| Extras | 2 | MAX_INPUT_LENGTH export; --lucene-suite-suggest-active-fg property |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Admin session expired after the host restart | User signed in |
| The pane was hidden, so clicks failed and real keystrokes didn't reach the page | Drove the real script with dispatched input/keydown events; recorded as not observed in #11 |
| The accessibility-tree reader named the input by its placeholder | DOM: `input.labels` = "Search" (correct association) |
| Status text stays "3 suggestions available" after close | Not re-announced; small polish item in #11 |

## Skill Audit

All required skills invoked ✓:
- /dotnet:csharp-refactoring (C# feature changes; binding guidance).

page-builder-widgets is optional and was not invoked. No admin design screen beyond the gutter rule (validated by measurement).

## Next Phase Readiness

**Ready:** 09-03, the blocklist admin (native listing/create/edit + "Block suggestion" report row action). It reuses RowActions, RemoveButton, InlineError, isShown and the Conventions incl. 2b.

**Concerns:** #11 items not observed.

**Blockers:** None

---
*Phase: 09-suggestions, Plan: 02*
*Completed: 2026-10-12*
