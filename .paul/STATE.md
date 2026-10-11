# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-09)

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.1 Tune & Capture — Phase 3 Rules Engine, 03-05 complete; ready to plan 03-06 Rules admin UI

## Current Position

Milestone: v0.1 Tune & Capture
Phase: 3 of 6 (Rules Engine) — In progress
Plan: 03-05 complete (unified); next 03-06 Rules admin UI
Status: Loop closed, ready for next PLAN
Last activity: 2026-10-11 — Unified 03-05 (all AC pass, checkpoint approved; 394 tests; p95 ≈ 8 ms with 300 pins)

Progress:
- Milestone: [███░░░░░░░] 33% (2 of 6 phases)
- Phase 1: [██████████] 100% (3/3 plans)
- Phase 2: [██████████] 100% (4/4 plans)
- Phase 3: [████████░░] 83% (5/6 plans)

## Loop Position

Current loop state:
```
PLAN ──▶ APPLY ──▶ UNIFY
  ✓        ✓        ✓     [Loop complete - ready for next PLAN]
```

## Accumulated Context

### Decisions
See PROJECT.md Key Decisions.

| Decision | Phase | Impact |
|----------|-------|--------|
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
| npm Table rows not keyboard-reachable (display:contents row buttons); hit-detail focus returns to the table wrapper (KNOWN-LIMITATIONS) | 03-02 checkpoint | S–M | 03-05 rules listing (per-row action or focusable row) |
| 03-02 verification gaps: 1440 px visual check and rapid-click re-test on the Filters panel / Apply / bar controls not run (pane ~800 px) | 03-02 UNIFY | S | Next admin UI checkpoint (03-05) |
| 03-02 limitations: filter-panel counts from the last executed search; server runs aborted searches (audit deferral); Show more capped by FacetValuesPerDimension; @types/node only transitive | 03-02 audit / checkpoint | S | KNOWN-LIMITATIONS |
| Leftover XpSearch contact-group macro `Contact.XpSearchSearchedFor("grinders")` fails on every evaluation in the CommunityProjects host log | 03-05 checkpoint | S | Part of the pending XpSearch DB-objects decision |
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

#### Skill Audit (Phase 3, plan 03-02)
| Expected | Invoked | Notes |
|----------|---------|-------|
| Admin UI design validation | ✓ | Kit stories/source/exports, tokens, kit states, a11y, browser check at ~800 px; 1440 px not checked (pane width) |
| /dotnet:csharp-refactoring | ○ | C# changed in 58c3126 (QueryTesterPage, SiteSearchApplication, SiteSearchIndexListingPage, AdminRegistrationTests); invocation not evidenced in plan or commit record |

### Blockers/Concerns
| Concern | Impact | Resolution Path |
|---------|--------|-----------------|
| None open. Resolved 2026-10-10: admin UI design-validation concern — design review fixes shipped in 03-02 (58c3126, f014963) and design validation is now required in SPECIAL-FLOWS | — | — |

### Git State
Last commit: see `git log -1` (docs(paul): unify plan 03-05; 03-05 code in the preceding feat commit)
Branch: main (not pushed)
Feature branches merged: none

## Session Continuity

Last session: 2026-10-11
Stopped at: Plan 03-05 unified (Phase 3: 5/6)
Next action: /paul:plan for 03-06 Rules admin UI (design via /xbyk-admin-ui:design-screen)
Resume file: .paul/phases/03-rules-engine/03-05-SUMMARY.md
Environment: 2026-10-10: test host is CommunityProjects (http://localhost:27340, DB comm_projects at 31.9.1, Suite schema v4). Disposable DB comm_projects_test (restored from the pre-31.9.1 backup, updated to 31.9.1, Suite v4) for LUCENESUITE_TEST_CONNECTIONSTRING. Admin client in Debug needs `npm start` in src/XperienceCommunity.Lucene.Suite.Admin/Client (Proxy 3011)

---
*STATE.md — Updated after every significant action*
