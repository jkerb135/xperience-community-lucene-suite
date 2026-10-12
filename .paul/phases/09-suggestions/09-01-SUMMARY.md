---
phase: 09-suggestions
plan: 01
subsystem: api
tags: [suggestions, kentico-module-class, aspnetcore, rate-limiting, caching]

requires:
  - phase: 07-report-data
    provides: SearchDaily rollups and surfaced membership (now the shared Surfacing helper)
  - phase: 06-widgets-package
    provides: Widgets endpoint and limiter patterns
provides:
  - LuceneSuite.BlockedSuggestion module class (installer v6)
  - Public ISuiteSuggestions (Core) and MapLuceneSuiteSuggestions (Widgets) with options
  - Shared Surfacing.Query used by reports and suggestions
affects: [09-02 combobox script, 09-03 blocklist admin, 11-public-api-audit, v0.4 instant search]

tech-stack:
  added: []
  patterns:
    - "Layered packages: Core API first (ISuiteSuggestions), Widgets endpoint as a thin adapter"
    - "Public exposure floor (MinSearches in window) on top of the surfacing threshold for anything shown to anonymous visitors"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Suggestions/BlockedSuggestionInfo.cs
    - src/XperienceCommunity.Lucene.Suite/Suggestions/SuggestionEngine.cs
    - src/XperienceCommunity.Lucene.Suite/Suggestions/SuggestionSource.cs
    - src/XperienceCommunity.Lucene.Suite/Suggestions/ISuiteSuggestions.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Surfacing.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Suggestions/SuiteSuggestEndpoints.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Suggestions/SuiteSuggestRateLimiter.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SuggestionEngineTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SuggestionSourceDatabaseTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SuiteSuggestEndpointTests.cs
    - docs/suggestions.md
  modified:
    - src/XperienceCommunity.Lucene.Suite/Analytics/AnalyticsModuleInstaller.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/SearchReportService.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptions.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptionsValidator.cs
    - src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/LuceneSuiteWidgetsServiceCollectionExtensions.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/AnalyticsInstallerDatabaseTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/AnalyticsOptionsTests.cs

key-decisions:
  - "User: public API (layered packages); new Phase 11 Public API Audit; v0.4 Instant Search after v0.3 Test"
  - "MatchMode stored as text 'exact'/'word'; the blocklist MatchKey is recomputed on load (a stale stored key cannot leak)"
  - "Eligibility filters run in SQL too, so MaxCandidates counts only eligible texts"
  - "maxResults outside 1–20 throws (developer input); the endpoint never passes it"

patterns-established:
  - "Endpoint order: no-store → limiter → opt-in index (404) → input length (400) → API → public max-age"

duration: ~45min (implementation delegated to an implementer agent, reviewed and re-verified)
completed: 2026-10-12T00:00:00Z
description: "Query suggestions: blocklist module class (v6), shared surfacing rule, cached ISuiteSuggestions with a public exposure floor and immediate blocklist effect, and an opt-in, rate-limited, strings-only suggest endpoint; host upgraded and verified by curl"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 9 Plan 01: Suggestions Core and Endpoint Summary

**Sites can now opt an index into as-you-type suggestions in two lines:**
- the C# API (`ISuiteSuggestions`) serves custom UIs;
- the Widgets endpoint (`MapLuceneSuiteSuggestions`) serves browsers.

Suggestions are surfaced queries from the last 90 days that usually return results, minus placeholders and blocklisted terms. They are returned as strings only, and nothing is tracked.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Blocklist model (v6) | Pass | `LuceneSuite.BlockedSuggestion`, CI/CD off, IndexName index. Installer 5 → 6 on the test DB (new upgrade test) and on the host (version key 6, table present) |
| AC-2: Candidates | Pass | One aggregated query over 90 complete UTC days with shared surfaced membership; MinSearches 5 in the window; no-result rate < 0.5; placeholders, browse and NULL excluded; top 2 000. DB tests incl. report parity |
| AC-3: Matching | Pass | Prefix group, then later-word group, by searches. Exact and Word blocks (phrase-aware). 9 engine tests. Accents are not folded (MatchKey), as for synonyms |
| AC-4: API + caching | Pass | Index checked before any query or cache; blocklist changes are visible at once (DB test); no analytics rows; options validated with key names |
| AC-5: Endpoint | Pass | 200 strings / 404 / 400 / 200 [] / 429 + no-store; public max-age configurable (0 → no-store); 15 endpoint tests; host limiter config untouched |
| AC-6: Docs, host, approval | Pass | docs/suggestions.md (setup, eligibility, block latency, ForwardedHeaders, textContent, residual risk); host wired and curl-verified; APPROVAL-CHECKS #10 Pending |

## Verification Results

- `dotnet build`: 0 warnings. Full test suite on comm_projects_test: 674 passed, 0 failed, 5 skipped (opt-in volume/seed tests). Re-run independently after the agent's report.
- Host curl:
  - `co` → 5 suggestions; `bl` → includes the later-word match "t-shirt black";
  - another index 404; 101 chars 400; no q `[]`;
  - `public, max-age=60`.
- Host counts unchanged: SearchEvent 19, SearchDaily 3 389, Interaction 2.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Scope addition | 1 | Existing installer tests updated for v6 (RemoveSuiteObjects list, expected version) plus an upgrade test |
| Signature | 1 | `SuggestionEngine.Match` takes `minInputLength` (the input is measured as a MatchKey) |
| Host | 1 | `Program.cs` needed `using …Widgets.Suggestions` (a namespace per endpoint family; flagged for Phase 11) |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Real `ILuceneIndexManager.GetIndex(name)` case-sensitivity unknown | Same null-check pattern as SuiteSearchService; the endpoint 404s unlisted indexes first |
| The previous host process (b6hhihkr4) exited when the host was restarted | Expected; the new host started in the background |

## Skill Audit

All required skills invoked ✓:
- /dotnet:csharp-refactoring (the Surfacing extraction is behaviour-preserving; the report tests are unchanged);
- /kentico-web-development:content-retrieval (module classes → ObjectQuery).

No admin UI.

## Next Phase Readiness

**Ready:**
- **09-02:** the combobox script over `/lucenesuite/suggest`; strings must be rendered with textContent.
- **09-03:** the blocklist admin, which should fill MatchKey and prevent duplicates.

**Concerns:**
- Seeded host history is now public via suggestions (#10 ↔ #4).
- Rate limiting is per process.

**Blockers:** None

---
*Phase: 09-suggestions, Plan: 01*
*Completed: 2026-10-12*
