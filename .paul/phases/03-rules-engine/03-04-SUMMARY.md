---
phase: 03-rules-engine
plan: 04
subsystem: search
tags: [lucene, boostingquery, termsfilter, kentico-progressive-cache, fail-open]

requires:
  - phase: 03-rules-engine (03-03)
    provides: RuleEngine/RuleInput/RuleOutcome, RuleInfo (cache touch + web-farm invalidation), ToDefinition
  - phase: 01-foundation
    provides: SuiteSearchService/SearchExecutor pipeline, DefaultSearchQueryFactory
provides:
  - SuiteSearchRequest.Contexts / ApplyRules; SuiteSearchResponse.FiredRuleIds / UserData / RulesFailed; SuiteRuleUserData
  - SuiteRulesOptions (LuceneSuite:Rules — MaxBoostFactor, CacheMinutes) with startup validation
  - RuleProvider — per-index cached rule loading, invalid rules logged (throttled)
  - Hides (one TermsFilter MUST_NOT) and multiplicative boost/bury (nested BoostingQuery, ≤ 200) in every suite search
  - Fail-open on rule load/evaluate/apply
affects: [03-05 pins, 03-06 rules admin UI (fired rules, RulesFailed), Phase 5 preview (ApplyRules=false), Phase 6 widgets (Contexts)]

tech-stack:
  added: []
  patterns:
    - "SearchWithRules(request, run) — rule orchestration testable without a LuceneIndex (internal ctor)"
    - "RuleOutcome passed to SearchExecutor's constructor; null outcome = byte-identical pre-rules query"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Rules/RuleProvider.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleSearchTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleProviderDatabaseTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite/Search/SearchExecutor.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchService.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchRequest.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchResponse.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptions.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptionsValidator.cs
    - src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs

key-decisions:
  - "Boosts nest one BoostingQuery per boost, capped at the 200 strongest (|strength|), replacing the audit's group-by-strength bound so same-strength boosts stack"
  - "Fail-open fallback reruns the search without rules for any non-ArgumentException on the rules path"
  - "RuleProvider.GetRules virtual (test seam), no new interface"

patterns-established:
  - "Host index probing via a file-based C# script against App_Data/LuceneSearch (read-only, outside the repo)"

duration: ~1.5h
completed: 2026-10-10T23:00:00Z
description: "Stored rules now change every suite search: cached per-index loading, contexts, hides excluded in the query, multiplicative boost/bury, fired rules and user data in the response, fail-open — p95 overhead 1–3 ms"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 3 Plan 04: Search Integration Summary

**A rule saved in the database now changes search results without a deploy. Hidden items vanish from hits, totals and facets; boosts and buries multiply scores; the response names the fired rules and returns their user data; any rule failure falls back to unruled results. Pins come in 03-05.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1.5 h |
| Tasks | 2 auto PASS + 1 checkpoint approved |
| Files | 3 created, 9 modified (+ KNOWN-LIMITATIONS) |
| Tests | 376 pass with DB tests (was 349); 2 opt-in volume tests skipped |
| Rules overhead | p95 1.0–3.0 ms (3 runs; Debug; 5 000 docs, 500 rules, 50 firing) vs budget 10 ms |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Contexts, ApplyRules, rule options | Pass | 5 context validation rows; options defaults, binding and 6 invalid rows naming `LuceneSuite:Rules:*` |
| AC-2: Cached per-index loading, fail safe | Pass | DB test: create/update/delete visible immediately; raw insert stays cached until the dependency is touched; invalid row skipped and named; other index excluded; case-insensitive index name. Load failure → unruled + RulesFailed + one warning |
| AC-3: Fired rules change results | Pass | Hide absent from hits/totals/facets (facet + plain + browse); factor exact ×10 / ×0.1 / ×3.162 (±1e-4); two boosts stack; exact StringField and analyzed TextField matches; browse and bury reorder; rules off / not fired = identical scores; fired ids + userData in run order; contexts; 500 rules × 50 hides × 50 boosts searchable; application failure → unruled; invalid request still throws |
| AC-4: Overhead < 10 ms p95 | Pass | 1.0–3.0 ms |
| Checkpoint | Approved | Host: "coffee" → Coffee Plunger hidden (28 → 27), articles boosted; "tea" unchanged. Temporary rules deleted, host stopped |

## Verification Results

- `dotnet build XperienceCommunity.Lucene.Suite.slnx` → 0 warnings, 0 errors.
- `dotnet test` no DB → 353 passed, 25 skipped; with comm_projects_test → 376 passed, 2 skipped.
- Host log during the checkpoint: no `fail:`/`crit:`, no rule warnings. comm_projects LuceneSuite_Rule back to 0 rows.
- KNOWN-LIMITATIONS: 3 entries (multiplicative-only boost with 200 cap; exact/phrase match only, facet-only fields unboostable; rules fail open).

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Rules/RuleProvider.cs` (59) | Created | IProgressiveCache per index, `lucenesuite.rule|all` dependency, throttled invalid-rule warning |
| `Search/SearchExecutor.cs` | Modified | Context validation; outcome via ctor; nested BoostingQuery per boost (≤ 200); TermsFilter MUST_NOT for hides |
| `Search/SuiteSearchService.cs` | Modified | `SearchWithRules`: evaluate → run → fall back; response rule fields; throttled warnings |
| `Search/SuiteSearchRequest.cs`, `SuiteSearchResponse.cs` | Modified | Contexts, ApplyRules; FiredRuleIds, UserData, RulesFailed, SuiteRuleUserData |
| `Search/SuiteSearchOptions.cs`, `SuiteSearchOptionsValidator.cs` | Modified | `SuiteRulesOptions` + validation |
| `LuceneSuiteServiceCollectionExtensions.cs` | Modified | Register RuleProvider |
| `RuleSearchTests.cs` (388), `RuleProviderDatabaseTests.cs` (105) | Created | Search-path and cache tests, overhead benchmark |
| `SearchExecutorTests.cs`, `AnalyticsOptionsTests.cs` | Modified | Context validation and rule option rows |

## Skill Audit

All required skills invoked ✓ — /dotnet:csharp-refactoring and /kentico-web-development:content-retrieval loaded earlier in this session (03-03 APPLY) and in effect. Admin design skills not applicable (no admin UI change).

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Spec corrections | 2 | Boost bound; host boost field |
| Approach changes | 2 | Test seams |
| Deferred | 0 new | Audit deferrals stand |

1. **Boost bound (spec correction, Task 2).** The audit's group-by-strength bound applied two same-strength boosts once, contradicting AC-3 ("two boosts get both factors"). Implemented one nested `BoostingQuery` per boost, capped at the 200 strongest by |strength| (ties keep field/value order), overflow logged via `BoostsTruncated`. Each level is a two-clause query, so the clause limit cannot be hit. Proven by the same-strength stacking test and the 500 × 50 × 50 test.
2. **Host boost field (checkpoint).** CommunityProjects' `ContentType` is a FacetField only (not term-searchable); the boost used Kentico's stored `ContentTypeName` StringField. Probe also confirmed ItemGuid is stored lowercase "D".
3. **Fail-open test seam.** Lowering `BooleanQuery.MaxClauseCount` also broke the unruled rerun (two-field default query); the test makes the first run throw and the second succeed.
4. **RuleProvider fake.** `GetRules` is virtual; tests subclass it.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| `LuceneIndex` has only an internal ctor | Rule orchestration extracted to `SearchWithRules(request, run)` |
| xunit v2 `ITestOutputHelper` namespace | `using Xunit.Abstractions;` |
| Port 3011 already in use when starting the admin client | Existing dev server reused |

## Next Phase Readiness

**Ready:**
- 03-05 pins: `RuleOutcome.Pins` already resolved by the engine; `SearchWithRules` and the executor are the insertion points; hides are already excluded so pinned-and-hidden can't resurface.
- 03-06: response carries FiredRuleIds, UserData and RulesFailed for the query tester.

**Concerns:**
- Pins must interact with paging and TotalHits; respect-filters needs a per-pin match check against query + facets.
- Boost field names are free text; 03-06 should offer the index's term fields.

**Blockers:** None.

---
*Phase: 03-rules-engine, Plan: 04 · Completed: 2026-10-10*
