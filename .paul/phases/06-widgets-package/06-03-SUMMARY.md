---
phase: 06-widgets-package
plan: 03
subsystem: ui
tags: [kentico, page-builder, widgets, preview, live-site, end-to-end]

requires:
  - phase: 06-widgets-package
    provides: view components, SuitePageSearch, events endpoint, lucene-suite.js (06-01, 06-02)
provides:
  - Three Page Builder widgets (search box with page selector, results, filters with dimension labels)
  - Preview/edit-mode safety (never tracked, fail-closed detection, inert search box in preview)
  - Contact-group membership query proven on a database
  - CommunityProjects wiring + permanent "Site search" page with verified real traffic
affects: [v0.2 reports (real traffic now flowing), milestone v0.1 review]

tech-stack:
  added: []
  patterns: [widgets as thin wrappers sharing component model builders and view paths, ISuitePreviewState seam over IWebsiteChannelContext + IPageBuilderDataContextRetriever]

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Widgets/PageBuilder/LuceneSuiteWidgets.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/PageBuilder/LuceneIndexOptionsProvider.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Search/SuitePreviewState.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/ContactGroupContextsDatabaseTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Widgets/Search/SuitePageSearch.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Components/*.cs (shared Build methods)
    - docs/analytics-client-contract.md

key-decisions:
  - "Search box disabled in all Kentico preview (signed preview URLs make submitted queries 404), not only edit mode"
  - "Host page 'Site search' at /site-search; XpSearch 'Search' page left untouched (user)"
  - "Host landing-page area allow-list extended with the XperienceCommunity.LuceneSuite. prefix"

patterns-established:
  - "Package widget assemblies need [assembly: AssemblyDiscoverable] — test asserts it"
  - "RegisterWidgetAttribute keeps types internally: assert registrations via GetCustomAttributesData"
  - "Kentico preview URLs are hash-signed: GET forms/links that change the query fail inside preview"
  - "Host file edits: read/write bytes to keep BOM + line endings; unique scratch script names"

duration: ~3h
completed: 2026-10-11T00:00:00Z
description: "Page Builder widgets for search box, results and filters on top of the 06-02 components, preview/edit never tracked, contact-group query DB-proven, and a live CommunityProjects Site search page with end-to-end analytics verified"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 6 Plan 03: Page Builder Widgets + Host End-to-End Summary

**Marketers can now build a tracked search page from three Lucene Suite widgets without writing code. The CommunityProjects "Site search" page recorded a real submit, then a page change, then a facet change and a result click, all under one search id. Preview and edit mode added nothing to analytics.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~3 h (incl. host work) |
| Completed | 2026-10-11 |
| Tasks | 2 auto completed; checkpoint deferred to APPROVAL-CHECKS.md #3 |
| Files | 4 created, 12 modified (+ 4 host files, untracked) |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Widgets registered and configurable | Pass | `AssemblyDiscoverable` is present and the 3 registrations (identifiers, view components, property types, `AllowCache = false`) are asserted. The host Management API lists the widgets with the expected property schemas. A page-selector page resolves to its URL; a deleted page or no page falls back to the current page. An empty or missing index shows a message in edit mode and renders nothing live. The widgets share the components' views. |
| AC-2: Preview and edit mode never tracked | Pass | Unit tests: preview → `TrackAnalytics = false` with no contexts; unreadable preview state is treated as preview. Rendering tests: no `sr=`, data attributes or script in preview; edit-mode placeholders. On the host, Page Builder read-only and edit mode added 0 rows; edit mode showed the placeholders and a disabled search box (real Kentico detection). The search box is disabled throughout preview (deviation, see below). |
| AC-3: Facet dimension labels | Pass | The parser has 4 cases (blank lines, missing `=`, extra `=`, last value wins). The heading shows the label while links keep the dimension name. On the host the heading reads "Content type". |
| AC-4: Contact-group query on a DB | Pass | `comm_projects_test`: a contact in a group, a recipient list and another contact's group → only the group is returned. Cleanup was verified (0 rows left). |
| AC-5: Host end-to-end | Pass | Search id `bdefc913-877e-4c5d-87b5-76eea755c16c`: SearchEvent sequences 1/2/3 (only seq 1 IsSubmit, page 2, 1 filter), plus a SearchInteraction click on seq 3 at position 2. The beacon got a 202 and the target URL ends `sr=…3.2`. Responses sent `no-store`. No Suite errors in the host log. |

## Verification Results

```
dotnet build (Suite)                         → 0 warnings, 0 errors
dotnet test (with test DB)                   → Passed 588, Skipped 3 (opt-in), Failed 0
npm test (Widgets/js)                        → 27 pass
dotnet pack                                  → staticwebassets: lucene-suite.js, lucene-suite-results.js
Host dotnet build                            → 0 errors (6 pre-existing warnings from host/other libraries)
Management API get_page_builder_components   → 3 XperienceCommunity.LuceneSuite.* widgets listed
```

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Widgets/PageBuilder/LuceneSuiteWidgets.cs` | Created | `AssemblyDiscoverable`, 3 `RegisterWidget` attributes, properties classes, widget view components, missing-index handling |
| `Widgets/PageBuilder/LuceneIndexOptionsProvider.cs` | Created | Index drop-down |
| `Widgets/Search/SuitePreviewState.cs` | Created | Preview/edit seam + Kentico implementation |
| `Widgets/Search/SuitePageSearch.cs` | Modified | Preview read once per request (fails closed), `TrackAnalytics`/contexts off in preview, `IndexExists`, `IsPreview`/`IsEditMode` on the result |
| `Widgets/Components/*.cs` | Modified | Shared `Build` methods + view paths; facet labels; search box preview flags |
| `Widgets/Views/**` | Modified | Placeholders, label headings, disabled fieldset in preview |
| `Widgets/LuceneSuiteWidgetsServiceCollectionExtensions.cs` | Modified | Registers `ISuitePreviewState` |
| `tests/…/SuiteSearchUrlTests.cs`, `SuiteWidgetsRenderingTests.cs` | Modified | Preview, fail-closed, labels, registration and widget-wrapper tests |
| `tests/…/ContactGroupContextsDatabaseTests.cs` | Created | Membership query on `comm_projects_test` |
| `docs/analytics-client-contract.md` | Modified | Widgets section, preview behaviour, caching warning |
| `.paul/APPROVAL-CHECKS.md` | Modified | Entry #3 |
| Host (untracked): `CommunityProjects.csproj`, `Search/SearchStartupExtensions.cs`, `Program.cs`, `Helpers/AreaRestrictionHelper.cs` | Modified | Widgets reference, `AddLuceneSuiteWidgets`, `MapLuceneSuiteEvents`, landing-page widget allow-list. Backups are in `project-sidecar/backups/widgets-2026-10-11`. |
| Host content (`comm_projects`) | Created | "Site search" landing page (`e13638b9-f089-4a73-bb17-74f1f2d1e9ac`, /site-search) with 2 sections and 3 widgets, published |

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Disable the search box in all preview | Kentico validates a hash of preview URLs; a submitted query 404s inside the frame | Marketers try searches on the live page or in the query tester |
| New "Site search" page (user) | Existing "Search" page is XpSearch's and full of dead widgets | XpSearch page stays in the pending cleanup |
| Extend the host landing-page area allow-list | The Dancing Goat template only allows widgets by prefix | Recorded as host edit #4 (backed up) |
| Widgets resolve preview state through `ISuitePreviewState` from request services | Kentico preview services are request-bound; tests need a seam | Unreadable state fails closed |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 3 | Essential |
| Scope additions | 2 | Host allow-list edit; preview-wide disabled search box |
| Deferred | 0 | — |

### Auto-fixed Issues

1. **Host files' line endings converted (CRLF → LF) by the first edit:** restored from backups and redone with byte-preserving edits. The diff now shows only the added lines.
2. **Stale scratch script ran** (an existing `C:\tmp\t3b.py`): it aborted on its first assertion before writing anything, and I verified that no file changed. Scripts now use unique `w603_*` names.
3. **`RegisterWidgetAttribute` exposes no type properties:** the registration test reads constructor arguments through `GetCustomAttributesData`.

### Other deviations

- **Page:** the plan's "Search" page at `/search` became "Site search" at `/site-search` (user decision on the existing XpSearch page).
- **Preview search box:** disabled throughout preview, not only in edit mode (AC-2 extended).
- **`LuceneSuiteFacetGroup`:** gained a positional `Label` (06-02 record, unreleased).
- **Widget view component names:** `LuceneSuite*Widget`.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Landing-page template refused the widgets (prefix allow-list) | Host `AreaRestrictionHelper` prefix added (backed up) |
| Kentico preview URL hash validation 404 on search submit | Search box disabled in preview |
| `curl` probes recorded as searches (not a bot UA) | Kept as test traffic (user: keep host traffic); noted in APPROVAL-CHECKS #3 |

## Skill Audit

- /dotnet:csharp-refactoring ✓
- /kentico-web-development:content-retrieval ✓ (ObjectQuery + cache dependency in 06-02; DB test here)
- /kentico-web-development:page-builder-widgets ✓ (docs fetched in PLAN)

## Next Phase Readiness

**Ready:**
- Phase 6 and milestone v0.1 are complete in code. The test host produces real Suite traffic for v0.2 reports.

**Concerns:**
- APPROVAL-CHECKS #2 and #3 are pending your milestone review (open questions: facet value labels, host edits).
- Facet values are raw code names on the host.

**Blockers:** None

---
*Phase: 06-widgets-package, Plan: 03*
*Completed: 2026-10-11*
