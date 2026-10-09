# Roadmap: XperienceCommunity.Lucene.Suite

## Overview
Marketer-facing search tuning, measurement and testing for Kentico.Xperience.Lucene. Ships in three milestones: tune and start capturing data (v0.1), measure (v0.2), test (v0.3). Event capture starts in v0.1 because reports need accumulated history.

## Current Milestone
**v0.1 Tune & Capture** (v0.1.0)
Status: In progress
Phases: 2 of 6 complete (33%)

## Phases

| Phase | Name | Plans | Status | Completed |
|-------|------|-------|--------|-----------|
| 1 | Foundation | 3/3 | ✅ Complete | 2026-10-09 |
| 2 | Event Capture | 4/4 | ✅ Complete | 2026-10-09 |
| 3 | Rules Engine | 1/5 | In progress | - |
| 4 | Synonyms | TBD | Not started | - |
| 5 | Preview Sandbox | TBD | Not started | - |
| 6 | Widgets Package | TBD | Not started | - |

## Phase Details

### Phase 1: Foundation ✅ (complete 2026-10-09)
**Goal:** Buildable package, suite-owned search API over `ILuceneSearchService`, and one working admin page (basic query tester) proving the client pipeline end to end.
**Depends on:** Nothing
**Research:** Done (kentico-lucene-extension-points.md, local-conventions-and-design-kit.md)
**Scope:**
- Isolated build (own global.json/props/CPM), net10.0 only, Kentico Lucene ≥ 15.0.5 (coexists with XpSearch's exact pin), LUCENE_48 in xbk
- Search API that sees query text, facets, hit count and timing (rules hook added in Phase 3 with the first rule)
- Admin application (own prefix, marketer category) on the design library + basic query tester
- Database installer moved to Phase 2 (first tables)

**Plans:**
- [x] 01-01: Skeleton + xbk wiring (complete 2026-10-08)
- [x] 01-02: Search API + test project (complete 2026-10-08)
- [x] 01-03: Admin application + client pipeline + query tester (complete 2026-10-09)

### Phase 2: Event Capture ✅ (complete 2026-10-09)
**Goal:** Identity-free, web-farm-safe search/click event capture.
**Depends on:** Phase 1 (search API)
**Research:** Done (query-analytics-dedup.md)
**Scope:** identity-free events (page-memory searchId; Suite session cookie only at Visitor/All; no contact/IP stored), forwarded-headers-aware in-memory bot/rate filter, PII scrub, Kentico bulk writes to module classes, hosted single-runner rollup job, 48 h raw retention → daily KPI rollups (25 months), surfacing threshold (≥ 5 searches on ≥ 2 days) + 30-day prune of sub-threshold rows. Merge spec: project-sidecar/docs/research/as-you-type-merge-analysis.md

**Plans:**
- [x] 02-01: Capture — module class + installer, request/response searchId contract, scrubber, session cookie, decorator + bulk writer (complete 2026-10-09)
- [x] 02-02: Interactions — ISearchInteractionLogger (LogClick/LogConversion) through the capture pipeline, installer v2, client contract doc + test vectors (complete 2026-10-09)
- [x] 02-03: Merge engine — configurable options + validation, SearchDaily class (installer v3), pure rollup engine, parameterized vectors (complete 2026-10-09)
- [x] 02-04: Job & retention — hosted single-runner job, watermark, idempotent persistence, raw/daily purge, sub-threshold fold into (other), E2E + volume on a disposable DB (complete 2026-10-09)

### Phase 3: Rules Engine (in progress, 1/5)
**Goal:** Marketers pin/boost/bury/hide/attach data by query, facets and context.
**Depends on:** Phase 1
**Research:** Done (search-merchandising-standards.md)
**Permissions resolved (2026-10-09):** Suite-owned "Site Search" app (Digital Marketing) with custom permissions (option A)

**Plans:**
- [x] 03-01: Placement & permissions — Site Search app, custom permissions, move section/query tester, role checkpoint (complete 2026-10-09)
- [ ] 03-02: Query tester design & state fixes — stuck facets, request handling, kit layout/states/side panel, a11y, C# polish; design validation made required (planned, inserted 2026-10-09 after design review)
- [ ] 03-03: Rule model + pure engine — module classes, conditions, actions, priority/conflicts, validity
- [ ] 03-04: Search integration — rules hook, boost/bury rewrite, pin/hide post-processing, contexts, cache invalidation, overhead
- [ ] 03-05: Rules admin UI — listing, ConditionBuilder editor, content picker pins, query tester shows fired rules

### Phase 4: Synonyms
**Goal:** Query-time synonym rewrite managed in admin.
**Depends on:** Phase 1

### Phase 5: Preview Sandbox
**Goal:** Side-by-side results with and without rules under a simulated contact-group/visitor context.
**Depends on:** Phases 3, 4

### Phase 6: Widgets Package
**Goal:** Optional `XperienceCommunity.Lucene.Suite.Widgets` package: HTTP events endpoint (MapLuceneSuiteEvents, rate-limited) over ISearchInteractionLogger, JS client implementing the client contract (tested against the shared vectors), tag helpers and Page Builder search widgets — gives xbk real Suite traffic.
**Depends on:** Phases 2–5

## Later Milestones

| Milestone | Contents |
|-----------|----------|
| v0.2 Measure | Reports (CTR, no-results, top, slowest; recompute on condition change), query suggestions, blocked suggestions, custom activities |
| v0.3 Test | A/B testing of rule sets on one index + comparison reports |

---
*Roadmap created: 2026-10-08 · Updated: 2026-10-09 after 03-01*
