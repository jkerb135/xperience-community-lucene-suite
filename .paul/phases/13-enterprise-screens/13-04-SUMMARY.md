---
phase: 13-enterprise-screens
plan: 04
subsystem: ui
tags: [kentico-admin, query-tester, compare, data-viz, accessibility]

requires:
  - phase: 13-enterprise-screens (13-03)
    provides: patterns.tsx (DataBar, RangeFooter, Stale)
  - phase: 05-preview-sandbox
    provides: Compare mode with per-column switches and simulated contexts (kept)
provides:
  - Query tester summary strip with fired-rule chips (icon + name + action summary) and synonym tags
  - Results table with position, Pinned tag, score DataBar, monospace GUID, RangeFooter 10/25/50
  - Compare columns as tables with a Moved column (arrow + n, —, New)
  - Try again on a failed search with faded previous results
  - QueryTesterFiredRule.Actions / PrimaryAction; RangeFooter pageLimit + resetKey
affects: [v0.3 Relevance query tester extensions (stopwords removed, typo corrections, field-weight compare)]

tech-stack:
  added: []
  patterns:
    - "Paging state (page + size) is committed only together with a successful result"
    - "Kentico Select keeps an unconfirmed pick: remount it (key) after a failed request"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/ResultParts.tsx
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/CompareView.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/state.ts
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/compare.ts
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/patterns.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleSummaries.cs

key-decisions:
  - "User: keep configurable Compare columns (Phase 5) and restyle as tables, not the handoff's fixed tuned/untuned pair"
  - "User: rows per page 10/25/50 client-side (Search already accepted PageSize)"
  - "Audit: page + size committed with the result; Try again only for failed requests"

patterns-established:
  - "Shared result parts (chips, tags, title cell) live in ResultParts.tsx so the template and CompareView don't import each other"

duration: ~2h (implementation delegated; host verification and two fixes by me)
completed: 2026-10-13T00:00:00Z
description: "Query tester matches the handoff: summary strip with fired-rule chips, score bars, positions and rows per page, Compare tables with rank movement, Try again on failure"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 13 Plan 04: Query Tester per Handoff Summary

**Marketers now see which rules fired and what they did, how strongly each document scored, and how far tuning moved each document.** The configurable Compare columns are kept.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Summary strip | Pass | Chip "Boost items for test · 1 pin" → /rules/4 with product-hover text, 8 px radius, 4/10 px padding and an aria-hidden icon; synonym Tag opens the synonym; headings h1/h2 |
| AC-2: Results table + footer | Pass | # / Title (Pinned tag) / Score bar (info blue, relative to the top) / GUID 12 px monospace; 50 px rows; 11–20 on page 2; 10/25/50; page and size committed with the result (checked while blocked) |
| AC-3: Compare rank movement | Pass | Tables; "New" for the pinned item; red arrow + n with hidden "1 place down"; explainer; focus returns to the table (rAF shim) |
| AC-4: States | Pass | Try again disabled in flight, aria-busy, recovers; bars 0.6 and text 1; "No documents match"; empty state never shown while failed (fixed during the check) |
| AC-5: No regressions | Pass | 669 / 1269 px both modes without overflow; DB counts unchanged; Debug + Release 0 warnings; .NET 857 passed / 6 skipped (flake passed alone); client 103/103 |

## Verification Results

- Builds: Debug and Release, 0 warnings.
- Full .NET suite: 857 passed, 6 skipped. RulesOverhead was the known flake and passed when rerun alone.
- Client tests: 103/103; tsc clean.
- Host evidence: APPROVAL-CHECKS #20. The temporary synonym #8 was deleted, and the counts are back at baseline (35 / 3389 / 1 / 0 / 0 / 8).

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Tasks 1–2 (+ host fixes) | `7077a3a` | feat | Query tester per handoff |
| Task 3 | (UNIFY commit) | docs | APPROVAL-CHECKS #20 |

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| New file | 1 | `ResultParts.tsx` holds the shared chips, tags and title cell. Exporting them from the template would have been a circular import |
| Spec reading | 1 | Try again re-runs the last *sent* request (query, page, size, filters), per AC-4; Task 2's text named the committed page, which would retry the wrong page |
| Host fixes | 2 | The page-size Select kept a failed pick (RangeFooter `resetKey`); the empty Callout could show next to "Search failed" (`&& !failed`) |
| Pre-existing bug fixed | 1 | `RuleSummaries` threw on duplicate-key JSON, which surfaced as "Search rejected"; it now degrades to "Invalid" |

## Deferred

- Score bars are relative to the current page (handoff design).
- Rows per page isn't persisted.
- Applied-filter chips update before a search succeeds (pre-existing, outside boundaries).

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | Re-invoked before markup; npm props and runtime tokens confirmed |
| Admin UI design validation | ✓ | Both widths, tokens, accessible names, focus return |
| /dotnet:csharp-refactoring | ✓ | Session |
| /kentico-web-development:content-retrieval | ✓ | Session (RuleNames ObjectQuery + Actions column) |
| /update-docs | ✓ | Phase 13 docs updated in 996f29b: rules-and-synonyms (rules list, form, pickers, Reload, validation, query tester), search-reports (report page section), insights (card), getting-started (Compare contexts) |

---
*Phase: 13-enterprise-screens, Plan: 04*
*Completed: 2026-10-13*
