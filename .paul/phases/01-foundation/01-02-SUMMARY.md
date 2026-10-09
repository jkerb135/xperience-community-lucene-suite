---
phase: 01-foundation
plan: 02
subsystem: search
tags: [kentico-lucene, lucene-48, facets, drill-sideways, xunit, msbuild]

requires: [01-01 isolated build]
provides:
  - Public search API (ISuiteSearchService, SuiteSearchRequest/Response/Hit/Facet/FacetValue)
  - ISearchQueryFactory + DefaultSearchQueryFactory (per-index field boosts)
  - SuiteSearchOptions, AddLuceneSuite DI registration
  - Internal SearchExecutor (pure Lucene, unit-tested) + SuiteSearchService shell (Kentico wiring)
  - Test project (xunit, 32 tests over in-memory RAMDirectory indexes)
  - Empty Suite Directory.Build.targets (isolation from solution-root targets)
affects: [01-03 Admin application + query tester, Phase 2 event capture, Phase 3 rules hook]

tech-stack:
  added: [xunit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.11.1 (tests only)]
  patterns: [pure executor + thin DI shell, validate before touching Lucene, DrillDownQuery + DrillSideways]

key-files:
  created: [Directory.Build.targets, src/XperienceCommunity.Lucene.Suite/Search/*.cs, src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs, tests/XperienceCommunity.Lucene.Suite.Tests/*]
  modified: [Directory.Packages.props, XperienceCommunity.Lucene.Suite.slnx, src/XperienceCommunity.Lucene.Suite/XperienceCommunity.Lucene.Suite.csproj]

key-decisions:
  - "OR within a facet dimension = one DrillDownQuery.Add(dim, value) per value (repeated Add ORs); Add(dim, values[]) is a path and was the mutation"
  - "50-facet-value limit counts across all dimensions; empty value list for a dimension is rejected"
  - "Range/length violations throw ArgumentOutOfRangeException; other malformed input ArgumentException, paramName \"request\""

duration: ~1 session (APPLY committed 2026-10-08T22:06-04:00)
completed: 2026-10-09T02:07:48Z
---

# Phase 1 Plan 02: Search API + test project Summary

**A suite-owned search API over Kentico Lucene now returns hits, total count, facets and timing, with all Lucene behaviour (boosts, paging limits, browse, OR-within-dimension drill-down with sideways counts, non-faceted indexes, bad ItemGuids) proven by 32 unit tests and one mutation check. The Kentico wiring shell is not yet exercised by a test and carries over to 01-03.**

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Field boosts rank | Pass | `TextSearch_RanksTitleBoostAboveContent_AndReturnsStoredFields`: B (Title, boost 3) above A (Content), TotalHits 2, ItemGuid/score/stored fields asserted |
| AC-2: Paging and limits | Pass | `Paging_ReturnsRequestedSlice_AndFullTotal` (page 3 × 10 of 25 → 5 hits, total 25); `OutOfRangeRequests_AreRejected` and `MalformedRequests_AreRejected` theories cover pageSize 0/101, page 0, window > 1000, page = int.MaxValue × 2 (guard is `(long)Page * PageSize > 1000`), query > 256, > 20 dims, > 50 values, null/empty IndexName, null/empty dim/value, value > 256 |
| AC-2b: Analyzes to nothing | Pass | `QueryWithNoTokensAfterAnalysis_ReturnsNoHits` ("the", "!!!") → 0 hits, no exception |
| AC-3: Empty query browses | Pass | `EmptyQuery_BrowsesWholeIndex` (null/empty/whitespace) → all docs, facet counts over whole index |
| AC-4: Facets count and filter | Pass | `Facets_AreCountedWithoutFilter` (Coffee 3, Tea 2); `FacetValuesWithinOneDimension_AreOred` (5 hits); `FacetFilter_KeepsSidewaysCounts` (3 hits, Tea 2 still shown). Mutation `query.Add(dimension, values.ToArray())` made the OR test fail (5 → 0); restored; `git diff --exit-code -- src/` = 0 |
| AC-5: Non-faceted + bad data | Pass | `NonFacetedIndex_ReturnsEmptyFacets`, `MissingOrInvalidItemGuid_YieldsNullItemGuid`, `FacetFiltersOnNonFacetedIndex_AreRejected` |
| AC-6: Clean and isolated | Pass | Release build 0 warnings / 0 errors; `dotnet test -c Release` 32/32 (re-run by orchestrator); `DirectoryBuildTargetsPath` = Suite's own file for all 3 projects (was solution root); `dotnet restore --locked-mode` exit 0 after commit; xbk scratch build exit 0, 0 errors, 279 warnings (= baseline), xbk sources untouched |

Extra test: `QueryFactory_WithoutConfiguredFields_Throws`.

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: Empty Directory.Build.targets, test packages in CPM, test project in .slnx (/tests/), GenerateDocumentationFile + InternalsVisibleTo | Done | `814d6a3` |
| 2: Public API + SearchExecutor + SuiteSearchService + AddLuceneSuite; no new package deps in src, lock file unchanged | Done (deviations 3–5) | `814d6a3` |
| 3: Tests + mutation check | Done (deviations 1–2) | `814d6a3` |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Mutation used `Add(dim, values.ToArray())` instead of the plan's example (separate Add per value) | The plan's example is the shipped design: repeated Add on one dimension ORs | Mutation still breaks AC-4 OR behaviour, which is what the check needed |
| 2 | Files staged before the mutation | So `git diff --exit-code -- src/` proves the revert against the intended content, not against HEAD | Proof is meaningful |
| 3 | Added `SuiteFacetValue(Label, Count)` record | Plan named SuiteFacet but no value type | One extra public record |
| 4 | Interpretations: 50-value limit counts across dimensions; empty value list rejected; paramName `"request"`; range/length limits → ArgumentOutOfRangeException | Plan was ambiguous | Documented contract; tests pin it |
| 5 | Defensive: null `GetAllDims` results filtered; stored numeric fields formatted with invariant culture; binary fields skipped; hard cast on strategy | Edge cases met while writing the executor | No behaviour change on the happy path |

Skill audit: All required skills invoked ✓ (`dotnet:csharp-refactoring` required, invoked; `dotnet:msbuild` invoked). No gap.

## Carry-over to 01-03 (SHELL_UNTESTED)

`SuiteSearchService` and `AddLuceneSuite` need a Kentico runtime and are not unit-tested. Verify these in the 01-03 checkpoint (query tester against xbk):

1. Unknown index (`GetIndex` returns null) → ArgumentException.
2. Strategy resolved via `GetRequiredService` and cast to the Lucene strategy type.
3. Facet vs plain path chosen by the strategy's `FacetsConfigFactory()`.
4. Index analyzer passed through to the query factory.
5. `Elapsed` measured by the shell's Stopwatch (executor returns Zero; shell overwrites).
6. Request validation runs before `GetIndex`.
7. `AddLuceneSuite` resolves correctly when registered after `AddKenticoLucene`.

## Deferred (from audit)

| Item | Revisit |
|------|---------|
| Strategy + FacetsConfig built per search (twice) | Phase 2 timing data decides on per-index caching |
| Captive scoped dependencies in strategies | Same as upstream; revisit only if a strategy needs scoped services |
| Full stored-field payloads on every hit | After real payload measurements; field allow-list if needed |
| No logging on Lucene failures (exceptions propagate) | Phase 2 event capture brings structured logging |

## Resolved from 01-01

- Solution-root `Directory.build.targets` no longer imports into Suite projects (empty Suite `Directory.Build.targets`).

## Next

Plan 01-03: Admin application + client pipeline + query tester (includes the SHELL_UNTESTED checks above). Phase 1 progress: 2 of 3 plans.

---
*Completed: 2026-10-08 (local)*
