# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-09)

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.1 Tune & Capture — Phase 2 Event Capture, 02-02 complete, 02-03 next

## Current Position

Milestone: v0.1 Tune & Capture
Phase: 2 of 5 (Event Capture) — In progress
Plan: 02-03 created + audited, awaiting approval (02-01, 02-02 complete)
Status: PLAN created, ready for APPLY
Last activity: 2026-10-09 — Enterprise audit of 02-03-PLAN.md (02-03-AUDIT.md)

Progress:
- Milestone: [██░░░░░░░░] 17% (1 of 6 phases)
- Phase 1: [██████████] 100% (3/3 plans)
- Phase 2: [█████░░░░░] 50% (2/4 plans)

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

### Deviations
#### Skill Audit (Phase 1, plan 01-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ○ | Not evidenced in APPLY record; edit was one builder-chain call in xbk/Program.cs |

Plans 01-02, 01-03, 02-01 and 02-02: all required skills invoked.

### Blockers/Concerns
| Concern | Impact | Resolution Path |
|---------|--------|-----------------|
| Phase 1: Tuning UI inherits Lucene Search app permissions (Development category). Marketers need Lucene app access; future rule/synonym editing would need UPDATE, which also allows editing/rebuilding/deleting indexes | Core value "marketers without a developer" vs. index-admin rights | Revisit before Phase 3 editing pages: custom permission on suite commands, or role guidance |

### Git State
Last commit: see `git log -1` (docs(paul): unify plan 02-02)
Branch: main (not pushed)
Feature branches merged: none

## Session Continuity

Last session: 2026-10-09
Stopped at: Plan 02-03 created
Next action: /paul:audit .paul/phases/02-event-capture/02-03-PLAN.md, then /paul:apply
Resume file: .paul/phases/02-event-capture/02-03-PLAN.md

---
*STATE.md — Updated after every significant action*
