---
phase: 08-reports-admin
plan: 01
subsystem: ui
tags: [kentico-admin, react, svg-chart, intl, date-handling]

requires:
  - phase: 07-report-data
    provides: SearchReportService (Overview, Trend, Latency), seeded host history
provides:
  - Per-index "Search reports" admin page (range bar, KPI tiles with deltas, trend chart, latency)
  - Reusable BarChart (token SVG + table view) and UTC-day helpers for 08-02
affects: [08-02 query tables, 09-suggestions admin]

tech-stack:
  added: []
  patterns:
    - "Admin pages resolve internal Suite services through IServiceProvider (public page ctors cannot take internal types)"
    - "Report days stay yyyy-MM-dd strings end to end; picker Dates converted via local calendar fields"
    - "Only CSS variables the 31.9.1 admin runtime defines; spacing from the npm Spacing enum"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchReportsPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/SearchReportsTemplate.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/TrendChart.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/report.ts
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/reports/report.test.ts
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchReportsPageDatabaseTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/entry.tsx
    - tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs
    - .paul/APPROVAL-CHECKS.md

key-decisions:
  - "URL range persistence dropped: history.replaceState raced Kentico's admin router (URL became /admin/?from=…)"
  - "Chart colours from runtime tokens (--color-icon-highlighted, --color-alert-icon, --color-background-selected, --color-border-disabled); kit-repo tokens like --color-background-primary are undefined in 31.9.1"
  - "Rate deltas in percentage points, counts relative, average position absolute"

patterns-established:
  - "Verify computed styles in the browser pane for every CSS variable used: the kit repo is ahead of the admin runtime"

duration: ~90min
completed: 2026-10-12T00:00:00Z
description: "Search reports admin page per index: preset/custom UTC day ranges, six KPI tiles with previous-period deltas, token-styled SVG trend and latency charts with table views; verified against SQL on the seeded host"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 8 Plan 01: Search Reports Page Summary

**Marketers with Site Search → View now get a "Search reports" page per index:**
- preset and custom UTC day ranges;
- six KPI tiles with the change against the previous period;
- a trend chart and latency histogram (token-styled SVG, each with a table view).

On the seeded host, every number checked equals SQL.

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~90 min |
| Tasks | 3 of 3 completed |
| Files | 6 created, 3 modified |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Page, permissions, command | Pass | Slug `reports`, order 50 (first in the section), Icons.Graph, VIEW on the page and the command; the index always comes from the page. Invalid ranges and bad formats return Error. Registration + DB tests. |
| AC-2: Range bar | Pass with deviation | Presets (7/30/90/365 days ending yesterday, capped) and a custom picker limited to the server's bounds. Latest request wins. UTC-day strings end to end; tested in 5 time zones and on a UTC−4 browser. **URL persistence dropped** (see Deviations). |
| AC-3: KPI tiles and notices | Pass | 6 InfoCards. Relative % for counts, percentage points for rates, absolute change for position. "No comparison" with a notice when the previous period is partial. A "not final" notice that disappeared once the watermark advanced. Empty state shown for a range with no searches. |
| AC-4: Trend chart + table | Pass | role="img" with a summary, pattern-plus-legend for days that aren't final, at most 8 axis labels, sized to its card (621 px / 1,221 px), no overflow; table view with a "Final" column. |
| AC-5: Latency | Pass | p50/p95/p99 shown as "≤ N ms" or "over 5 s"; 10-bucket histogram with a table view. |
| AC-6: Design validation | Pass | Browser pane at 1,100 and 1,700 px viewports. Headings h1 → h2 × 3, tab order presets → Custom → switches, runtime tokens verified with computed styles. APPROVAL-CHECKS #5 recorded as Pending. |

## Verification Results

- `dotnet build` → 0 warnings.
- `dotnet test` with the test DB → 625 passed, 5 skipped (opt-in volume/seed tests).
- Client:
  - `npm run typecheck` clean;
  - `npm test` 44/44;
  - `report.test.ts` passes under UTC, America/New_York, Asia/Tokyo, Pacific/Kiritimati (UTC+14) and Pacific/Pago_Pago (UTC−11);
  - `npm run build` compiles.
- Host, comparing the page with SQL:

| Range | Page | SQL |
|-------|------|-----|
| 30 days | 3,681 searches; 1,480 session-days; −5.6 % | Same, against a previous period of 3,900 |
| 90 days | 7,731 searches, partial comparison | 7,731 |
| Custom Aug 13–20 | 1,050 searches | 1,050 |

## Accomplishments

- The first marketer-facing measurement screen; Phase 7's authorization note is closed (the page gates View and the index).
- The UTC-day handling is the plan's must-have: proven in node across extreme time zones and in a real UTC−4 browser with the Kentico picker.
- The SVG `BarChart` needs no dependency and is reusable by 08-02 and later reports.

## Task Commits

| Task | Commit | Type | Description |
|------|--------|------|-------------|
| Tasks 1–3 | `0601a20` | feat | Page, client template, chart, helpers, tests, APPROVAL-CHECKS #5 |

Plan metadata: `63f3d67` (docs: plan + audit)

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 2 | Chart tokens; URL persistence removed per the audit's fallback |
| Scope changes | 1 | No reports.css (no CSS loader in the client) |
| Deferred | 0 new | Audit deferrals in KNOWN-LIMITATIONS |

### Details

**1. URL persistence removed (AC-2 fallback).**
- `history.replaceState` ran while Kentico's single-page app was still on `/admin/`, producing `/admin/?from=…`; a reload would land on the admin home.
- The audited plan said to drop it rather than work around the router, so I did. The helpers and their tests were deleted with it; `isAllowed` keeps the bounds tests.

**2. Chart tokens remapped.**
- The bars rendered black: `--color-background-primary`, `--color-background-primary-subtle` and `--color-border-low-emphasis`, as well as `--spacing-*` and `--font-size-*`, are undefined in the 31.9.1 admin runtime. They exist only in the newer kit repo.
- The chart now uses runtime tokens, verified with `getComputedStyle`: `--color-icon-highlighted` (#5d0088), `--color-alert-icon`, `--color-background-selected`, `--color-border-disabled`. Spacing comes from the npm `Spacing` enum; label size is the relative `smaller`.

**3. No `reports.css`.** The webpack setup has no CSS loader and the client has no CSS files; styles are inline with tokens, as in the existing templates.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Public `Page` constructor cannot take the internal `SearchReportService` | Resolve it through `IServiceProvider`; mapping lives in a testable `internal static Build` |
| CS1591 on the public DTOs | Short XML docs added |
| Kentico's `DateTimeRangeInput` has built-in shortcuts ("Last 48 hours"…) that can't be hidden | They work (any range is converted to days); recorded in APPROVAL-CHECKS #5 for review |
| Host rollup job concern from Phase 7 | Resolved: the job rolled 2026-10-11 during the check (watermark set; 6 real searches appeared) |

## Skill Audit

All required skills invoked ✓:
- /xbyk-admin-ui:design-screen in PLAN and again in APPLY before markup;
- admin UI design validation (npm typings, computed tokens, states, accessibility, two widths);
- /dotnet:csharp-refactoring;
- /kentico-web-development:content-retrieval.

## Next Phase Readiness

**Ready:**
- 08-02 adds the query tables and row actions to this page.
- `report.ts` formatting and the `BarChart` are reusable.
- The runtime-token rule is established.

**Concerns:**
- APPROVAL-CHECKS #4 (seeded data) and #5 (reports page) are pending the milestone review.
- The 08-02 row actions must prefill the query tester, rule and synonym pages without a URL query string (the same admin-router risk); plan for page parameters or command-based state.

**Blockers:** None

---
*Phase: 08-reports-admin, Plan: 01*
*Completed: 2026-10-12*
