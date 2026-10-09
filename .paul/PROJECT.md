# XperienceCommunity.Lucene.Suite

## What This Is

An add-on suite for Xperience by Kentico sites that use the `Kentico.Xperience.Lucene` integration. It gives marketers admin tooling to tune search results (rules, synonyms, suggestions), measure search (analytics reports), compare rule sets with A/B tests, and preview queries side by side under a simulated contact-group or visitor context. Ground-up rewrite that replaces the unreleased XpSearch project.

## Core Value

Marketers tune, test and measure site search without a developer.

## Current State

| Attribute | Value |
|-----------|-------|
| Type | Application (XbyK add-on library: admin + live site) |
| Version | 0.0.0 |
| Status | v0.1 in progress — Phase 1 complete, Phase 2 ready to plan |
| Last Updated | 2026-10-09 |

## Requirements

### Core Features

- Rules engine: pin, boost, bury, hide, attach custom data; conditions on query text, applied facets, and context (contact groups / visitor); priority + validity window
- Synonyms (applied at query time) and blocked suggestions
- Query suggestions built from search analytics
- Search analytics: CTR, no-results queries, top queries, slowest queries; reports recompute on every condition change
- A/B testing of rule sets against one index, with comparison reports
- Side-by-side query preview with simulated contact-group / visitor context
- Custom activity tracking (consent-gated)

### Validated (Shipped)
- ✓ Isolated build (net10.0, own props/CPM), Kentico Lucene ≥ 15.0.5 coexisting with XpSearch in xbk — Phase 1
- ✓ Suite search API over ILuceneSearchService (query text, field boosts, paging limits, facets with sideways counts, hit count, timing) — Phase 1
- ✓ Per-index "Search tuning" admin section with query tester; client Proxy in dev, embedded on non-Debug builds — Phase 1

### Active (In Progress)
- [ ] Phase 2 Event Capture: identity-free, web-farm-safe search/click events (not yet planned)

### Planned (Next)
- v0.1 Tune & capture: foundation, event capture, rules engine, synonyms, preview
- v0.2 Measure: reports, query suggestions, blocked suggestions, custom activities
- v0.3 Test: A/B testing of rule sets + comparison reports

### Out of Scope
- PDF export of reports — parked; cheapest route if revived is browser print stylesheet
- Live push (SignalR) report updates — "live" means recompute when filters change
- Unique-user metrics and per-user outlier exclusion — require identity, which analytics never stores
- A/B testing two separate indexes — rule-set A/B on one index covers the goal
- Migration from XpSearch — never released
- Profanity dictionary — industry standard is a user-managed blocklist

## Constraints

### Technical Constraints
- Must work on self-hosted web farms and Kentico SaaS
- File access only through CMS.IO (external storage may be mapped); marketer data lives in the database, not files
- `Kentico.Xperience.Lucene` exposes no query hook (`ILuceneSearchService` only lends a searcher) — the suite owns its search API on top
- Analyzers are created by reflection (no DI) and default to LUCENE_30 (upstream bug #204) — set LUCENE_48 explicitly
- Lucene.NET 4.8 has no `SynonymGraphFilter` — synonyms are a query-time rewrite
- Any added Lucene.Net.* package must match the host's exact beta version (4.8.0-beta00017)
- Index rebuilds return empty results (upstream #173) — exclude from analytics/A-B metrics
- No-op Lucene lock factory across servers when indexes share external storage
- Analytics dedup must be web-farm safe: no in-memory cross-request state; browser-issued searchId, merged at rollup
- Never bucket A/B variants or identify users by server IP; honor forwarded headers
- Use the `@kentico/xperience-admin-components` design library; distinct object-type prefix from XpSearch so both can run in xbk during switchover
- Admin UI extends Kentico Lucene's admin: a per-index "Search tuning" section beside IndexEditPage under IndexListingPage (/lucene/indexes/search-tuning/{indexId}/…), reached by a table link per index; Kentico forbids section children of Edit-template pages. Couples Suite.Admin to Kentico.Xperience.Lucene.Admin public page types; access follows the Lucene Search application (listing = VIEW)
- Admin client: Proxy mode (webpack dev server, port 3011) in development; Release build runs npm and embeds the bundle

### Business Constraints
- Public community package (MIT, matching upstream)
- XpSearch (Personal/xperience-community-lucene-instant-search) is loose reference only — never a basis for decisions

### Compliance Constraints
- Analytics/reporting store KPI/metric data only — never contact IDs, IPs or hashes. Identity-free is bounded by Kentico cookie level/consent: below Visitor, searches correlate only by a random per-search ID held in page memory; at Visitor/All, a Suite session cookie (random, registered with Kentico at Visitor level, 30-min sliding, no contact link) additionally groups searches across page loads
- IP used in memory for bot rate limiting only, never persisted
- Custom activities go through Kentico activity tracking and its consent/cookie level — the suite adds no gate of its own
- A/B participation uses a suite variant cookie (test + variant only, no identifier) registered with Kentico at a configurable cookie level; it is set only when the current Kentico cookie level allows it. Visitors without it are served control and excluded from experiment metrics. Events carry the variant only
- Surfacing threshold: a query appears in reports, seeds suggestions or prompts rule creation only after ≥ 5 searches on ≥ 2 distinct days (both configurable); counted from the first search; covers both business noise and k-anonymity
- Rollup rows still below threshold 30 days after last seen are pruned
- Scrub PII from query text at ingestion; raw events 48 h (until attribution closes), daily KPI rollups 25 months
- Market-neutral (primary use case USA): the suite never stores identity in analytics and never sets a cookie or tracks above the current Kentico cookie level; legal compliance (GDPR, CCPA, GPC handling) is the site's consent/cookie-level configuration, as with Kentico itself
- Rollup retention 25 months is a configurable default for year-over-year comparison, not a legal ceiling

## Key Decisions

| Decision | Rationale | Date | Status |
|----------|-----------|------|--------|
| Rewrite replaces XpSearch | Built before current tooling/design library | 2026-10-08 | Active |
| Own git repo per package | Sibling packages are separate repos | 2026-10-08 | Active |
| Marketer data in DB module classes; CMS.IO for real files only | Web farm + SaaS consistency | 2026-10-08 | Active |
| A/B tests compare rule sets on one index | Avoids double-indexing across servers | 2026-10-08 | Active |
| Query-time synonyms | No SynonymGraphFilter in Lucene.NET 4.8; no reindex per edit | 2026-10-08 | Active |
| Rule conflicts: explicit priority + ID tie-break; hide beats pin; pins respect filters | Industry common denominator; no content leaks | 2026-10-08 | Active |
| Contact groups passed as rule contexts | Industry standard (only Coveo resolves in-engine) | 2026-10-08 | Active |
| Algolia metric definitions (30 s as-you-type, 1 h attribution, 1-based positions) | Reference standard | 2026-10-08 | Active |
| Reports: recompute on condition change (no polling, no push) | "Live" = filter-driven; no push infra | 2026-10-08 | Active |
| Search API returns generic hits (ItemGuid, score, stored fields); default query from per-index field boosts via QueryBuilder, replaceable ISearchQueryFactory | No QueryParser dependency; admin + rules need a generic shape | 2026-10-08 | Active |
| Admin UI is a per-index section inside the Lucene app, sibling of IndexEditPage (option A) | Per-index tuning (user); Edit-template pages only allow side-panel/dialog children | 2026-10-09 | Active |
| Admin client Proxy in dev, embedded on Release via MSBuild npm step | User decision; matches Kentico's documented dev/deploy split | 2026-10-08 | Active |
| Buffered bulk writes for raw events, drop on overflow | Never slow search | 2026-10-08 | Active |
| Consented visitors get a Suite session cookie (option a), not the contact GUID | Cross-page session merging without making analytics personal data; no collector/eraser | 2026-10-09 | Active |
| Rollups/retention run as a hosted background job on every node, single-runner via sp_getapplock, idempotent per-day recompute | User decision; no admin setup, web-farm safe | 2026-10-09 | Active |
| Analytics tables are Kentico module classes written via InfoProviderBulkExtensions (BulkInsert/BulkUpdate/BulkDelete), CI/CD explicitly disabled; no plain SQL tables | User decision; Kentico 31.9.1 bulk APIs verified | 2026-10-09 | Active |
| Consent delegated entirely to Kentico cookie level / activity tracking | Platform-consistent; works for US and EU without suite-specific logic | 2026-10-08 | Active |
| Analytics are identity-free KPI data; no suite collector/eraser needed | No personal data stored → nothing to access/erase; counts all visitors | 2026-10-08 | Active |
| Surfacing threshold = count + distinct days, not distinct people | Identity-free data can't count people; one visitor can't satisfy multi-day | 2026-10-08 | Active |
| A/B stickiness via variant cookie gated by Kentico cookie level (K13-style), not contact hash | Needs no contact/identity; outlier-user exclusion dropped | 2026-10-08 | Active |

## Success Metrics

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Marketer creates a rule and sees it in preview without a deploy | Yes | - | Not started |
| Search count dedup correct across web-farm nodes | Matches searchId count | - | Not started |
| Search latency overhead of suite (rules + capture) | p95 < 10 ms | - | Not started |
| Works on SaaS and multi-instance self-hosted | Yes | - | Not started |

## Tech Stack / Tools

| Layer | Technology | Notes |
|-------|------------|-------|
| Runtime | .NET 10 (`net10.0`) only | Suite's own Directory.Build.props (isolated from solution root) |
| Platform | Xperience by Kentico ≥ 31.9.1 | Declared floor; Suite's own central package management |
| Search | Kentico.Xperience.Lucene.Core ≥ 15.0.5 | Floor matches XpSearch's exact pin so both load in xbk |
| Engine | Lucene.NET 4.8.0-beta00017 | Suggest/Analysis.Common at exact same beta |
| Admin UI | `@kentico/xperience-admin-components` + XbyK admin client module | Design kit repo: Personal/xperience-by-kentico-admin-design-components |
| Storage | SQL Server via module classes; SqlBulkCopy for raw events | |
| Host for testing | xbk (Dancing Goat) | |

## Links

| Resource | URL |
|----------|-----|
| Upstream | https://github.com/Kentico/xperience-by-kentico-lucene |
| Research | ../project-sidecar/docs/research/ |

---
*Created: 2026-10-08 · Last updated: 2026-10-09 after Phase 1*
