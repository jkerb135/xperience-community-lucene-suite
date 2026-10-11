# Roadmap: XperienceCommunity.Lucene.Suite

## Overview
Marketer-facing search tuning, measurement and testing for Kentico.Xperience.Lucene. Ships in three milestones: tune and start capturing data (v0.1), measure (v0.2), test (v0.3). Event capture starts in v0.1 because reports need accumulated history.

## Current Milestone
**v0.1 Tune & Capture** (v0.1.0)
Status: In progress
Phases: 4 of 6 complete (67%)

## Phases

| Phase | Name | Plans | Status | Completed |
|-------|------|-------|--------|-----------|
| 1 | Foundation | 3/3 | ✅ Complete | 2026-10-09 |
| 2 | Event Capture | 4/4 | ✅ Complete | 2026-10-09 |
| 3 | Rules Engine | 7/7 | ✅ Complete | 2026-10-11 |
| 4 | Synonyms | 2/2 | ✅ Complete | 2026-10-11 |
| 5 | Preview Sandbox | 0/1 | Planning | - |
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

### Phase 3: Rules Engine ✅ (complete 2026-10-11)
**Goal:** Marketers pin/boost/bury/hide/attach data by query, facets and context.
**Depends on:** Phase 1
**Research:** Done (search-merchandising-standards.md)
**Permissions resolved (2026-10-09):** Suite-owned "Site Search" app (Digital Marketing) with custom permissions (option A)

**Plans:**
- [x] 03-01: Placement & permissions — Site Search app, custom permissions, move section/query tester, role checkpoint (complete 2026-10-09)
- [x] 03-02: Query tester design & state fixes — stuck facets, request handling, kit layout/states/side panel, content-hub Filters panel, a11y, C# polish; design validation made required (complete 2026-10-10)
- [x] 03-03: Rule model + pure engine — JSON model + strict validator, pure engine, LuceneSuite.Rule (installer v4) (complete 2026-10-10)
- [x] 03-04: Search integration (complete 2026-10-10) — request contexts + ApplyRules, cached RuleProvider (web-farm invalidation, invalid rules logged), hide via query exclusion, multiplicative boost/bury, fired rules + userData in response, overhead
- [x] 03-05: Pins (complete 2026-10-11) — placement across pages, respect-filters check, totals, pinned flag on hits (split from 03-04, 2026-10-10)
- [x] 03-06: Rules admin UI (complete 2026-10-11) — native listing + edit pages, conditions/boosts custom form components (server-validated), custom data; Manage rules permission (design via /xbyk-admin-ui:design-screen)
- [x] 03-07 (complete 2026-10-11): Pins/hides editor (index-search item picker, respect-filters toggle) + query tester shows fired rules, pinned marker, rules-not-applied warning (split from 03-06, 2026-10-11)

### Phase 4: Synonyms ✅ (complete 2026-10-11)
**Goal:** Query-time synonym rewrite managed in admin.
**Admin UI:** design via /xbyk-admin-ui:design-screen (SPECIAL-FLOWS)
**Depends on:** Phase 1
**Decisions (2026-10-11):** two-way + one-way; synonym matches weighted below exact (0.8); per index; two plans

**Plans:**
- [x] 04-01 (complete 2026-10-11): Synonym model (LuceneSuite.Synonym, installer v5), strict serializer, cached provider, pure expansion engine, search integration (weighted DisMax variants, fail-open, AppliedSynonyms)
- [x] 04-02 (complete 2026-10-11): Synonyms admin (listing + editor, Manage synonyms) + query tester shows applied synonyms

### Phase 5: Preview Sandbox (planning)
**Goal:** Side-by-side results with and without rules under a simulated contact-group/visitor context.
**Admin UI:** design via /xbyk-admin-ui:design-screen (SPECIAL-FLOWS)
**Depends on:** Phases 3, 4
**Decisions (2026-10-11):** compare mode inside the query tester; contexts = contact groups + free text; each column configurable; rank-change markers

**Plans:**
- [ ] 05-01: Query tester compare mode — contexts + page size on Search, ContactGroups command, two configurable columns, context picker, rank-change markers

### Phase 6: Widgets Package
**Goal:** Optional `XperienceCommunity.Lucene.Suite.Widgets` package: HTTP events endpoint (MapLuceneSuiteEvents, rate-limited) over ISearchInteractionLogger, JS client implementing the client contract (tested against the shared vectors), tag helpers and Page Builder search widgets — gives xbk real Suite traffic.
**Depends on:** Phases 2–5

## Later Milestones

| Milestone | Contents |
|-----------|----------|
| v0.2 Measure | Reports (CTR, no-results, top, slowest; recompute on condition change), query suggestions, blocked suggestions, custom activities |
| v0.3 Test | A/B testing of rule sets on one index + comparison reports |

---
*Roadmap created: 2026-10-08 · Updated: 2026-10-11 (Phase 4 complete)*
