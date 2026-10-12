---
phase: 12-public-api-audit
plan: 01
subsystem: api
tags: [public-api, PublicApiAnalyzers, refactoring, documentation]

requires:
  - phase: 09 / 10 / 11
    provides: the public surface added in v0.2 (suggestions, activities, insights)
provides:
  - 12-01-INVENTORY.md (163 → 147 public types, classified)
  - PublicAPI.Shipped/Unshipped.txt per package, enforced by PublicApiAnalyzers 5.6.0
  - SearchClick record (LogClick(SearchClick)); MapLuceneSuite* in XperienceCommunity.Lucene.Suite.Widgets
  - docs/public-api.md (supported boundary, framework-required, internal by design, other public surfaces)
affects: [v0.3 Test, v0.4 Instant Search, first NuGet release]

tech-stack:
  added: [Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0 (analyzer only, PrivateAssets=all)]
  patterns:
    - "Every public change is a PublicAPI.Unshipped.txt diff; move to Shipped at release"
    - "Internalize only after a host runtime smoke check (Kentico discovers types by reflection)"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Analytics/SearchClick.cs
    - src/*/PublicAPI.Shipped.txt, src/*/PublicAPI.Unshipped.txt
    - docs/public-api.md
    - .paul/phases/12-public-api-audit/12-01-INVENTORY.md
  modified:
    - Directory.Packages.props, the three src csproj
    - src/XperienceCommunity.Lucene.Suite/Analytics/ISearchInteractionLogger.cs, SearchInteractionLogger.cs
    - src/XperienceCommunity.Lucene.Suite/Rules/*, Synonyms/* (visibility)
    - src/XperienceCommunity.Lucene.Suite.Widgets/Events/SuiteEventsEndpoints.cs, Suggestions/SuiteSuggestEndpoints.cs, Events/SuiteClientSettings.cs
    - docs/analytics-client-contract.md, activities.md, getting-started.md, samples.md (changed code lines)
    - F:/Personal/CommunityProjects/src/Program.cs (host, usings only)

key-decisions:
  - "User: PublicApiAnalyzers baseline; admin services stay internal; pre-1.0 breaking cleanups now"
  - "Rule/synonym JSON models internal (no external use; stored JSON is the contract); SiteSearchPermissions Supported"
  - "SuiteResultReference stays in Widgets.Events (not exposed by the moved endpoints)"

patterns-established:
  - "Host-file edits are re-verified byte-for-byte against the backup, not trusted from a report"

duration: ~60min (implementation delegated; host CRLF fix, smoke check and cleanup by me)
completed: 2026-10-12T00:00:00Z
description: "Public API audit: 17 unnecessary public types internalized (163 → 147), LogClick(SearchClick) and one Widgets namespace, PublicApiAnalyzers baseline proven to fail on undeclared changes, and docs/public-api.md defining the supported boundary — host smoke-checked after restart"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 12 Plan 01: Public API Audit Summary

**The Suite's public API is now intentional and frozen.** Every public type is classified, 17 that nobody outside needed are internal, and any future public change fails the build until it is declared.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Inventory + classification | Pass | Reflected count = inventory count. Surface only shrank (+SearchClick). Customization types (view models, widget properties, options) stay public. Host smoke check after restart passed |
| AC-2: SearchClick | Pass | Same validation and exceptions; LogConversion unchanged; tests changed call shapes only. New: `LogClick(null)` → ArgumentNullException |
| AC-3: One Widgets namespace | Pass | Host compiles with one using; routes unchanged (events 202, suggest 200, settings 200) |
| AC-4: Baseline enforced | Pass | Analyzer 5.6.0 matches Roslyn 5.6 / SDK 10.0.303; it produced 1016 RS0016 errors before generation; RS0016 proof recorded; test project and host unaffected |
| AC-5: Documented boundary | Pass | public-api.md: Supported tables, Framework-required note, Internal by design, Other public surfaces (endpoints, contract, JS exports, URL parameters, data-*, CSS, settings keys), Changing the API, links (front matter and links checked) |

## Verification Results

- Build 0 warnings with the analyzer active.
- .NET: 797 passed + 1 timing flake in one full run (RulesOverhead: 3/3 passed alone; the same flake rate at HEAD, per the implementer's worktree check); 798 in the implementer's last run.
- Client 77/77; Widgets js 45/45.
- Host: rebuilt and restarted. Every Suite admin page loaded, including the rule edit page with its stored values; /site-search, suggest, settings and the events endpoint all answer.
- Smoke-test rows removed: 1 fake click and 2 curl searches.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Defect caught in review | 1 | The implementer's host Program.cs edit lost CRLF (reported as preserved). I restored it and verified it byte-identical to the backup except the usings |
| Scope addition | 1 | Search-page URL parameters added to "Other public surfaces" (real customer-facing surface) |
| Behaviour note | 1 | `LogClick(null)` now throws ArgumentNullException (it was impossible before) |
| Test-only | 1 | Rule test theories take `object` parameters (public test methods can't take internal types) |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Loaded in session; its rules (binding-aware edits, contracts beyond C#) were passed to the implementer and enforced by the smoke check |
| /dotnet:msbuild | ○ (optional) | Analyzer wiring was simple; not needed |
| update-docs | ✓ | Run at UNIFY (docs commit) |

## Next Phase Readiness

**Ready:** v0.2 is feature-complete. The milestone review covers APPROVAL-CHECKS #4–#15 (open questions: #12 audit trail, #13 click text, #14 ranking + cold latency, #15 internalized types).

**Concerns:**
- RulesOverhead timing test flakes (pre-existing; decide on a CI budget).
- WebPageItemInfo (internal Kentico API) still used for click page links (KNOWN-LIMITATIONS).
- No binary-compat check until a release exists.

**Blockers:** None

---
*Phase: 12-public-api-audit, Plan: 01*
*Completed: 2026-10-12*
