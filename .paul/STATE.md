# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-12) · Milestones: .paul/MILESTONES.md

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.2 Measure — Phase 8 Reports Admin

## Current Position

Milestone: v0.2 Measure (v0.2.0)
Phase: 8 of 10 (Reports Admin) — Planning
Plan: 08-02 created + audited, awaiting approval
Status: PLAN created, ready for APPLY
Last activity: 2026-10-12 — Audited .paul/phases/08-reports-admin/08-02-PLAN.md (08-02-AUDIT.md)

Progress:
- v0.1 Tune & Capture: [██████████] 100% ✓
- v0.2 Measure: [██░░░░░░░░] 25% (1 of 4 phases; Phase 8: 1 of 2 plans)

## Loop Position

Current loop state:
```
PLAN ──▶ APPLY ──▶ UNIFY
  ✓        ○        ○     [Plan created, awaiting approval]
```

## Accumulated Context

### Decisions
See PROJECT.md Key Decisions.

| Decision | Phase | Impact |
|----------|-------|--------|
| 2026-10-12: Enterprise audit on 08-02-PLAN.md. Applied 1 must-have, 3 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 8 | Handoff bound to the exact target page path (never lands in an existing rule/synonym edit page); {email}/{number} placeholder texts cannot become rules/synonyms (server HasPlaceholder); prefilled tester search untracked; report range/sort/page restored per index from sessionStorage |
| 2026-10-12: 08-02 planning (user choices): row-action prefill via one-time sessionStorage handoff {target, index, query, expiry} read by our own client pieces (query tester, rule conditions editor, synonym terms editor) — no URL query strings, no server state; one query table with sort switch, 25/page, (other) line; actions without permission shown disabled with a tooltip. Design-screen in PLAN: npm Table ActionCell (focusable icon buttons) avoids the row-focus gap | Phase 8 | 08-02 created |
| 2026-10-12: 08-01 applied: SearchReportsPage resolves the internal SearchReportService via IServiceProvider (public page ctor cannot take internal types); URL range persistence dropped (replaceState raced Kentico's admin router → /admin/?from=…); admin 31.9.1 runtime lacks the kit repo's newer CSS variables (--color-background-primary, --spacing-*, --font-size-*) → chart uses runtime tokens (--color-icon-highlighted etc.), npm Spacing enum, relative font size; no CSS loader in the client → inline token styles (no reports.css); DateTimeRangeInput ships its own non-removable shortcuts | Phase 8 | 08-02 reuses the page and the runtime-token rule |
| 2026-10-12: Enterprise audit on 08-01-PLAN.md. Applied 1 must-have, 4 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 8 | UTC-day strings end to end (no toISOString / Date parsing; TZ-tested); rate deltas in percentage points; command failures shown with last result kept; host watermark checked first; URL persistence dropped if it conflicts with the admin router |
| 2026-10-12: Phase 8 planning (user choices): trend = token-styled SVG chart + "Show as table" (npm 31.9.1 exports no ColumnChart; amCharts not externalized → own unlicensed ~1 MB bundle); range = presets (7/30/90 days, 12 months) + custom DateTimeRangeInput; row actions = open in query tester, create rule for query, add synonym; two plans (08-01 page/range/KPIs/trend/latency, 08-02 query tables + row actions). Design-screen in PLAN: composed Stack/Row/Card layout, InfoCard KPIs (strings only → delta in details), NameToggleButtons presets | Phase 8 | 08-01 created |
| 2026-10-12: 07-02 applied: seed runner in tests/Seeding (generator → RollupEngine → SqlBulkCopy, RollupLock + serializable range re-read, DB-name confirm); texts normalized (lowercase/trim) because the SearchDaily unique index is case-insensitive and ignores trailing spaces; host comm_projects seeded DancingGoatSample 2026-08-12..2026-10-10 (3,477 rows, 7,725 searches; APPROVAL-CHECKS #4 with clear command) | Phase 7 | Phase 8 has 60 days of host history |
| 2026-10-12: Enterprise audit on 07-02-PLAN.md. Applied 1 must-have, 4 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 7 | LUCENESUITE_SEED_CONFIRM = target DB name; seeder holds RollupLock and re-reads limits in the write transaction; clicks bounded by ResultCount; docs warn seeded history affects real queries' surfacing |
| 2026-10-12: 07-02 planned: seeder writes via SqlBulkCopy without initializing Kentico against the target (no web-farm/cache side effects on the running host); range = N days before min(first daily day, watermark+1, earliest raw day, today); clear by explicit range; host range 2026-08-12..2026-10-10 (host: no daily rows, no watermark, raw from 2026-10-11) | Phase 7 | 07-02 |
| 2026-10-12: 07-01 applied: "(other)" = range totals − named totals (NOT IN over the nullable QueryText timed out at 30 s on 200k rows; subtraction is exact because named + other partition the range, surfaced membership still decided by SQL); sums via Kentico AggregatedColumn; percentiles with decimal rank; volume 400 days × 500 queries: overview 104 ms, trend 105 ms, queries 119 ms, latency 86 ms | Phase 7 | Phase 8 renders from SearchReportService |
| 2026-10-12: Enterprise audit on 07-01-PLAN.md. Applied 1 must-have, 4 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 7 | Surfaced membership decided in SQL (collation), CompleteThrough/IsComplete for unrolled days, PreviousIsPartial nulls deltas, future To rejected, Phase 8 owns authorization |
| 2026-10-12: Phase 7 planning (user choices): seed generator = opt-in test runner (LUCENESUITE_SEED_CONNECTIONSTRING, nothing packaged); vocabulary + clicked items from the index (built-in list for the test DB); seed the host with ~60 removable days for DancingGoatSample; overview compares with the previous period. Seeder runs the real RollupEngine in memory and writes SearchDaily only for days before the job start (watermark or earliest raw day) and only into empty ranges (DailyRollupStore.Replace would move the watermark). Two plans: 07-01 report service, 07-02 seed generator + host seeding | Phase 7 | 07-01 created |
| 2026-10-12: Milestone v0.2 Measure created (user, discuss-milestone): 4 phases — 7 Report Data (report queries + dev-only seed generator through the real rollup), 8 Reports Admin, 9 Suggestions (strings-only rate-limited suggest endpoint + progressive-enhancement script; blocklist hides suggestions only), 10 Custom Activities (search performed / result clicked / no results via Kentico consent); no admin settings page | Milestone | Next: /paul:plan Phase 7 |
| 2026-10-12: Milestone v0.1 Tune & Capture completed: MILESTONES.md entry, archive .paul/milestones/v0.1.0-ROADMAP.md, version aligned to 0.1.0 (PROJECT, config, paul.json), tag v0.1.0 | Milestone | Next: /paul:discuss-milestone for v0.2 Measure |
| 2026-10-12: 06-04 applied: host ContentType facet = DataClassInfo display name (fallback code name); index rebuilt via Lucene admin rotate button; counts unchanged | Phase 6 | v0.1 follow-up closed |
| 2026-10-12: v0.1 milestone review (user): APPROVAL-CHECKS #2 and #3 approved; facet values fixed in the host index strategy (not a widget value-label property); test traffic kept | Milestone | Follow-up host quick fix, then complete milestone |
| 2026-10-11: 06-03 applied: widgets need [assembly: AssemblyDiscoverable] (asserted); RegisterWidgetAttribute types only readable via GetCustomAttributesData; Kentico preview URLs are hash-signed so the search box is disabled in all preview (not only edit); host landing-page template allow-list extended with the XperienceCommunity.LuceneSuite. prefix; new host page "Site search" /site-search (XpSearch "Search" page untouched, user); e2e search bdefc913… seq 1–3 + click seq 3 pos 2 verified; host edits must preserve BOM/CRLF (byte edits) | Phase 6 | v0.1 complete; real traffic flowing for v0.2 |
| 2026-10-11: Enterprise audit on 06-03-PLAN.md. Applied 1 must-have, 3 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 6 | Widgets assembly needs [assembly: AssemblyDiscoverable] or RegisterWidget is never scanned; preview detection fails closed; edit-mode search box inert; docs warn about <cache>/output cache/CDN |
| 2026-10-11: 06-03 planning (user choices): search box widget targets the results page via a page selector (empty = current page); keep a permanent Search landing page on CommunityProjects (ongoing traffic; its analytics rows are kept); facet dimension labels as a widget property. Plan adds preview/edit-mode exclusion in SuitePageSearch (core capture excludes only bots/prefetch) | Phase 6 | Last v0.1 plan; checkpoint → APPROVAL-CHECKS #3 |
| 2026-10-11: 06-02 applied: SuitePageSearch public (internal ctor, factory DI; view components need public ctor types); contacts via ICurrentContactProvider.GetExistingContact; Kentico Lucene Url = WebPageUrl.RelativePath (~/…) → Url.Content; Razor renders null data-* attributes (JS treats empty as absent); IProgressiveCache dependency only when settings.Cached; 06-01 doc had two cp1252 § bytes (repaired; always write with utf-8) | Phase 6 | 06-03 wraps the view components as widgets and verifies the contact-group query on the host |
| 2026-10-11: User: human-verify checkpoints no longer block APPLY — each is recorded as a Pending entry in .paul/APPROVAL-CHECKS.md and reviewed together at the end of the milestone (v0.1, after 06-03); decisions and blockers still stop | All phases | 06-02 checkpoint is entry #2; future plans phrase checkpoints as entries |
| 2026-10-11: Enterprise audit on 06-02-PLAN.md. Applied 2 must-have, 3 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 6 | Hit hrefs allow-listed (relative or http/https; sr= + beacons same-site only); search responses no-store + output cache storage off; unknown index loud, outages render "unavailable"; URL-selected facets always removable; rel=nofollow on paging/facets |
| 2026-10-11: 06-02 planning (user choices): view components with overridable Razor views (06-03 widgets wrap them); facets as toggle links (no JS); sr= always on result links; hit title/URL/summary via field-name parameters (defaults Title / Kentico Url / none). Plan: one search per request per index shared by all components; URL q/page/f.{dim}/sid/seq; sid continues only on same-origin navigation (Sec-Fetch-Site); page size + contact-group contexts as site-wide options | Phase 6 | Departs from "contact groups as a widget property" (shared search) — user approved the plan with site-wide options 2026-10-11 |
| 2026-10-11: 06-01 applied: Widgets package (Razor RCL) with package-owned per-IP limiter acquired in the handler (host RateLimiterOptions untouched, no UseRateLimiter); InternalsVisibleTo Widgets on core (reuses LogThrottle, MaxResultWindow); public API approved (/lucenesuite/events, /settings, sr=, JS exports incl. norm/osa/typo); C# pitfall: `cond ? null : Memory<byte>` yields empty Memory via byte[] conversion; node 24 `t.mock.property(globalThis, "navigator")` hangs → defineProperty | Phase 6 | 06-02 tag helpers use SuiteResultReference, SuiteClientSettings.EventsUrl and the _content asset |
| 2026-10-11: Enterprise audit on 06-01-PLAN.md. Applied 1 must-have, 3 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 6 | Events endpoint owns its PartitionedRateLimiter (never touches host RateLimiterOptions, no UseRateLimiter dependency); EventsPerMinute 0 disables + throttled warning for proxy/CDN; bounded body read incl. chunked; eventId optional |
| 2026-10-11: Phase 6 planning (user choices): server-rendered widgets (no public search API); Search box + Results + Facets as separate widgets; visitor contact groups passed as contexts (widget property, default on, only with a current contact); three plans (06-01 package + events endpoint + JS client, 06-02 tag helpers + server rendering, 06-03 Page Builder widgets + host wiring); full contract JS client built now despite server-rendered widgets continuing searches via server-rendered sid/seq links (user overrode the click-beacon-only recommendation) | Phase 6 | Last v0.1 phase |
| 2026-10-11: 05-01 applied: kit breakpoints are md 1366–1919 / lg ≥ 1920 (use colsMd for desktop side-by-side; earlier colsLg-only tweaks apply from 1920 only); --color-text-high-emphasis = brand purple, body text = --color-text-default-on-light; contact groups gated with IApplicationPermissionEvaluator on ContactGroupsApplication.IDENTIFIER; rules-overhead perf test flaked once under parallel load (passes alone, 7.4–8.8 ms) | Phase 5 | Patterns for Phase 6 and later admin UI |
| 2026-10-11: Enterprise audit on 05-01-PLAN.md. Applied 1 must-have, 3 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 5 | ContactGroups command also requires Kentico Contact groups View (no cross-app leak); rank by first occurrence per ItemGuid (multilingual); previews proven untracked; truncation flag at 500 |
| 2026-10-11: Phase 5 planning (user choices): compare mode inside the query tester (no new page); contexts = contact groups (sent as code names) + free text; each column configurable (defaults Baseline = rules/synonyms off, Tuned = on); rank-change markers ↑/↓/New + missing items. Design-screen in PLAN: no npm MultiSelect → contact groups in a SidePanel Checkbox list; hit lists as focusable buttons (avoids Table row focus gap); one plan | Phase 5 | Closes the v0.1 tuning loop |
| 2026-10-11: 04-02 applied: empty custom-field value + RequiredValidationRule so errors appear only at save (31.9.1 skips Required on load/change and never validates invisible fields); listing formatter reads sibling columns from the row; chip editor commits typed terms on blur; npm Input lacks maxLength/onKeyDown (DOM maxLength via inputRef, Enter via onKeyPress) | Phase 4 | Patterns for Phase 5 admin |
| 2026-10-11: Enterprise audit on 04-02-PLAN.md. Applied 1 must-have, 4 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 4 | Typed-but-unadded term committed on blur (no silent loss); SerializeTerms keeps non-ASCII literal; shared MatchKey vectors C#/node; chip list semantics; synonym ownership DB test |
| 2026-10-11: 04-02 planning (user choices): terms edited in a custom tag/chip component (not a native text area); query tester gets an "Apply synonyms" switch (SuiteSearchRequest.ApplySynonyms, default true); applied synonyms link to their edit pages. Design-screen in PLAN: native ListingPage/ModelEditPage with RadioGroup Kind + VisibleIfEqualTo Input; npm Tag remove buttons are focusable and labelled, no invalid state → per-term error lines | Phase 4 | Last Phase 4 plan |
| 2026-10-11: 04-01 applied: synonyms expand through ISearchQueryFactory as DisjunctionMaxQuery(0) variants (boost × 0.8); one MatchKey for terms and queries; shared fail-open rerun now sets RulesFailed only when rules fired; host "mill" example shows weight is a preference (user kept 0.8); installer v5 on comm_projects | Phase 4 | 04-02 admin edits SynonymInfo via SynonymSerializer paths; query tester adds AppliedSynonyms |
| 2026-10-11: Enterprise audit on 04-01-PLAN.md. Applied 2 must-have, 5 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 4 | Fail-open covers variant query building (rerun without rules+synonyms); variant boost multiplied not overwritten; one MatchKey for entries and queries; weight documented as preference |
| 2026-10-11: Phase 4 planning (user choices): two-way + one-way synonyms (no replace type); synonym matches slightly below exact (weight 0.8, configurable); per index under Search tuning (Manage synonyms); two plans — 04-01 model + installer v5 + query expansion, 04-02 admin + query tester. Expansion = DisjunctionMaxQuery of factory(original) and single-substitution variants (works with any ISearchQueryFactory); rules and analytics see the original text | Phase 4 | 04-02 builds admin on the 03-06/03-07 patterns |
| 2026-10-11: 03-07 applied: component commands get the page's index via a RuleEditPageBase.GetFormItems override (31.9.1 ComponentCommand calls it); cross-field picker exclusion via FormComponentProps.fieldValues; ActionsJson built from the form only (serializer disallows unknown members); SidePanel content focused by mount ref; GetPath is admin-root-relative → client prefixes admin base | Phase 3 | Patterns for Phase 4 admin |
| 2026-10-11: Enterprise audit on 03-07-PLAN.md. Applied 2 must-have, 2 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 3 | Picker de-duplicates by ItemGuid (multilingual); commands fail closed (unresolved index, Describe cap 350); pinned+hidden refused on the Hides field via a cross-field rule; fired-rule names in one query |
| 2026-10-11: 03-07 planning (user choices): one standard plan (pins/hides editor + query tester); query tester gets an "Apply rules" switch. Design-screen in PLAN: picker = SidePanel with labelled Input + Checkbox list (npm lacks SearchInput; Table rows not focusable); core SuiteItemLookup internal with InternalsVisibleTo Admin; index name for component commands from the page, never the client | Phase 3 | Last Phase 3 plan; checkpoint closes 03-02/03-06 verification gaps |
| 2026-10-11: 03-06 applied: EditPageBase.Submit/Change require system Update and native CreatePage requires Create in 31.9.1 → both overridden/replaced with Manage rules gates (ModelEditPage for create); child-page links need ancestor PageParameterValues; server-only validation rules via always-pass client rule; concurrency token in a hidden custom component; per-control errors after blur/failed save; mapper on StoredRule (RuleInfo needs Kentico) | Phase 3 | Patterns reused by 03-07 and Phase 4 admin |
| 2026-10-11: Enterprise audit on 03-06-PLAN.md. Applied 3 must-have, 3 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 3 | Native submit/delete gated on Manage rules (app declares no system Create/Update/Delete); rule-belongs-to-index checks; pins/hides re-read at submit; optimistic concurrency; UTC round trip |
| 2026-10-11: 03-06 planning (user choices): pins/hides chosen with an index-search picker (index stores WebPageItemGUID for pages, ContentItemGUID for reusable items — Kentico content selectors would not match pages); 03-06 split → 03-06 listing + editor (details, conditions, boosts, custom data), 03-07 pins/hides editor + query tester rule display. Design-screen run in PLAN: native ListingPage/ModelEditPage (npm 31.9.1 lacks the kit templates, ConfirmationDialog, ActionButtons, NumberInput); custom React only for conditions and boosts | Phase 3 | Phase 3 now 7 plans |
| 2026-10-11: 03-05 applied: pinned window documents found by doc-id membership, ItemGuids via per-segment FieldCache (20.3 → ≈8 ms p95, 300 pins); DrillSideways counts of a filtered dimension include filter-ignoring pins; host index confirmed multilingual (es docs share ItemGuids) | Phase 3 | 03-06 can show PinnedByRuleId; dropped pins not reported yet |
| 2026-10-10: Enterprise audit on 03-05-PLAN.md. Applied 2 must-have, 2 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 3 | One hit per pinned ItemGuid in multilingual indexes; TotalHits = run-1 total − passing pinned docs + available pins (equals pageable length) |
| 2026-10-10: 03-05 planning (user choices): pinned items count in TotalHits and facets via a zero-weight, coord-disabled pinned clause in the match set; filter-ignoring pins add to the total only; pins show regardless of query text (Algolia); standard track | Phase 3 | Unpinned scores must stay identical (proven by test) |
| 2026-10-10: 03-04 applied: boosts nest one BoostingQuery per boost, capped at the 200 strongest (supersedes the audit's group-by-strength bound, which broke same-strength stacking); fail-open reruns unruled for any non-ArgumentException; RuleProvider.GetRules virtual as test seam; host boosts use Kentico's ContentTypeName StringField (ContentType is facet-only) | Phase 3 | 03-05 pins plug into SearchWithRules / SearchExecutor; 03-06 shows FiredRuleIds/UserData/RulesFailed |
| 2026-10-10: Enterprise audit on 03-04-PLAN.md. Applied 2 must-have, 3 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 3 | Bounded query (one TermsFilter MUST_NOT for hides; boosts grouped by strength ≤ 199 levels); fail-open covers rule application; RulesFailed flag on the response |
| 2026-10-10: 03-04 planning (user choices): split pins into new 03-05 (admin UI → 03-06); boost/bury = score × MaxBoostFactor^(strength/100), MaxBoostFactor default 10 (LuceneSuite:Rules), via Lucene.Net.Queries BoostingQuery; hides = MUST_NOT ItemGuid in the query (excluded from totals and facets); rules fail open on load errors | Phase 3 | 03-05 pins post-process on top of 03-04; PROJECT "duplicate pins take best position" research note already superseded by priority (KNOWN-LIMITATIONS) |
| 2026-10-10: 03-03 rule model + engine: malformed JSON → root-path error, member errors keep their path; null/blank JSON = empty; engine whole-word matching via boundary substring (no allocations, ≈0.5 ms / 500 rules); RuleEngine types internal; disposable comm_projects_test DB set up | Phase 3 | 03-04 loads RuleInfo → ToDefinition → skip+log invalid → Evaluate; decides public surface |
| 2026-10-10: Enterprise audit on 03-03-PLAN.md. Applied 4 must-have, 5 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 3 | Engine normalizes input without PII placeholders; invalid stored rules fail safe (03-04 must log skipped ids); pins past 300 dropped; Hides sorted |
| 2026-10-10: /xbyk-admin-ui:design-screen required for every custom admin page (PLAN records screen classification + template/component map; APPLY re-invokes before markup); kit asset path fixed to KENTICO_ADMIN_DS (F:/Personal/xperience-by-kentico-admin-design-components) | Pre-03-03 | 03-05, Phase 4/5 admin pages and the admin settings page; UNIFY logs a gap if not invoked |
| 2026-10-10: 03-03 rules stored as structured JSON (conditions array + actions object on one LuceneSuite.Rule class), evaluated by a pure engine — not Kentico ConditionBuilder macros (user decision) | Phase 3 | 03-05 builds its own condition editor from kit components; ROADMAP 03-05 'ConditionBuilder editor' wording superseded |
| 2026-10-10: Test host switched xbk → CommunityProjects (F:\Personal\CommunityProjects\CommProjects.sln, untracked). XpSearch unwired (lib kept in libraries/xperience-search as reference only, not source of truth); host Kentico 31.8.0 → 31.9.1 + DB update (backup comm_projects_pre-31.9.1_2026-10-10.bak); plain DefaultLuceneIndexingStrategy (Title ×3 / Body / ContentType facet) + AddLuceneSuite + LUCENE_48; Development proxy 3011; file backups in project-sidecar/backups/host-swap-2026-10-10 | Pre-03-03 | All later plans verify against CommunityProjects; leftover XpSearch DB objects pending user decision |
| 2026-10-10: 03-02 query tester: state logic as node-tested pure helpers (latest-request-wins + abort, selected merge); user-requested content-hub Filters SidePanel (draft; Clear all / Cancel / Apply) + applied-filters bar composed from exported npm components (kit FilterPanel/FilterStatusIndicator not exported in 31.9.1); admin shell root `.app` sets `color: red` → never leave text unstyled; npm Table rows (display:contents buttons) can't take focus → focus returns to the table wrapper; app identifier/slug/permission names pinned as literals in tests; admin UI design validation now required | Phase 3 | 03-05 reuses the filter/side-panel/state patterns and must solve keyboard access to rows |
| 2026-10-09: 03-01 Site Search app (site-search, Digital marketing) with View + LuceneSuite.ManageRules/ManageSynonyms/ManageExperiments; cogwheel hidden via Kentico-internal IApplicationPermissionEvaluator (user option a; IUITreeNodeUIPermissionEvaluator internal in 31.9.1); permissions enforced by [UIEvaluatePermission], ValidatePage = index-exists on every routed node (user change b362fa0) | Phase 3 | 03-05 adds pages under SearchTuningSectionPage and enforces the custom permissions on its commands |
| 2026-10-09: 02-04 rollup job: session applock on unpooled connection; watermark = hidden settings key read uncached in the lock, written in the day's CMSTransactionScope (Kentico bulk ops enlist); RawRetention minimum tightened to 1 day + 3W + flush + 5 min + RollupInterval + 1 h; collation-equal texts merged; volume within budget (no streaming); SessionId/Position getter null bug fixed | Phase 2 | v0.2 reports read SearchDaily incl. (other) (QueryText NULL) |
| 2026-10-09: Sub-threshold prune folds into per-day (other) bucket (QueryText NULL) to preserve totals; xbk DB subject to retention when it next runs this code | Phase 2 | 02-04 |
| 2026-10-09: 02-03 options bound via Configure<IServiceProvider> + optional IConfiguration (not BindConfiguration; no live reload); SearchDaily = installer v3; engine input contract + duration cap + tie-break implemented; 02-02 mint conflicts resolved as settings (vectors prove both flips) | Phase 2 | 02-04 maps Info rows to the documented engine input contract |
| 2026-10-09: 02-02 interaction logger: validation throws synchronously, storage never throws; real Kentico cookie-level provider writes no CMSCookieLevel on a started response (SearchSessionCookie kept) | Phase 2 | Closes 02-01 HasStarted concern |
| 2026-10-09: Enterprise audit on 02-02-PLAN.md. Applied 2 must-have, 3 strongly-recommended. Deferred 3. Verdict: conditionally acceptable → acceptable | Phase 2 | Destructive tests only on a disposable DB copy; cross-page conversion attribution via URL ids |
| 2026-10-09: Enterprise audit on 02-03-PLAN.md. Applied 3 must-have, 2 strongly-recommended. Deferred 2. Verdict: conditionally acceptable → acceptable | Phase 2 | Logical searches capped at AttributionWindow; engine input contract defined; retention can't purge before rollup |
| 2026-10-09: Phase 2 re-split into 4 plans (02-03 engine, 02-04 job & retention); histogram edges + UTC day fixed; settings via appsettings, admin page deferred | Phase 2 | — |
| 2026-10-09: User: every analytics/merge setting is configurable (typo thresholds, min lengths, deep-backspace ratio, 30 s window, 1 h attribution, retention, surfacing threshold, queue sizes); current values are defaults — resolves the ipad/ipod and coffee-grinder questions | Phase 2 | 02-03 exposes rollup settings via SuiteSearchOptions; contract + vectors parameterized |
| 2026-10-09: Widgets/tag helpers/endpoint/JS client → optional package (Phase 6, end of v0.1); core exposes ISearchInteractionLogger | Phase 2 | Resolves the real-traffic question; 02-02 is API + contract only |
| 2026-10-09: 02-01 cookie via Configure<CookieLevelOptions> + CookieLevelConstants (RegisterCookie/CookieLevel obsolete in 31.9.1); Suite core references Kentico.Xperience.WebApp | Phase 2 | Core package depends on the live-site WebApp package |
| 2026-10-09: Enterprise audit on 02-01-PLAN.md. Applied 4 must-have, 5 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 2 | Capture can never break search; installer safe on web-farm startup |
| 2026-10-09: Phase 2 design: module classes + Kentico bulk APIs; hosted single-runner rollup job; session cookie at Visitor/All (option a); merge at rollup per as-you-type-merge-analysis.md with the 5 recommended choices | Phase 2 | Split into 02-01 capture / 02-02 interactions / 02-03 rollups |
| 2026-10-09: Search tuning section placed under IndexListingPage as sibling of IndexEditPage (user option A); entry via TableActions.AddLink — SUPERSEDED by 03-01 (Site Search app) | Phase 1 | URL now /admin/site-search/indexes/{id}/…; Phases 3–5 add pages to this section |
| 2026-10-09: Checkpoint fixes 1–3 applied in-loop (accessible facet checkboxes, narrow-width layout, clean validation text) | Phase 1 | Query tester usable at ~790 px |
| 2026-10-08: xbk NoWarn += XPSEARCH002 (user option a) — pre-existing break from XpSearch's [Experimental] UseMcp() | Phase 1 | xbk opts into an experimental XpSearch API |
| 2026-10-08: Enterprise audit on 01-03-PLAN.md. Applied 2 must-have, 5 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 1 | Permission check mandatory; npm embed for all non-Debug configs with --ignore-scripts |
| 2026-10-08: Enterprise audit on 01-02-PLAN.md. Applied 3 must-have, 4 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 1 | Overflow-safe paging guard; stop-word queries return 0 hits; strategies via GetRequiredService |
| 2026-10-08: Enterprise audit on 01-01-PLAN.md. Applied 4 must-have, 4 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 1 | Baseline + backups for untracked xbk edits; Kentico.Xperience.Core 31.9.1 is the declared floor |

### Deferred Issues
| Issue | Origin | Effort | Revisit |
|-------|--------|--------|---------|
| PDF export of reports | Init | M | When reports ship (v0.2) |
| xbk/Program.cs:65 hard-codes Management API secret | Research | S | Outside this repo — user to move to user secrets |
| xbk/packages.lock.json is rewritten by every SeparatedAdmin=true build (both modes share one lock file; restored to default shape 2026-10-08) | 01-01 UNIFY | S | Only matters if xbk ever adopts locked-mode restore |
| NuGet PackageId availability; CI pipeline; root global.json 8.0.101 | 01-01 audit | S–M | Release phase / outside Suite |
| Strategy/FacetsConfig built per search; captive scoped deps in strategies; full stored-field payloads; no failure logging | 01-02 audit | S–M | Phase 2 (timing data, logging) |
| Stored fields visible to Site Search VIEW users; Node.js required for non-Debug builds; npm vulnerability scanning in CI (5 dev-only highs in KNOWN-LIMITATIONS); no client unit tests | 01-03 audit | S–M | Phase 3 permissions / Release-CI phase |
| 02-01 audit deferrals: language/channel dimensions; clock drift beyond NTP (KNOWN-LIMITATIONS); unit-level DB writer test (covered E2E) | 02-01 audit | S–M | When the search API carries language/channel |
| 02-02 audit deferrals: shared queue capacity (drops logged per kind); custom-endpoint rate limiting (developer-owned) | 02-02 audit | S | Phase 6 Widgets traffic |
| No config change tracking / live reload: merge-setting changes only affect days rolled afterwards | 02-03 UNIFY | S | v0.2 reports |
| Admin settings page | 02-03 audit | M | After 03-04 (Site Search app + custom permissions now exist) |
| 03-01 audit deferrals: existing roles must be granted Site Search → View; old /admin/lucene/indexes/search-tuning/… URLs no longer resolve | 03-01 audit | S | README at packaging |
| npm Table rows not keyboard-reachable (display:contents row buttons); hit-detail focus returns to the table wrapper (KNOWN-LIMITATIONS). Rules listing avoided it (native Kentico listing); query tester still affected (03-07 left as is) | 03-02 checkpoint | S–M | Phase 5 preview (same table) |
| 03-02 limitations: filter-panel counts from the last executed search; server runs aborted searches (audit deferral); Show more capped by FacetValuesPerDimension; @types/node only transitive | 03-02 audit / checkpoint | S | KNOWN-LIMITATIONS |
| Verification gap: stored Valid-from value not checked by SQL on the host (rule deleted before the check; mapping covered by unit test). Roles + listing keyboard closed at the 03-07 checkpoint | 03-07 checkpoint | S | Next admin checkpoint touching rules |
| "Featured content" rule action: content item references (Content hub selector, ContentItemGUIDs) returned in the response for the site to load via IContentRetriever; custom JSON kept as a developer field. Suite must not resolve content itself (no language/channel on requests, latency budget) | 03-06 discussion (user: not now) | M | When wanted |
| 03-07 audit deferrals: picker 20 results without paging; multilingual title by doc id; stored fields visible to View users (01-03) | 03-07 audit | S | KNOWN-LIMITATIONS |
| Leftover XpSearch contact-group macro `Contact.XpSearchSearchedFor("grinders")` fails on every evaluation in the CommunityProjects host log | 03-05 checkpoint | S | Part of the pending XpSearch DB-objects decision |
| 04-01 audit deferrals: accent folding of synonym inputs; orphan synonyms after index rename/deletion; query-parser custom factories reading synonym syntax characters | 04-01 audit | S | KNOWN-LIMITATIONS |
| Leftover XpSearch scheduled tasks (XpSearch.QueryLogRetention, XpSearch.PopularitySignal) fail on every CommunityProjects host start | 04-01 checkpoint | S | Part of the pending XpSearch DB-objects decision |
| 04-02 audit deferrals: same term in several synonyms (no warning); Kind change without confirmation | 04-02 audit | S | KNOWN-LIMITATIONS |
| Rules-overhead perf test (p95 < 10 ms) flaked once when the suite ran alongside the host and dev server; 7.4–8.8 ms alone. 07-02 APPLY (no src change, host + admin dev server running): 9.75 / 10.19 (fail) / 8.92 ms alone; passed in the 08-01 full run | 05-01 APPLY | S | Recurring: decide on a CI-specific budget or a quieter measurement |
| 03-07 custom-data `<pre>` in the normal query tester uses the brand-purple token | 05-01 APPLY | S | KNOWN-LIMITATIONS; next change to the normal tester view |
| Widgets: per-process rate limit, forgeable events, unfingerprinted JS asset; no NuGet metadata on either package | 06-01 audit / APPLY | S–M | KNOWN-LIMITATIONS; 06-02 asset versioning; release phase |
| Widget strings English-only; search not usable inside Kentico preview (signed URLs); host facet values are display names (old code-name filter URLs no longer match) | v0.1 review / 06-04 | S | KNOWN-LIMITATIONS |
| 02-04 audit deferral: query crossing the threshold after part of its history was folded into (other); rollup lock contention proven within one process only | 02-04 audit / UNIFY | S | KNOWN-LIMITATIONS; v0.2 reports / real web farm |

### Deviations
#### Skill Audit (Phase 1, plan 01-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ○ | Not evidenced in APPLY record; edit was one builder-chain call in xbk/Program.cs |

Plans 01-02, 01-03, 02-01, 02-02, 02-03 and 02-04: all required skills invoked.

#### Skill Audit (Phase 3, plan 03-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Both APPLY runs |
| /kentico-web-development:content-retrieval | ○ | Not evidenced; listing reads LuceneIndexItemInfo via Kentico ListingPage ObjectType, no hand-written query |

#### Skill Audit (Phase 3, plan 03-06)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN and APPLY (before markup) |
| Admin UI design validation | ✓ | ~790/1440 px, focus, tokens, states; listing-row keyboard pass not run (empty listing) |

Plans 03-03, 03-04, 03-05, 03-07, 04-01, 04-02, 05-01, 06-01, 06-02, 06-03, 07-01, 07-02, 08-01: all required skills invoked.

#### Skill Audit (Phase 3, plan 03-02)
| Expected | Invoked | Notes |
|----------|---------|-------|
| Admin UI design validation | ✓ | Kit stories/source/exports, tokens, kit states, a11y, browser check at ~800 px; 1440 px not checked (pane width) |
| /dotnet:csharp-refactoring | ○ | C# changed in 58c3126 (QueryTesterPage, SiteSearchApplication, SiteSearchIndexListingPage, AdminRegistrationTests); invocation not evidenced in plan or commit record |

### Blockers/Concerns
| Concern | Impact | Resolution Path |
|---------|--------|-----------------|
| Phase 7: APPROVAL-CHECKS #4 pending — keep or clear seeded host history (2026-08-12..2026-10-10) and accept its effect on real-query surfacing | Phase 8 demos use it | Milestone review |
| Resolved 2026-10-10: admin UI design-validation concern — design review fixes shipped in 03-02 (58c3126, f014963) and design validation is now required in SPECIAL-FLOWS | — | — |

### Git State
Last commit: 8cbe68c docs(paul): unify 08-01 search reports page
Branch: main (not pushed)
Feature branches merged: none

## Session Continuity

Last session: 2026-10-12
Stopped at: Plan 08-02 audited
Next action: /paul:apply .paul/phases/08-reports-admin/08-02-PLAN.md
Resume file: .paul/phases/08-reports-admin/08-02-PLAN.md
Environment: 2026-10-10: test host is CommunityProjects (http://localhost:27340, DB comm_projects at 31.9.1, Suite schema v5 since 04-01; Widgets wired since 06-03, Site search page /site-search). Disposable DB comm_projects_test (restored from the pre-31.9.1 backup, updated to 31.9.1, Suite v5 via the DB tests) for LUCENESUITE_TEST_CONNECTIONSTRING. Admin client in Debug needs `npm start` in src/XperienceCommunity.Lucene.Suite.Admin/Client (Proxy 3011)

---
*STATE.md — Updated after every significant action*
