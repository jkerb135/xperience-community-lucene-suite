---
phase: 06-widgets-package
plan: 02
subsystem: ui
tags: [aspnetcore, view-components, razor-class-library, live-site, analytics, contact-groups]

requires:
  - phase: 06-widgets-package
    provides: events endpoint, SuiteResultReference, lucene-suite.js (06-01)
  - phase: 02-event-capture
    provides: server-minted SearchId, rollup de-duplication of (SearchId, Sequence)
provides:
  - LuceneSuiteSearchBox / LuceneSuiteResults / LuceneSuiteFacets view components with overridable default views
  - SuitePageSearch (one search per index per request, URL-driven) + SuiteSearchUrl
  - Contact-group contexts for visitors (never creates a contact)
  - lucene-suite-results.js click beacons
affects: [06-03 Page Builder widgets + host wiring, v0.2 reports]

tech-stack:
  added: []
  patterns: [URL-only search state with sid/seq continuation, Sec-Fetch-Site gating, response marked uncacheable when a search runs, href allow-list]

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Widgets/Search/SuitePageSearch.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Search/SuiteSearchUrl.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Search/SuiteContactGroupContexts.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Components/LuceneSuiteResultsViewComponent.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Views/Shared/Components/LuceneSuiteResults/Default.cshtml
    - .paul/APPROVAL-CHECKS.md
  modified:
    - src/XperienceCommunity.Lucene.Suite.Widgets/LuceneSuiteWidgetsServiceCollectionExtensions.cs
    - docs/analytics-client-contract.md

key-decisions:
  - "SuitePageSearch is the one public service (internal ctor, factory registration) — view components need public constructor types"
  - "Human-verify checkpoints deferred to .paul/APPROVAL-CHECKS.md, reviewed at milestone end (user)"

patterns-established:
  - "Razor always renders data-* attributes even when null; JS must treat empty values as absent"
  - "Kentico Lucene Url field holds WebPageUrl.RelativePath (~/…) → Url.Content before validating"
  - "IProgressiveCache loaders set CacheDependency only when settings.Cached (testable without Kentico init)"

duration: ~2h
completed: 2026-10-11T00:00:00Z
description: "Server-rendered search box, results and facets view components sharing one URL-driven search per request, with safe tracked result links, toggle-link facets, contact-group contexts and click beacons"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 6 Plan 02: Server-rendered Search Components Summary

**Three `<vc:lucene-suite-…>` components render a tracked search page with no client framework. They share one search per index per request, which is driven by `q`/`page`/`f.{dim}`/`sid`/`seq`. Result links are allow-listed and, when they stay on the site, carry `sr=` and click beacons. Facets are toggle links that always stay removable. Visitor contact groups are sent as rule contexts.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~2 h |
| Completed | 2026-10-11 |
| Tasks | 2 auto completed; checkpoint deferred to APPROVAL-CHECKS.md #2 |
| Files | 14 created, 5 modified |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: URL model | Pass | Parse never throws. Limits are enforced: q ≤ 256 (longer is invalid), page within the 1000 window, 10 dimensions × 20 values, sid + seq 1–10 000. The round trip survives `&`, `=`, spaces, non-ASCII and dotted dimensions. Toggle links add or remove a value and reset to page 1; continuation links carry seq + 1. |
| AC-2: One search per request | Pass | Memoized in HttpContext.Items. Sec-Fetch-Site: same-origin or a missing header continues the search; none, same-site and cross-site mint a new one. Submit = no continuation + non-empty q. ArgumentException → Invalid; other failures → Unavailable + throttled error; an aborted request propagates. An unknown index throws InvalidOperationException. `no-store` and output-cache storage off only when a search runs. |
| AC-3: Contact-group contexts | Pass (unit) | Filtering, the limit of 50 and fail-safe behaviour are tested through stubbed contact/query seams. The Kentico query (ICurrentContactProvider.GetExistingContact + one WhereIn query) is not yet checked against a DB; that is carried to the 06-03 host checkpoint (APPROVAL-CHECKS #2 item 7). |
| AC-4: Default views | Pass | Rendering tests cover: the labelled GET form; status line; `<ol start>`; `sr=` appended before `#frag`; `javascript:`, `data:`, `//` and `/\` unlinked; external https linked without tracking; same-host absolute tracked; HTML encoding of hostile values; pagination aria-current with `rel=nofollow`; selected-but-missing facets at count 0; the beacon script only with events mapped. The script tag has `asp-append-version`. |
| AC-5: Click beacons | Pass | `clickPayload` is node-tested for valid input and 9 invalid cases. Click and middle-click (auxclick button 1) are delegated on document; preventDefault is never called. |

## Verification Results

```
dotnet build XperienceCommunity.Lucene.Suite.slnx  → 0 Warning(s), 0 Error(s)
dotnet test (with test DB)                          → Passed 574, Skipped 3 (opt-in env), Failed 0
  SuiteSearchUrlTests 28 · SuiteWidgetsRenderingTests 10 (new)
npm test (Widgets/js)                               → 27 pass
dotnet pack                                         → staticwebassets: lucene-suite.js, lucene-suite-results.js; views compiled into the DLL
```

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Widgets/Search/SuiteSearchUrl.cs` | Created (150) | URL model + link builders |
| `Widgets/Search/SuitePageSearch.cs` | Created (161) | Shared per-request search, statuses, caching control |
| `Widgets/Search/SuiteContactGroupContexts.cs` | Created (74) | Cached contact-group code names |
| `Widgets/Components/*ViewComponent.cs` | Created (26/115/56) | Search box, results (safe links), facets + view models |
| `Widgets/Views/**` | Created | `_ViewImports` + three default views |
| `Widgets/wwwroot/lucene-suite-results.js`, `js/lucene-suite-results.test.mjs` | Created (34, 40) | Click delegation + tests |
| `tests/…/SuiteSearchUrlTests.cs`, `SuiteWidgetsRenderingTests.cs` | Created (300, 226) | URL, page search, contexts; MVC rendering |
| `.paul/APPROVAL-CHECKS.md` | Created | Deferred approval review document |
| `Widgets/LuceneSuiteWidgetsServiceCollectionExtensions.cs` | Modified | Options (PageSize, SearchOnEmptyQuery, ContactGroupContexts) + registrations |
| `Widgets/Events/SuiteEventsEndpoints.cs` | Modified | Records the mapped path (SuiteEventsPath) |
| `Widgets/…Widgets.csproj` | Modified | AddRazorSupportForMvc |
| `docs/analytics-client-contract.md` | Modified | Server-rendered components section; repaired two cp1252 `§` bytes written by 06-01 |

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| `SuitePageSearch` public (internal ctor, factory DI) | View components need public constructor types; also usable by custom pages | Public surface, listed in APPROVAL-CHECKS #2 |
| `ICurrentContactProvider.GetExistingContact()` instead of static `ContactManagementContext` | Injectable, never creates a contact | Same behaviour as planned |
| Search box takes no index name | It never searches | Simpler signature |
| Checkpoints → APPROVAL-CHECKS.md (user) | One batch review at milestone end | Applies to all later plans |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 3 | Essential |
| Scope additions | 1 | Encoding repair of 06-01 doc bytes |
| Deferred | 2 | Contact-group DB check; checkpoint review |

### Auto-fixed Issues

1. **`page` is a reserved Razor directive:** the loop variable was renamed.
2. **Razor renders null `data-*` attributes:** the test now expects an empty `data-lucene-suite-events`, and the JS ignores empty values.
3. **CacheHelper needs Kentico initialisation:** the dependency is set only when `settings.Cached`, which is the standard Kentico idiom. This also keeps the loader testable.

### Other deviations

- **One-search-per-request rendering test:** a single request runs the search and then renders facets, rather than a two-component Razor page. The memo itself is unit-tested.
- **Checkpoint deferred to `.paul/APPROVAL-CHECKS.md` #2** (user instruction).

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| 06-01 doc edit written with the Windows default encoding left two invalid UTF-8 bytes (committed in 3a7bc95) | Repaired; scanned every tracked text file (all valid); scripts now always pass `encoding='utf-8'` |
| Heredoc quoting broke a long inline Python edit | Moved to a script file |

## Skill Audit

/dotnet:csharp-refactoring ✓ and /kentico-web-development:content-retrieval ✓ (both invoked this session; the content-retrieval references were used for the ObjectQuery + cache dependency).

## Next Phase Readiness

**Ready:**
- 06-03 can register Page Builder widgets that wrap the three view components, wire the CommunityProjects host (`AddLuceneSuiteWidgets`, `MapLuceneSuiteEvents`, a search page) and run end-to-end with real traffic.

**Concerns:**
- The contact-group query and a real-browser pass are still unverified (06-03).
- Facet headings show raw dimension names (open question in APPROVAL-CHECKS #2).

**Blockers:** None

---
*Phase: 06-widgets-package, Plan: 02*
*Completed: 2026-10-11*
