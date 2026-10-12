---
phase: 11-search-insights
plan: 01
subsystem: analytics
tags: [insights, reports, sql-aggregation, options]

requires:
  - phase: 07-report-data
    provides: SearchReportService (Validate, previous period, row maths), Surfacing, LatencyPercentiles
  - phase: 03-rules-engine / 04-synonyms
    provides: pure RuleEngine / SynonymEngine and cached providers for annotations
provides:
  - SuiteInsightsOptions (LuceneSuite:Analytics:Insights, public, validated)
  - InsightEngine (pure) + SearchInsightService (internal) returning ranked, annotated findings
  - SearchReportService.PreviousPeriod (shared by reports and insights)
affects: [11-02 reports UI, 12-public-api-audit]

tech-stack:
  added: []
  patterns:
    - "Period-over-period per text in ONE SQL aggregation (conditional sums on a @parameter), never C# text joins"
    - "Annotate only what will be shown (after the cap)"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/Insights/InsightModels.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/Insights/InsightEngine.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/Insights/SearchInsightService.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/InsightEngineTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchInsightDatabaseTests.cs
    - docs/insights.md
  modified:
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptions.cs
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptionsValidator.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/SearchReportService.cs
    - src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/AnalyticsOptionsTests.cs

key-decisions:
  - "Range dates passed as an ObjectQuery parameter (@InsightFrom) in conditional-sum columns; only constant column names and validated ints are SQL text"
  - "Regression requires a strict worsening (> 0) in addition to RegressionMinPoints"
  - "Rule display names read with one WhereIn on RuleInfo (RuleDefinition has no name)"

patterns-established:
  - "Rate boundary comparisons in decimal (0.3 − 0.2 must equal 0.10)"

duration: ~25min (implementation delegated to an implementer agent; reviewed and re-verified)
completed: 2026-10-12T00:00:00Z
description: "Search insights core: five configurable finding kinds over one SQL aggregation of the range and previous period, noise-guarded by MinAffected, ranked by affected searches and annotated with the synonyms and rules that already match — 47 ms warm at volume"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 11 Plan 01: Search Insights Core Summary

**The Suite can now say what to fix first.** For any index and range, it returns content gaps, relevance misses, buried answers, slow queries and regressions. They are ranked by searches affected, and each shows the synonyms and rules that already cover it. 11-02 puts them on the reports page.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Configurable thresholds | Pass | 10 settings incl. audit-added MinAffected; defaults and the key named in each validation error; small/large site override tests |
| AC-2: One aggregation, SQL text identity | Pass | Conditional sums per QueryText on `@InsightFrom`; surfaced only; browse/(other)/other index excluded; MaxQueries + Truncated; parity with report Queries for 3 texts; collation-equal texts merged |
| AC-3: Rules | Pass | Boundary tests per rule (decimal comparisons); ContentGap/RelevanceMiss exclusive; several findings per query; ordering and cap with Omitted; MinAffected incl. 2-of-20 regressions |
| AC-4: Existing-fix annotations | Pass | Synonym ids + fired rules (id, name) only for capped findings; facet/context/disabled rules not listed; provider failure → flag + one log |
| AC-5: Range semantics and cost | Pass with note | Validate, PreviousPeriod and CompleteThrough are shared with reports; partial previous period → no regression; no writes (counts + checksum). Volume: **47 ms warm** (52 on an earlier run) at 400 days × 500 queries + 500 rules + 200 synonyms; **284 ms cold first call** (provider caches and SQL plan cold) |

## Verification Results

- `dotnet build`: 0 warnings. Full `dotnet test` on comm_projects_test: 795 passed, 6 skipped (opt-in volume tests), 0 failed. Re-run by me after the implementer's report.
- Report tests unchanged. The only extraction, `PreviousPeriod`, keeps the identical expression.
- Volume test: `LUCENESUITE_VOLUME_TEST=1`. It times the second call, following the 07-01 pattern.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Stricter than planned | 1 | Dates as SQL parameters instead of invariant literals |
| Rule refinement | 1 | Regression needs a strict worsening (else RegressionMinPoints = 0 flags unchanged queries) |
| Budget interpretation | 1 | ≤ 200 ms holds warm, not on the cold first call; for milestone review |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Loaded in this session; the implementer has no skill tool, so its extraction was reviewed by me (identical expression) |
| /kentico-web-development:content-retrieval | ✓ | Loaded in this session; ObjectQuery with AggregatedColumn + parameters |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Another session edits docs/ (GitHub Pages scaffold, update-docs flow, several uncommitted pages) | Only docs/insights.md is ours; the others are left untouched. `docs/configuration.md` (theirs) should get the Insights settings |

## Next Phase Readiness

**Ready:**
- 11-02 calls `SearchInsightService.Insights` from a page command and renders `InsightReport`. It maps each kind to the existing actions: synonym/rule prefill, query tester, Block suggestion.

**Concerns:**
- The cold-call latency.
- docs/insights.md says "the page says so" about annotation failure, which is only true once 11-02 shows the flag.

**Blockers:** None

---
*Phase: 11-search-insights, Plan: 01*
*Completed: 2026-10-12*
