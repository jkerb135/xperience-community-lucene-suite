# Roadmap: XperienceCommunity.Lucene.Suite

## Overview
Marketer-facing search tuning, measurement and testing for Kentico.Xperience.Lucene. Ships in three milestones: tune and start capturing data (v0.1), measure (v0.2), test (v0.3). Event capture starts in v0.1 because reports need accumulated history.

## Current Milestone
**v0.1 Tune & Capture** (v0.1.0)
Status: In progress
Phases: 0 of 5 complete

## Phases

| Phase | Name | Plans | Status | Completed |
|-------|------|-------|--------|-----------|
| 1 | Foundation | 2/3 | In progress | - |
| 2 | Event Capture | TBD | Not started | - |
| 3 | Rules Engine | TBD | Not started | - |
| 4 | Synonyms | TBD | Not started | - |
| 5 | Preview Sandbox | TBD | Not started | - |

## Phase Details

### Phase 1: Foundation
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
- [ ] 01-03: Admin application + client pipeline + query tester

### Phase 2: Event Capture
**Goal:** Identity-free, web-farm-safe search/click event capture.
**Depends on:** Phase 1 (search API)
**Research:** Done (query-analytics-dedup.md)
**Scope:** identity-free events (page-memory searchId, no contact/IP stored), forwarded-headers-aware in-memory bot/rate filter, PII scrub, buffered bulk writes, 48 h raw retention → daily KPI rollups (25 months), surfacing threshold (≥ 5 searches on ≥ 2 days) + 30-day prune of sub-threshold rows

### Phase 3: Rules Engine
**Goal:** Marketers pin/boost/bury/hide/attach data by query, facets and context.
**Depends on:** Phase 1
**Research:** Done (search-merchandising-standards.md)

### Phase 4: Synonyms
**Goal:** Query-time synonym rewrite managed in admin.
**Depends on:** Phase 1

### Phase 5: Preview Sandbox
**Goal:** Side-by-side results with and without rules under a simulated contact-group/visitor context.
**Depends on:** Phases 3, 4

## Later Milestones

| Milestone | Contents |
|-----------|----------|
| v0.2 Measure | Reports (CTR, no-results, top, slowest; recompute on condition change), query suggestions, blocked suggestions, custom activities |
| v0.3 Test | A/B testing of rule sets on one index + comparison reports |

---
*Roadmap created: 2026-10-08*
