# XperienceCommunity.Lucene.Suite

## What This Is

An add-on suite for Xperience by Kentico sites that use the `Kentico.Xperience.Lucene` integration. It gives marketers admin tooling to tune search results (rules, synonyms, suggestions), measure search (analytics reports), compare rule sets with A/B tests, and preview queries side by side under a simulated contact-group or visitor context. Ground-up rewrite that replaces the unreleased XpSearch project.

## Core Value

Marketers tune, test and measure site search without a developer.

## Current State

| Attribute | Value |
|-----------|-------|
| Type | Application (XbyK add-on library: admin + live site) |
| Version | 0.1.0 |
| Status | v0.2 Measure: all phases (7–13, 8.1) complete; APPROVAL-CHECKS #4–#21 approved; ready to complete the milestone |
| Last Updated | 2026-10-13 |

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
- ✓ Identity-free search capture: one scrubbed raw row per tracked search in module class LuceneSuite.SearchEvent via background BulkInsert; searchId/sequence contract; session cookie only at Visitor/All; bot/prefetch/untracked/empty-index exclusions; web-farm-safe installer — Phase 2 (02-01)
- ✓ Public ISearchInteractionLogger (LogClick/LogConversion): identity-free click/conversion rows in LuceneSuite.SearchInteraction via the same background pipeline; synchronous argument validation, storage never throws; installer v2 with downgrade guard — Phase 2 (02-02)
- ✓ Analytics client contract (docs/analytics-client-contract.md) + shared executable test vectors (docs/analytics-test-vectors.json) for searchId minting, attribution and EventIds — Phase 2 (02-02)
- ✓ Every analytics/merge/attribution/retention/surfacing/job setting configurable from appsettings "LuceneSuite" with startup validation naming the key (histogram edges + UTC day fixed) — Phase 2 (02-03)
- ✓ Pure, deterministic rollup engine (dedupe, logical-search segmentation with duration cap, counted text, attribution with page window) driven by the shared vectors under default and non-default settings — Phase 2 (02-03)
- ✓ Daily KPI module class LuceneSuite.SearchDaily (unique Day/Index/Query), installer v3 — Phase 2 (02-03)
- ✓ Hosted web-farm-safe rollup job: every node runs it, one wins a non-blocking sp_getapplock per tick; completed UTC days rolled idempotently behind a transactional watermark; completeness floor never re-rolls a day from purged input — Phase 2 (02-04)
- ✓ Retention: raw purge never ahead of the next day's input window, RawHardLimit privacy ceiling for a stuck job, SearchDaily purge after DailyRetention, batched deletes — Phase 2 (02-04)
- ✓ Sub-threshold fold of SearchDaily rows into per-day "(other)" rows (QueryText NULL) with every per-day total preserved; browse ('') never folded — Phase 2 (02-04)
- ✓ Suite-owned "Site Search" admin application (Digital marketing) with its own permissions — View + Manage rules / Manage synonyms / Manage experiments; per-index section + query tester moved into it; role with only Site Search → View can tune search, Lucene-only role is refused; Lucene listing cogwheel is a permission-aware shortcut — Phase 3 (03-01)

- ✓ Query tester design/state fixes: latest-request-wins, content-hub Filters side panel, applied-filters bar, kit states — Phase 3 (03-02)
- ✓ Rules as structured JSON (LuceneSuite.Rule, installer v4) with strict validator (errors by JSON path) and a pure, allocation-light engine: conditions on query text (any/is/starts/ends/contains), facets and contexts; priority + UTC validity window — Phase 3 (03-03)
- ✓ Rules applied in search: cached RuleProvider (web-farm invalidation, invalid rules logged and skipped), hides excluded in the query, multiplicative boost/bury, fired rule ids + custom data + RulesFailed on the response, fail-open; ApplyRules/Contexts on requests — Phase 3 (03-04)
- ✓ Pins placed across pages (respect-filters option), counted in totals/facets, PinnedByRuleId on hits, p95 ≈ 8 ms with 300 pins — Phase 3 (03-05)
- ✓ Rules admin: native listing + editor gated on Manage rules (View is read-only), structured conditions/boosts editors with server validation, custom data, optimistic concurrency — Phase 3 (03-06)
- ✓ Pins/hides edited through an index-search item picker; query tester shows fired rules (linked, with custom data), pinned hits, rule failures, and an Apply rules switch — Phase 3 (03-07)

- ✓ Query-time synonyms per index: two-way groups and one-way entries (LuceneSuite.Synonym, installer v5), expanded as weighted query variants (0.8, max 20) through the query factory, fail-open, AppliedSynonyms on the response; ApplySynonyms request flag — Phase 4 (04-01)
- ✓ Synonyms admin: native listing + editor behind Manage synonyms with a tag/chip terms editor; query tester shows applied synonyms (linked) and can switch synonyms off — Phase 4 (04-02)

- ✓ Preview sandbox: query tester Compare mode — Baseline vs Tuned columns with their own rules/synonyms switches and simulated contexts (Kentico contact groups by code name, gated on Contact groups → View, plus free text), rank-change markers and missing items — Phase 5 (05-01)

- ✓ Optional Widgets package: rate-limited click/conversion endpoint (package-owned limiter) + client-settings endpoint, `sr=` result reference, dependency-free contract JS client proven against the shared vectors — Phase 6 (06-01)
- ✓ Server-rendered search box, results and facets view components sharing one URL-driven search per request (sid/seq continuation, Sec-Fetch-Site gating, safe tracked result links, toggle-link facets, no-store, contact-group contexts) — Phase 6 (06-02)
- ✓ Page Builder widgets (search box with results-page selector, results, filters with dimension labels); preview/edit mode never tracked; contact-group query DB-proven; CommunityProjects Site search page producing real Suite traffic — Phase 6 (06-03)

- ✓ Report query service over SearchDaily (internal): overview with previous-period deltas (partial periods → no deltas), daily trend with completeness from the watermark, surfaced query tables (4 sorts, paging) with one "(other)" row decided by SQL collation, histogram latency percentiles; ~100 ms per section at 400 days × 500 queries — Phase 7 (07-01)
- ✓ Dev-only seed runner (test project): contract-faithful synthetic traffic from index titles rolled by the real engine into never-rolled empty days (rollup lock, DB-name confirm, range clear); CommunityProjects seeded with 60 days — Phase 7 (07-02)

- ✓ Search reports admin page per index: preset/custom UTC day ranges, KPI tiles with previous-period deltas, token SVG trend + latency charts with table views — Phase 8 (08-01)
- ✓ Query table (Top / No results / Lowest CTR / Slowest, paged, (other)) with row actions that prefill the query tester, Create rule or Add synonym via a one-time path-bound sessionStorage handoff — Phase 8 (08-02)

- ✓ Admin UI consistency: every Suite admin screen audited against native Kentico 31.9.1 and fixed — native listing action bar + chips in the query tester, side-panel focus return, 24 px region rhythm, one-line listing-style row actions, editor error timing (after leaving a field/group or a failed save, inline and polite), bordered condition blocks; Suite admin UI Conventions 1–10 — Phase 8.1
- ✓ Search reports dashboard: period stepping, grouped selectable KPI tiles with a rolling-average trend, period breakdown and response-time side panels, Needs attention (top no-result queries with actions), daily series for every KPI — Phase 8.1 (08.1-03)

- ✓ Query suggestions: public ISuiteSuggestions over surfaced queries (90 days, MinSearches public floor, no-result rate < 50 %, no placeholders) + opt-in, rate-limited, strings-only /lucenesuite/suggest — Phase 9 (09-01)
- ✓ Accessible, CSP-safe search box autocomplete (ARIA 1.2 combobox ES module, opt-in per index, byte-identical when off) — Phase 9 (09-02)
- ✓ Blocked suggestions per index (exact / contains words): native listing + create + delete and one-click "Block suggestion" from the report query table, immediate on the endpoint, reports unaffected — Phase 9 (09-03)

- ✓ Kentico custom activities for tracked contacts: Search performed, Search without results, Search result clicked (installer v7, types disabled until a marketer enables them; consent and cookie level by Kentico; scrubbed text, query-free activity URLs; events contract v2 adds optional click query/title, activity-only) — Phase 10 (10-01)

- ✓ Search insights: five rule-based finding kinds (content gap, relevance miss, buried answer, slow query, regression) over one SQL aggregation of the range and previous period, thresholds in `LuceneSuite:Analytics:Insights`, ranked by searches actually affected, annotated with the synonyms/rules already in place; shown in the Search reports "Fix these first" card + "All insights" panel with pre-filled fixes and a fallback to the no-result list — Phase 11 (11-01, 11-02)

- ✓ Public API audited and frozen: 163 → 147 public types (unneeded rule/synonym models internal), LogClick(SearchClick), one Widgets namespace for MapLuceneSuite*, PublicApiAnalyzers baseline (PublicAPI.Shipped/Unshipped.txt), docs/public-api.md defining the supported boundary — Phase 12 (12-01)

- ✓ Enterprise screens per the design handoff: custom rules listing (derived status, status counts, search/sort/paging, bulk enable/disable/priority/delete with partial-failure results) — Phase 13 (13-01)
- ✓ Rule editor regrouped with a live status line, contact-group/custom context and facet value pickers, conflict alert with confirmed Reload, repeated saves (form reloads after a successful save), live validation through page commands (Save no longer flashes) and field messages for wrongly typed values — Phase 13 (13-02, 13-02.1, fixes)
- ✓ Search reports per handoff: tile sparklines, data bars, rows per page, empty-period callout, Retry with a graphics-only stale fade — Phase 13 (13-03)
- ✓ Query tester per handoff: summary strip with fired-rule chips (action summary), score bars, positions + rows per page, Compare tables with rank movement, Try again — Phase 13 (13-04)
- ✓ Audit trail: every create/update/delete of a rule, synonym or blocked suggestion writes one Kentico event-log entry with the user (source LuceneSuite; bulk rule actions as one summary; retention follows CMSLogSize); rule validity fields labelled as local time — Phase 13 (13-05)

### Active (In Progress)

### Planned (Next)
- v0.3 Relevance: per-item popularity data (14), query-time stopwords (15), field-weights editor (16), typo tolerance (17), "Did you mean" + curated suggestions (18), popularity boost (19)
- v0.4 Test: A/B testing of rule sets + comparison reports
- v0.5 Instant Search: public, opt-in search endpoint (index + field allow-list, rate limit, searchId analytics) and an Algolia-InstantSearch-style JS widget library

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
- Admin UI lives in the Suite-owned "Site Search" application (Digital marketing): read-only index listing → per-index "Search tuning" section (/admin/site-search/indexes/{indexId}/…); no Suite page is parented to a Kentico Lucene page; access follows Site Search permissions (VIEW + custom LuceneSuite.Manage*). Only link to Kentico Lucene's admin: a PageExtender cogwheel on its index listing, hidden without Site Search → View via the Kentico-internal CMS.Membership.Internal.IApplicationPermissionEvaluator (public cross-app evaluator is internal in 31.9.1). Every routed node carries its own [UIEvaluatePermission] and index-exists ValidatePage (Kentico validates only the requested node)
- Admin client: Proxy mode (webpack dev server, port 3011) in development; Release build runs npm and embeds the bundle

### Business Constraints
- Public community package (MIT, matching upstream)
- XpSearch (F:\Personal\CommunityProjects\libraries\xperience-search) is loose reference only — never a basis for decisions

### Compliance Constraints
- Analytics/reporting store KPI/metric data only — never contact IDs, IPs or hashes. Identity-free is bounded by Kentico cookie level/consent: below Visitor, searches correlate only by a random per-search ID held in page memory; at Visitor/All, a Suite session cookie (random, registered with Kentico at Visitor level, 30-min sliding, no contact link) additionally groups searches across page loads
- IP used in memory for bot rate limiting only, never persisted
- Custom activities go through Kentico activity tracking and its consent/cookie level — the suite adds no gate of its own
- A/B participation uses a suite variant cookie (test + variant only, no identifier) registered with Kentico at a configurable cookie level; it is set only when the current Kentico cookie level allows it. Visitors without it are served control and excluded from experiment metrics. Events carry the variant only
- Surfacing threshold: a query appears in reports, seeds suggestions or prompts rule creation only after ≥ 5 searches on ≥ 2 distinct days (both configurable); counted from the first search; covers both business noise and k-anonymity
- Rollup rows still below threshold 30 days after last seen are folded into a per-day "(other)" bucket (QueryText NULL) and deleted, so totals are preserved while rare query text disappears
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
| Admin UI is a per-index section inside the Lucene app, sibling of IndexEditPage (option A) | Per-index tuning (user); Edit-template pages only allow side-panel/dialog children | 2026-10-09 | Superseded 2026-10-09 |
| Admin UI moves to a Suite-owned "Site Search" application (Digital Marketing) with its own permissions (View + Manage rules/synonyms/experiments); per-index structure kept; Lucene listing cogwheel is a shortcut | Kentico custom permissions can only be declared on an application class the Suite owns; marketers must not need Lucene index rights | 2026-10-09 | Active |
| Admin client Proxy in dev, embedded on Release via MSBuild npm step | User decision; matches Kentico's documented dev/deploy split | 2026-10-08 | Active |
| Core exposes ISearchInteractionLogger (like ICustomActivityLogger); HTTP endpoint, JS client, tag helpers and widgets ship in optional package XperienceCommunity.Lucene.Suite.Widgets (Phase 6) | User decision: custom UIs/endpoints integrate via the C# API | 2026-10-09 | Active |
| All analytics/merge/retention settings configurable via SuiteSearchOptions bound from appsettings "LuceneSuite" (validated on start), except histogram bucket edges and the UTC day boundary (fixed: changing them corrupts stored history); admin settings page deferred | User decision 2026-10-09 | 2026-10-09 | Active |
| Buffered bulk writes for raw events, drop on overflow | Never slow search | 2026-10-08 | Active |
| Consented visitors get a Suite session cookie (option a), not the contact GUID | Cross-page session merging without making analytics personal data; no collector/eraser | 2026-10-09 | Active |
| Rollups/retention run as a hosted background job on every node, single-runner via sp_getapplock, idempotent per-day recompute | User decision; no admin setup, web-farm safe | 2026-10-09 | Active |
| Rollup watermark is a hidden settings key read uncached inside the lock and written in the day's CMSTransactionScope with BulkDelete/BulkInsert (enlistment proven) | Stale or reset watermark can never wipe correct KPIs | 2026-10-09 | Active |
| RawRetention minimum = 1 day + 3 × AttributionWindow + FlushInterval + 5 min + RollupInterval + 1 h; RawHardLimit (7 d) caps raw query text when the job is stuck | Every day stays rollable; privacy bound holds under failure | 2026-10-09 | Active |
| Analytics tables are Kentico module classes written via InfoProviderBulkExtensions (BulkInsert/BulkUpdate/BulkDelete), CI/CD explicitly disabled; no plain SQL tables | User decision; Kentico 31.9.1 bulk APIs verified | 2026-10-09 | Active |
| Consent delegated entirely to Kentico cookie level / activity tracking | Platform-consistent; works for US and EU without suite-specific logic | 2026-10-08 | Active |
| Analytics are identity-free KPI data; no suite collector/eraser needed | No personal data stored → nothing to access/erase; counts all visitors | 2026-10-08 | Active |
| Surfacing threshold = count + distinct days, not distinct people | Identity-free data can't count people; one visitor can't satisfy multi-day | 2026-10-08 | Active |
| A/B stickiness via variant cookie gated by Kentico cookie level (K13-style), not contact hash | Needs no contact/identity; outlier-user exclusion dropped | 2026-10-08 | Active |
| Rules stored as structured JSON on one module class, evaluated by a pure engine (not Kentico ConditionBuilder macros) | User decision; testable, fast, no macro surface | 2026-10-10 | Active |
| Pins/hides pick items by searching the index (store WebPageItemGUID / ContentItemGUID as indexed), not Kentico content selectors | Content selectors return ContentItemGUIDs, which don't match indexed pages | 2026-10-11 | Active |
| Rule admin uses native Kentico listing/edit pages; custom React only for structured fields; page context reaches component commands via GetFormItems, never the client | Native look and permissions; trust boundary | 2026-10-11 | Active |
| Synonyms: two-way + one-way, query-time variants weighted 0.8 below exact (a preference, not a guarantee), per index; rules and analytics see the original text | User decisions; no rebuild per edit; any ISearchQueryFactory keeps working | 2026-10-11 | Active |
| Preview = compare mode inside the query tester; simulated contexts are the strings the site would send (membership not evaluated); contact groups need Kentico Contact groups → View | User decisions; no duplicate page; no cross-app data leak | 2026-10-11 | Active |
| Widgets package: server-rendered view components + Page Builder widgets over one URL-driven search per request; page size and contact-group contexts are site-wide options; sr= always on same-site result links; preview/edit never tracked | User decisions (Phase 6); shared search makes per-widget settings conflict | 2026-10-11 | Active |
| Package endpoints never reconfigure the host (own rate limiter; no AddRateLimiter/UseRateLimiter) | A library must not change host-wide middleware behaviour | 2026-10-11 | Active |
| Human-verify checkpoints are deferred to .paul/APPROVAL-CHECKS.md and reviewed at milestone end | User decision | 2026-10-11 | Active |
| v0.2 scope: reports + seed generator + suggestions (suggest endpoint returns strings only — extends v0.1's "no public search API") + blocklist (suggestions only) + custom activities (search/click/no results); settings stay in appsettings | User decisions (discuss-milestone) | 2026-10-12 | Active |
| Reports read SearchDaily only; "(other)" = range totals − named totals (surfaced membership decided by SQL, never C# string matching); the report service performs no authorization (admin pages gate on View + index exists) | 07-01 (NOT IN over nullable QueryText timed out at volume; collation defines text equality) | 2026-10-12 | Active |
| Demo history is seeded only by a dev runner in the test project, written without Kentico into empty days no real process can own, behind a DB-name confirmation and the rollup lock | 07-02 / audit (running host, operator error) | 2026-10-12 | Active |
| Test host content changes stay minimal and recorded: host files backed up before edits (byte-preserving), new pages rather than reusing existing ones, temporary data cleaned unless the user keeps it | User decisions across v0.1 | 2026-10-12 | Active |
| Layered packages (user, 2026-10-12): (1) Core + Admin alone expose a documented public C# API complete enough for a custom UI; (2) Widgets ships tag helpers/widgets out of the box plus public building blocks to author your own; (3) an instant-search JS library over a public search endpoint (v0.4). Supersedes "no public search API" from v0.4 on; until then the only public HTTP surfaces are events, settings and strings-only suggestions | User direction during 09-01 planning | 2026-10-12 | Active |
| Admin UI follows Suite Conventions 1–10 (08.1-01-FINDINGS.md): reference precedence native 31.9.1 runtime > npm exports > kit repo; listing pages on the desk, dashboards in Papers, editors in native forms; errors only after leave/save; side panels return focus; tokens verified at runtime; npm Spacing values are strings. Rules mirrored into the design-screen skill (enterprise.md) | Earlier per-screen validation let cross-screen drift through (08.1 audit, user) | 2026-10-12 | Active |
| Report row actions that only write one record run as a page command on the report page (success toast, errors in the page's NotificationBar), not a handoff; blocklist has no edit page (delete + re-add) | User decisions 09-03 | 2026-10-12 | Active |
| Search activities: Suite activity types are installed disabled and never updated (marketer opt-in in Kentico); logged in Core for submitted searches only; Suite-written activities always set ActivityURL/Referrer without query strings (Kentico MVC defaults to the raw Referer); click query/title are browser-supplied, activity-only | User decisions + 10-01 audit/APPLY | 2026-10-12 | Active |
| Insights: computed on read (no storage); "searches affected" = searches actually lost to the problem (regression = searches × rate change; slow = searches in latency buckets wholly at or above the cutoff); latency thresholds hold only when the whole bucket is at or above them | Phase 11 (user + host finding in 11-02) | 2026-10-12 | Active |
| Public API: PublicApiAnalyzers baseline in the three packages (every change is a PublicAPI.Unshipped.txt diff; move to Shipped at release); admin services (reports, insights, rule/synonym/blocklist management) stay internal by design; supported boundary in docs/public-api.md | Phase 12 (user) | 2026-10-12 | Active |
| v0.3 Relevance milestone added before Test (Test → v0.4, Instant Search → v0.5): query-time admin-managed stopwords, field-weights editor (appsettings as defaults), fuzzy typo variants below exact, Did-you-mean + curated suggestions, popularity boost with per-item click data collected first; rewrite order stopwords → synonyms → typos → field weights → rules | User decisions | 2026-10-12 | Active |
| Phase 13 handoff deviations (npm 31.9.1 / enterprise.md precedence): Spinner instead of skeletons; text never faded (stale = graphics at 60 % + a line); Compare keeps configurable Baseline/Tuned columns instead of a fixed tuned/untuned pair; pins keep typed positions (drag order would drop fixed slots) | User decisions + audits | 2026-10-13 | Active |
| Live validation and status in Suite admin forms use page commands, not form-component commands (Kentico marks the form busy for every form-component command); successful edit saves navigate to the same page (Kentico applies returned values only on failed submits) | 13-02.1 + post-13-03 host findings | 2026-10-13 | Active |
| Suite admin data changes are audited in Core through TYPEINFO object events (one entry per change, user stamped by Kentico); bulk operations mute per-item entries with CMSActionContext LogEvents = false and log one summary; the trail lives in Kentico's event log, so its retention is the site's CMSLogSize | v0.2 milestone review (user) + 13-05 audit | 2026-10-13 | Active |
| Admin cross-page prefill = one-time sessionStorage handoff bound to target, index, exact page path and expiry; consumers fill only empty forms; never URL query strings or server state | 08-01 (URL state raced the admin router) / 08-02 audit | 2026-10-12 | Active |

## Success Metrics

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Marketer creates a rule and sees it in preview without a deploy | Yes | Yes — query tester Compare mode with simulated contexts (05-01) | Met |
| Real Suite traffic from the live site | Yes | Yes — CommunityProjects /site-search widgets record searches, continuations and clicks (06-03) | Met |
| Search count dedup correct across web-farm nodes | Matches searchId count | - | Not started |
| Search latency overhead of suite (rules + capture) | p95 < 10 ms | ≈ 8 ms p95 with 300 pins + 500 rules (Debug, 5 000 docs, 03-05); one flake under parallel load (05-01) | On track |
| Works on SaaS and multi-instance self-hosted | Yes | - | Not started |

## Tech Stack / Tools

| Layer | Technology | Notes |
|-------|------------|-------|
| Runtime | .NET 10 (`net10.0`) only | Suite's own Directory.Build.props (isolated from solution root) |
| Platform | Xperience by Kentico ≥ 31.9.1 | Declared floor; Suite's own central package management |
| Search | Kentico.Xperience.Lucene.Core ≥ 15.0.5 | Floor matches XpSearch's exact pin so both load in xbk |
| Engine | Lucene.NET 4.8.0-beta00017 | Suggest/Analysis.Common at exact same beta |
| Admin UI | `@kentico/xperience-admin-components` + XbyK admin client module | Design kit repo: F:\Personal\xperience-by-kentico-admin-design-components |
| Storage | SQL Server via module classes; SqlBulkCopy for raw events | |
| Live-site deps | Kentico.Xperience.WebApp 31.9.1 + Microsoft.AspNetCore.App framework reference in Suite core | Added 02-01 for cookie levels; acceptable because the core package is the live-site package |
| Host for testing | CommunityProjects (Dancing Goat), F:\Personal\CommunityProjects\CommProjects.sln | Untracked; replaced xbk 2026-10-10; XpSearch unwired |

## Links

| Resource | URL |
|----------|-----|
| Upstream | https://github.com/Kentico/xperience-by-kentico-lucene |
| Research | project-sidecar/docs/research/ (git-ignored) |

---
*Created: 2026-10-08 · Last updated: 2026-10-13 after Phase 13 (v0.2 phases complete)*
