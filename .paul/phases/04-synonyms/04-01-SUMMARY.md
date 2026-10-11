---
phase: 04-synonyms
plan: 01
subsystem: search
tags: [lucene, synonyms, query-expansion, kentico-module-class]

requires:
  - phase: 03-rules-engine (03-04)
    provides: SuiteSearchService.SearchWithRules fail-open, SearchExecutor composition, cached provider pattern
provides:
  - LuceneSuite.Synonym module class (installer v5) + SynonymInfo
  - SynonymSerializer (strict, errors by path), SynonymProvider (cached per index), SynonymEngine (MatchKey, single-substitution variants)
  - Query-time expansion in search (DisjunctionMaxQuery, weight 0.8), AppliedSynonyms + SynonymsFailed on the response
  - LuceneSuite:Synonyms options (Weight, MaxExpansions, CacheMinutes)
affects: [04-02 synonyms admin + query tester, 05 preview sandbox]

tech-stack:
  added: []
  patterns:
    - "Query variants built through ISearchQueryFactory and combined with DisjunctionMaxQuery(0); variant boost multiplied by weight"
    - "One MatchKey function for stored terms and query word runs"
    - "Fail-open rerun covers rules and synonyms together; flags set only for what was actually applied"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Synonyms/SynonymInfo.cs
    - src/XperienceCommunity.Lucene.Suite/Synonyms/SynonymDefinition.cs
    - src/XperienceCommunity.Lucene.Suite/Synonyms/SynonymSerializer.cs
    - src/XperienceCommunity.Lucene.Suite/Synonyms/SynonymProvider.cs
    - src/XperienceCommunity.Lucene.Suite/Synonyms/SynonymEngine.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite/Search/SearchExecutor.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchService.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/AnalyticsModuleInstaller.cs

key-decisions:
  - "RulesFailed set on the shared rerun only when rules actually fired (RuleEngine always returns an outcome)"
  - "Synonym weight stays 0.8 (user approved after seeing the host ranking example)"

duration: ~1 session
completed: 2026-10-11T00:00:00Z
description: "Per-index two-way and one-way synonyms stored in LuceneSuite.Synonym and applied at query time as weighted DisMax variants, with fail-open and AppliedSynonyms on the response"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 4 Plan 01: Synonyms engine Summary

**Searches now expand query text with the index's synonyms (two-way groups and one-way entries) as weighted query variants, with no index rebuild; failures fall back to the original text.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1 session (2026-10-11) |
| Tasks | 2 auto + 1 checkpoint, all complete |
| Files | 5 created (src) + 4 test files created, 9 modified |
| Tests | 438 → 474 passed (3 opt-in skipped) |
| Volume (opt-in) | Expand p95 0.07 ms over 2 000 synonyms; search p95 0.126 ms with vs 0.123 ms without |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Stored per index, validated strictly | Pass | All error paths tested; letter/digit rule; installer v4→v5 test (idempotent, v4 objects untouched); host at v5 |
| AC-2: Weighted variants | Pass | Two-way both ways, one-way not reversed, no cascading, contiguous multi-word, punctuation-trimmed, order/dedupe/cap tested |
| AC-3: Search applies synonyms, fail-open | Pass | Exact ranks above identical synonym-only doc (ratio ≈ 0.8); all-terms factory works; boost multiplied (2 → 1.6); provider failure and variant-only factory failure fall open; ArgumentException propagates; rules see original text; request unchanged |
| AC-4: Config, caching, overhead | Pass | Validator rules; DB test: cached per index, disabled/invalid skipped (logged), every change clears; volume numbers above |
| AC-5: Live check | Pass with notes | "mill" → 7 hits incl. grinders; "grinder" → exact grinders (1.93, 1.68) above Mini Mill Slim (0.93); "tee" → 3 T-shirts. AppliedSynonyms not visible live (query tester result type lacks it until 04-02; unit-tested). One-way reverse not observable on host data (unit-tested) |

## Deviations

| Type | Description | Impact |
|------|-------------|--------|
| Behaviour refinement | Shared fail-open rerun sets RulesFailed only when rules fired (`outcome.FiredRuleIds.Count > 0`); before, any non-null outcome set it. Matches the plan's AC-3 wording | More accurate flag; 03-04 fail-open tests still pass |
| Verification method | Checkpoint synonyms inserted/deleted with SQL (no admin until 04-02), inserted before the first search so the cache was empty; host restarted after deletion to drop the cached set | No Kentico event-log entries for those rows |
| Finding | Query "mill" ranked grinders (1.28, 1.12) above the exact "Hario Mini Mill Slim" (0.97, identical to its no-synonym score) — the audit's "weight is a preference" case | Recorded in KNOWN-LIMITATIONS with these numbers; user approved keeping 0.8 |

## Findings recorded (KNOWN-LIMITATIONS)

- Weight is a preference, not a ranking guarantee (host example).
- Literal matching: no plurals/stems, no accent folding, 1–5 contiguous words, one substitution per variant.
- Storage: display column empty for two-way groups; orphan rows after index rename/deletion.

## Skill audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Loaded this session |
| /kentico-web-development:content-retrieval | ✓ | Loaded this session; SynonymInfo via IInfoProvider + IProgressiveCache with Columns() |

Skill audit: all required skills invoked ✓

## Next Phase Readiness

**Ready:** 04-02 can build the synonyms listing/editor on SynonymInfo + SynonymSerializer (errors by path) behind Manage synonyms, and add AppliedSynonyms/SynonymsFailed to the query tester result.

**Concerns:**
- comm_projects_test is still at v4 in STATE's environment note but the DB tests upgrade it to v5 on run (now v5).
- Leftover XpSearch scheduled tasks (QueryLogRetention, PopularitySignal) fail on every host start — part of the pending XpSearch cleanup decision.

**Blockers:** None

---
*Phase: 04-synonyms, Plan: 01*
*Completed: 2026-10-11*
