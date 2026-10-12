# Roadmap: XperienceCommunity.Lucene.Suite

## Overview
Marketer-facing search tuning, measurement and testing for Kentico.Xperience.Lucene. Ships in three milestones: tune and start capturing data (v0.1), measure (v0.2), test (v0.3). Event capture starts in v0.1 because reports need accumulated history.

## Current Milestone
**v0.2 Measure** (v0.2.0)
Status: 🚧 In Progress
Phases: 0 of 4 complete
Focus: Marketers see how site search performs and act on it — reports on real (or seeded) history, suggestions that steer visitors, and search behaviour in Kentico contacts.

## Phases

| Phase | Name | Plans | Status | Completed |
|-------|------|-------|--------|-----------|
| 7 | Report Data | 0/2 | Planning | - |
| 8 | Reports Admin | TBD | Not started | - |
| 9 | Suggestions | TBD | Not started | - |
| 10 | Custom Activities | TBD | Not started | - |

## Phase Details

### Phase 7: Report Data
**Focus:** Report query service over `SearchDaily` (KPIs, top queries, no-results, CTR, slowest from the latency histogram; date range; `(other)` handling) and a dev-only seed generator that writes contract-faithful synthetic events across many days and runs the real rollup.
**Depends on:** v0.1 (capture, rollup, retention)
**Decisions (2026-10-12):** seed generator = opt-in test runner; vocabulary from the index; seed the host (~60 removable days); overview compares with the previous period

**Plans:**
- [x] 07-01: Report query service (overview + previous period, trend, surfaced query tables + (other), latency percentiles)
- [ ] 07-02: Seed generator (contract-faithful synthetic sessions → RollupEngine → SearchDaily before the job start, --clear) + host seeding

### Phase 8: Reports Admin
**Focus:** Report pages per index in the Site Search app, recomputed on condition change; links into rules and synonyms (e.g. create a rule for a no-results query).
**Admin UI:** design via /xbyk-admin-ui:design-screen (SPECIAL-FLOWS)
**Depends on:** Phase 7
**Plans:** TBD

### Phase 9: Suggestions
**Focus:** Query suggestions from surfaced queries; a rate-limited suggest endpoint in the Widgets package returning strings only (untracked); a progressive-enhancement search box script (accessible combobox); a per-index blocked-suggestions list with admin editor (blocks suggestions only, not reports).
**Depends on:** Phase 7 (surfaced queries); Widgets package
**Plans:** TBD

### Phase 10: Custom Activities
**Focus:** Kentico custom activity types — search performed (one per logical search, scrubbed text), result clicked, no results — logged only with a current contact and Kentico consent/cookie level allowing it.
**Depends on:** v0.1 capture pipeline
**Plans:** TBD

**Constraints (from discussion 2026-10-12):** Suite analytics stay identity-free; reports read rollups only and respect the surfacing threshold; the suggest endpoint serves no results and only threshold-surfaced text; the seed generator is dev-only; settings stay in appsettings (no admin settings page); checkpoints go to `.paul/APPROVAL-CHECKS.md`.

## Completed Milestones

<details>
<summary>v0.1 Tune & Capture - 2026-10-12 (6 phases, 21 plans)</summary>

| Phase | Name | Plans | Completed |
|-------|------|-------|-----------|
| 1 | Foundation | 3/3 | 2026-10-09 |
| 2 | Event Capture | 4/4 | 2026-10-09 |
| 3 | Rules Engine | 7/7 | 2026-10-11 |
| 4 | Synonyms | 2/2 | 2026-10-11 |
| 5 | Preview Sandbox | 1/1 | 2026-10-11 |
| 6 | Widgets Package | 4/4 | 2026-10-12 |

Full detail: `.paul/milestones/v0.1.0-ROADMAP.md` · Summary: `.paul/MILESTONES.md`

</details>

## Later Milestones

| Milestone | Contents |
|-----------|----------|
| v0.3 Test | A/B testing of rule sets on one index + comparison reports |

---
*Roadmap created: 2026-10-08 · Updated: 2026-10-12 (v0.1 complete; v0.2 Measure created)*
