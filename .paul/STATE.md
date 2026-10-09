# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-08)

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.1 Tune & Capture — Phase 1 Foundation, plan 01-03 next

## Current Position

Milestone: v0.1 Tune & Capture
Phase: 1 of 5 (Foundation) — In Progress
Plan: 01-03 created + audited, awaiting approval (01-01, 01-02 complete)
Status: PLAN created, ready for APPLY
Last activity: 2026-10-08 — Enterprise audit of 01-03-PLAN.md (01-03-AUDIT.md)

Progress:
- Milestone: [█░░░░░░░░░] 13%
- Phase 1: [███████░░░] 67%

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
| SuiteSearchService/AddLuceneSuite shell untested (index lookup, strategy cast, facet path, analyzer, Elapsed, DI order) | 01-02 UNIFY | S | 01-03 checkpoint |
| Strategy/FacetsConfig built per search; captive scoped deps in strategies; full stored-field payloads; no failure logging | 01-02 audit | S–M | Phase 2 (timing data, logging) |

### Deviations
#### Skill Audit (Phase 1, plan 01-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ○ | Not evidenced in APPLY record; edit was one builder-chain call in xbk/Program.cs |

### Blockers/Concerns
| Concern | Impact | Resolution Path |
|---------|--------|-----------------|
| Tuning UI inherits Lucene Search app permissions (Development category). Marketers need Lucene app access; future rule/synonym editing would need UPDATE, which also allows editing/rebuilding/deleting indexes | Core value "marketers without a developer" vs. index-admin rights | Revisit before Phase 3 editing pages: custom permission on suite commands, or role guidance |

## Session Continuity

Last session: 2026-10-08
Stopped at: Plan 01-03 created
Next action: /paul:audit .paul/phases/01-foundation/01-03-PLAN.md, then /paul:apply
Resume file: .paul/phases/01-foundation/01-03-PLAN.md

---
*STATE.md — Updated after every significant action*
