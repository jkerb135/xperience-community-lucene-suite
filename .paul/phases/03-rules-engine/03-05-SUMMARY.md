---
phase: 03-rules-engine
plan: 05
subsystem: search
tags: [lucene, pins, paging, fieldcache, multilingual]

requires:
  - phase: 03-rules-engine (03-04)
    provides: SearchWithRules, SearchExecutor outcome via ctor, hides, boosts, fail-open
  - phase: 03-rules-engine (03-03)
    provides: ResolvedPin (position, item, respectFilters, ruleId) with conflicts resolved
provides:
  - Pins placed at global 1-based positions across pages (PinMerger)
  - Pinned items in the match set (zero-weight, coord-disabled clause) → counted in TotalHits and facets
  - One hit per pinned ItemGuid in multilingual indexes; TotalHits equals the pageable length
  - SuiteSearchHit.PinnedByRuleId
affects: [03-06 rules admin UI (pinned indicator, pin picker), Phase 5 preview]

tech-stack:
  added: []
  patterns:
    - "Membership in the pinned-document set (doc ids) instead of per-document ItemGuid reads"
    - "ItemGuid via FieldCache.DEFAULT.GetTerms per segment"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Search/PinMerger.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/PinMergerTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite/Search/SearchExecutor.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchResponse.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleSearchTests.cs

key-decisions:
  - "Pinned documents in the window are found by doc-id membership; ItemGuids read through FieldCache (performance: 20.3 → ≈8 ms p95 with 300 pins)"
  - "Sideways counts of a filtered dimension include filter-ignoring pins like any match (drilled-down counts don't)"

duration: ~1.5h
completed: 2026-10-11T00:30:00Z
description: "Pins land at their global positions across pages, count in totals and facets, respect facet filters per rule, appear once per item in multilingual indexes, and name their pinning rule — p95 ≈ 8 ms with 300 pins"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 3 Plan 05: Pins Summary

**Pinned items now appear at their configured positions in suite search, on whichever page that falls, counted in totals and facets, once per item even in multilingual indexes, with the pinning rule on each hit. Every rule action now affects search results.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1.5 h |
| Tasks | 2 auto PASS + 1 checkpoint approved |
| Files | 2 created, 3 modified (+ KNOWN-LIMITATIONS) |
| Tests | 394 pass with DB tests (was 376); 2 opt-in volume tests skipped |
| Rules overhead | p95 7.5–8.3 ms (3 runs; Debug; 5 000 docs, 500 rules, 50 firing, 300 pins) vs budget 10 ms |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Global positions across pages | Pass | PinMerger 9 cases; integration: pin at 12 → page 2 hit 2, page 1 all organic |
| AC-2: Availability by filter setting | Pass | Non-matching item pinned; respect-filters + failing → dropped; ignore-filters → shown; missing guid ignored; hidden handled by engine |
| AC-3: Totals/facets, unchanged scores, multilingual, invariant | Pass | Total +1 and Food facet counted for a pinned non-match; unpinned scores/order identical; two language docs → one hit; concatenated pages = TotalHits with no repeats (5 scenarios); zero-organic query → only the pin, total 1 |
| AC-4: PinnedByRuleId, score, overhead | Pass | PinnedByRuleId on pins, null on organic; moved organic pin keeps its score, non-match scores 0; p95 ≈ 8 ms |
| Checkpoint | Approved | Host "coffee": T-shirt Black #1, Origins of Arabica Bourbon #3 (once; Spanish document not shown), total 29; "tea" unchanged. Rule deleted, host stopped |

## Verification Results

- `dotnet build XperienceCommunity.Lucene.Suite.slnx` → 0 warnings, 0 errors.
- `dotnet test` with comm_projects_test → 394 passed, 2 skipped.
- Host log: no Suite errors; 2 `fail:` lines from a leftover XpSearch macro (`Contact.XpSearchSearchedFor`) in a contact group condition — pre-existing, covered by the pending XpSearch-objects decision.
- KNOWN-LIMITATIONS: 2 entries (pin scoring/counting/reporting incl. multilingual facets and pin-only zero-result semantics; pin lookup cost and FieldCache memory).

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Search/PinMerger.cs` (21) | Created | Pure global placement + page slice |
| `Search/SearchExecutor.cs` | Modified | Pinned match-set clause; fetch window + pins; pinned-document lookup (all languages), filter check, document choice, total formula, merge |
| `Search/SuiteSearchResponse.cs` | Modified | `SuiteSearchHit.PinnedByRuleId`; Score doc |
| `PinMergerTests.cs` (36), `RuleSearchTests.cs` | Created / extended | 9 merge cases; 9 pin integration tests; pageable invariant; 300-pin benchmark |

## Skill Audit

All required skills invoked ✓ — /dotnet:csharp-refactoring and /kentico-web-development:content-retrieval loaded this session; content-retrieval confirmed not applicable (pins read from the Lucene index, no Kentico content API). Admin design skills not applicable.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 2 | Performance target met; test expectation corrected |
| Clarifications | 1 | Sideways facet semantics |
| Deferred | 0 new | Audit deferrals stand |

1. **Overhead first measured 20.3 ms** (Task 2): ItemGuid read from stored documents for ~600 documents per search. Fixed by (a) identifying window documents of pinned items by doc-id membership in the pinned-document set and (b) reading ItemGuids through `FieldCache.DEFAULT.GetTerms` per segment, plus sizing the lookup to the pin count → 7.5–8.3 ms.
2. **Two PinMerger expectations wrong** (5 organic + 2 pins = 7); code correct. Equal-position comment corrected (later pin lands first; the engine never produces equal positions).
3. **Facet counts for filter-ignoring pins:** drilled-down counts exclude a failing pin as planned, but DrillSideways counts of the filtered dimension include it (they answer "if you selected this value"). Documented in KNOWN-LIMITATIONS.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Bash heredoc failed on the large edit | Edits run from script files |
| User's host (PID 29288) held port 27340 and bin | User stopped it; rebuilt and checked |
| Host index is multilingual (es documents share ItemGuids) | Confirmed the audit's one-hit-per-item fix on real data |

## Next Phase Readiness

**Ready:**
- 03-06 has everything the editor and query tester need: FiredRuleIds, UserData, RulesFailed, PinnedByRuleId; RuleSerializer for inline errors.

**Concerns:**
- Dropped pins are not reported; the 03-06 query tester can't explain a missing pin without a response field.
- Leftover XpSearch contact-group macro errors on the host (user decision pending).

**Blockers:** None.

---
*Phase: 03-rules-engine, Plan: 05 · Completed: 2026-10-11*
