# Project State

## Project Reference

See: .paul/PROJECT.md (updated 2026-10-08)

**Core value:** Marketers tune, test and measure site search without a developer.
**Current focus:** v0.1 Tune & Capture — Phase 1 Foundation, plan 01-02 next

## Current Position

Milestone: v0.1 Tune & Capture
Phase: 1 of 5 (Foundation) — In Progress
Plan: 01-01 complete (UNIFY done); 1 of 3 plans in phase
Status: Loop closed, ready for next PLAN
Last activity: 2026-10-08 — UNIFY of 01-01 (01-01-SUMMARY.md)

Progress:
- Milestone: [░░░░░░░░░░] 7%
- Phase 1: [███░░░░░░░] 33%

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
| 2026-10-08: xbk NoWarn += XPSEARCH002 (user option a) — pre-existing break from XpSearch's [Experimental] UseMcp() | Phase 1 | xbk opts into an experimental XpSearch API |
| 2026-10-08: Enterprise audit on 01-01-PLAN.md. Applied 4 must-have, 4 strongly-recommended. Deferred 4. Verdict: conditionally acceptable → acceptable | Phase 1 | Baseline + backups for untracked xbk edits; Kentico.Xperience.Core 31.9.1 is the declared floor |

### Deferred Issues
| Issue | Origin | Effort | Revisit |
|-------|--------|--------|---------|
| PDF export of reports | Init | M | When reports ship (v0.2) |
| xbk/Program.cs:65 hard-codes Management API secret | Research | S | Outside this repo — user to move to user secrets |
| Solution-root Directory.build.targets still imports into Suite projects (.targets resolve separately from .props); Suite-only clone/CI won't have it | 01-01 UNIFY | S | Next plan touching build files — add empty Suite Directory.Build.targets |
| LICENSE holder "Citro Digital" (copied from GEO) not explicitly confirmed by user | 01-01 | S | Before first publish; ask user |
| xbk/packages.lock.json left in SeparatedAdmin=true shape (Admin CentralTransitive, no Suite.Admin entry); both modes share one lock file | 01-01 UNIFY | S | Run plain `dotnet restore xbk/xbk.csproj`; decide if xbk ever uses locked mode |
| NuGet PackageId availability; CI pipeline; root global.json 8.0.101 | 01-01 audit | S–M | Release phase / outside Suite |

### Deviations
#### Skill Audit (Phase 1, plan 01-01)
| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ○ | Not evidenced in APPLY record; edit was one builder-chain call in xbk/Program.cs |

### Blockers/Concerns
None.

## Session Continuity

Last session: 2026-10-08
Stopped at: Plan 01-01 unified (loop closed)
Next action: /paul:plan for 01-02 (Search API + test project)
Resume file: .paul/phases/01-foundation/01-01-SUMMARY.md

---
*STATE.md — Updated after every significant action*
