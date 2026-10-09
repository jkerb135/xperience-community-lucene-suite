# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-09)

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.1 Tune & Capture — Phase 3 Rules Engine, ready to plan

## Current Position

Milestone: v0.1 Tune & Capture
Phase: 3 of 6 (Rules Engine) — Not started
Plan: Not started
Status: Ready to plan
Last activity: 2026-10-09 — Phase 2 complete (02-04 unified: rollup job, store, retention + fold), transitioned to Phase 3

Progress:
- Milestone: [███░░░░░░░] 33% (2 of 6 phases)
- Phase 1: [██████████] 100% (3/3 plans)
- Phase 2: [██████████] 100% (4/4 plans)
- Phase 3: [░░░░░░░░░░] 0%

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
| 2026-10-09: Search tuning section placed under IndexListingPage as sibling of IndexEditPage (user option A); entry via TableActions.AddLink | Phase 1 | URL /admin/lucene/indexes/search-tuning/{id}/…; Phases 3–5 add pages to this section |
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
| Stored fields visible to Lucene VIEW users; Node.js required for non-Debug builds; npm vulnerability scanning in CI (5 dev-only highs in KNOWN-LIMITATIONS); no client unit tests | 01-03 audit | S–M | Phase 3 permissions / Release-CI phase |
| AC-5 (permissions) approved by user without reported observations | 01-03 UNIFY | S | Re-check with a real role when custom permissions land (Phase 3) |
| 02-01 audit deferrals: language/channel dimensions; clock drift beyond NTP (KNOWN-LIMITATIONS); unit-level DB writer test (covered E2E) | 02-01 audit | S–M | When the search API carries language/channel |
| 02-02 audit deferrals: shared queue capacity (drops logged per kind); custom-endpoint rate limiting (developer-owned) | 02-02 audit | S | Phase 6 Widgets traffic |
| No config change tracking / live reload: merge-setting changes only affect days rolled afterwards | 02-03 UNIFY | S | v0.2 reports |
| Admin settings page | 02-03 audit | M | With Phase 3 permissions concern |
| 02-04 audit deferral: query crossing the threshold after part of its history was folded into (other); rollup lock contention proven within one process only | 02-04 audit / UNIFY | S | KNOWN-LIMITATIONS; v0.2 reports / real web farm |

### Deviations
#### Skill Audit (Phase 1, plan 01-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ○ | Not evidenced in APPLY record; edit was one builder-chain call in xbk/Program.cs |

Plans 01-02, 01-03, 02-01, 02-02, 02-03 and 02-04: all required skills invoked.

### Blockers/Concerns
| Concern | Impact | Resolution Path |
|---------|--------|-----------------|
| Phase 1: Tuning UI inherits Lucene Search app permissions (Development category). Marketers need Lucene app access; future rule/synonym editing would need UPDATE, which also allows editing/rebuilding/deleting indexes | Core value "marketers without a developer" vs. index-admin rights | Must be resolved during Phase 3 planning (before editing pages): custom permission for editing pages / suite commands, or role guidance |

### Git State
Last commit: see `git log -1` (docs(paul): unify plan 02-04, close Phase 2; Phase 2 code in d5261ec, 18add8d and earlier)
Branch: main (not pushed)
Feature branches merged: none

## Session Continuity

Last session: 2026-10-09
Stopped at: Phase 2 complete (4/4), ready to plan Phase 3 Rules Engine
Next action: /paul:plan for Phase 3 (resolve the permissions concern first)
Resume file: .paul/ROADMAP.md
Environment: 2026-10-09 (02-04 APPLY): no xbk process running; DB `xperience` already at LuceneSuiteInstalledVersion = 3 (SELECT only). When xbk next runs this code, the installer adds the watermark key and the rollup job applies retention to its raw rows (by design)

---
*STATE.md — Updated after every significant action*
