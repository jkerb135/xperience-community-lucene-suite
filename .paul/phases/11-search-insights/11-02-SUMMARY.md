---
phase: 11-search-insights
plan: 02
subsystem: ui
tags: [kentico-admin, dashboard, insights, side-panel, accessibility]

requires:
  - phase: 11-search-insights
    provides: SearchInsightService / InsightReport (11-01)
  - phase: 08-reports-admin / 08.1 / 09-03
    provides: reports dashboard, RowActions + handoffs, returnFocus, BlockSuggestion command
provides:
  - SearchReportsPage.Insights command + client DTOs (SearchInsightsResult, SearchInsightRow, linked rule/synonym)
  - insights.ts (kind labels, evidence, grouping) + Fix these first card + All insights side panel
  - RowActions action subset; QueryTesterPage.RuleEditUrl / SynonymEditUrl shared helpers
  - 11-01 engine fix: affected = searches actually lost (Regression), whole-bucket p95 + exact slow count (SlowQuery)
affects: [12-public-api-audit]

tech-stack:
  added: []
  patterns:
    - "Findings state lifted to the template, fetched once by the card, reused by the panel"
    - "Latency buckets: a threshold holds only when the whole bucket is at or above it (lower edge)"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/insights.ts
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/insights.test.ts
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchReportsPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/NeedsAttention.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/QueryTable.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/SearchReportsTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/Insights/InsightEngine.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/Insights/InsightModels.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/Reports/Insights/SearchInsightService.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/InsightEngineTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchInsightDatabaseTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchReportsPageDatabaseTests.cs
    - docs/insights.md

key-decisions:
  - "Ranking defect found on the host and fixed in 11-01 Core (plan boundary allowed defect fixes): Regression affected = round(searches × rate change); SlowQuery only when the p95 bucket's lower edge ≥ cutoff, affected = searches in buckets ≥ cutoff"
  - "Audit must-have: no-findings fallback to the pre-11-02 queries-without-results list"
  - "RowActions subset keeps the table's fixed button order across findings"

patterns-established:
  - "Verify rankings on real data before closing: unit-correct thresholds can still rank the wrong problems first"

duration: ~80min (Tasks 1–2 delegated to an implementer agent; host check, engine fix and docs by me)
completed: 2026-10-12T00:00:00Z
description: "Search reports' Fix these first card ranks findings by searches actually lost, with pre-filled fixes and existing-fix links, plus an All insights panel and a fallback that keeps the old no-result list — verified on the host after fixing an 11-01 ranking defect"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 11 Plan 02: Search Insights UI Summary

**Search reports now opens with a to-do list.** The Fix these first card ranks the problems that cost the most searches and offers the fix for each, pre-filled. It also shows when a fix is already in place.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Insights command | Pass | View-gated; index from the page; range errors → `error`; edit URLs via the query tester's helpers (now shared); no writes (DB test + host counts) |
| AC-2: Needs attention card | Pass | Top 5 with kind text, query, evidence, existing-fix links and the kind's actions. Host: "Add synonym" prefilled, existing-fix link shown, range reload, no-findings fallback, li labelled by its query |
| AC-3: All-insights panel | Pass with gap | Grouped by kind (h3 + count + explanation), same items and actions. Focus return on close **not observable** in the hidden pane (no animation frames; the existing panel behaves the same there) |
| AC-4: Native look, a11y, no regressions | Pass | 1100/1700 px: no overflow, one-line actions, heading outline h1/h2/h3. Query table unchanged (defaults). Build 0 warnings; .NET 798 passed / 6 skipped; client 77; tsc clean; host counts unchanged |

## Verification Results

- Re-run by me after the engine fix: build 0 warnings; `dotnet test` 798 passed, 6 skipped; `npm test` 77/77; `tsc --noEmit` 0.
- Host (DancingGoatSample, seeded 60 days, 30-day range): 7 findings / 36 queries checked; the top 5 are the 100% no-result queries.
- A temporary synonym (#5) produced "Already applies: Synonym" with a link to its edit page, and was then deleted. Counts are identical before and after.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Defect fix in 11-01 Core | 1 | Regression and Slow "affected" now count only searches actually lost or slow; Slow uses the bucket's lower edge. Two engine theories and two DB ranking expectations updated; docs/insights.md updated |
| Implementer choices kept | 4 | Two URL delegates (reusing the query tester's helpers, extracted); fixed button order; "Synonym N" link text without ↗; CompleteThrough not in the DTO |
| Verification gaps | 2 | Focus return (hidden pane); rule/tester/block actions from a finding not clicked (they share the table's tested paths; no relevance-miss finding existed for Block) |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN and APPLY (before markup) |
| Admin UI design validation | ✓ | Two widths, overflow, headings, accessible names, states, counts; focus return not observable |
| /dotnet:csharp-refactoring | ✓ | Loaded in this session; the extraction of the URL helpers was reviewed |
| /kentico-web-development:content-retrieval | ✓ | Loaded in this session; no new queries |
| update-docs | run at UNIFY | See the phase transition |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| The first live ranking put noisy regressions and a 500 ms–1 s "slow" query above 100% no-result queries | Engine fixed (above) |
| An inline shell heredoc failed (Git Bash parse), then the script got a /f/ path that Windows Python can't open | Script file in project-sidecar/tmp run with a Windows path; nothing was half-applied |
| A concurrent session committed docs (incl. insights.md, configuration Insights rows) | Only insights.md edited; configuration.md's SlowMinP95Ms wording ("at least this") left to the docs owner |

## Next Phase Readiness

**Ready:** Phase 12 Public API Audit. New public surface to review: SuiteInsightsOptions and the reports page's client DTOs (`SearchInsights*`, `BlockSuggestion*`).

**Concerns:** APPROVAL-CHECKS #4–#14 pending, including the ranking definition (#14) and the cold-call latency (11-01).

**Blockers:** None

---
*Phase: 11-search-insights, Plan: 02*
*Completed: 2026-10-12*
