# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-09)

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.1 Tune & Capture — Phase 2 Event Capture, ready to plan

## Current Position

Milestone: v0.1 Tune & Capture
Phase: 2 of 5 (Event Capture) — Not started
Plan: Not started
Status: Ready to plan
Last activity: 2026-10-09 — Plan 01-03 unified; Phase 1 complete, transitioned to Phase 2

Progress:
- Milestone: [██░░░░░░░░] 20% (1 of 5 phases)
- Phase 1: [██████████] 100% (3/3 plans)
- Phase 2: [░░░░░░░░░░] 0%

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

### Deviations
#### Skill Audit (Phase 1, plan 01-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ○ | Not evidenced in APPLY record; edit was one builder-chain call in xbk/Program.cs |

Plans 01-02 and 01-03: all required skills invoked.

### Blockers/Concerns
| Concern | Impact | Resolution Path |
|---------|--------|-----------------|
| Phase 1: Tuning UI inherits Lucene Search app permissions (Development category). Marketers need Lucene app access; future rule/synonym editing would need UPDATE, which also allows editing/rebuilding/deleting indexes | Core value "marketers without a developer" vs. index-admin rights | Revisit before Phase 3 editing pages: custom permission on suite commands, or role guidance |

### Git State
Last commit: see `git log -1` (docs(paul): unify plan 01-03, close Phase 1)
Branch: main (not pushed)
Feature branches merged: none

## Session Continuity

Last session: 2026-10-09
Stopped at: Phase 1 complete, ready to plan Phase 2
Next action: /paul:plan for Phase 2 (Event Capture)
Resume file: .paul/ROADMAP.md

---
*STATE.md — Updated after every significant action*
