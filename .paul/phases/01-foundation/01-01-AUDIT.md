# Enterprise Plan Audit Report

**Plan:** .paul/phases/01-foundation/01-01-PLAN.md
**Audited:** 2026-10-08
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable as written. The plan's core compatibility logic (isolated build config; Lucene floor 15.0.5 to coexist with XpSearch's exact `[15.0.5]` pin) is correct and verified. But three of its acceptance criteria were not provable as written ("no new warnings" with no baseline; AC-3 depending on a build that may already fail; the tested Kentico version differing from the shipped floor), it edits untracked files with no rollback, and it contained a factual error (LICENSE holder). With the upgrades below applied, I would approve it.

## 2. What Is Solid

- **Build isolation via nearest-file resolution** (own global.json, Directory.Build.props, Directory.Packages.props): it is the mechanism MSBuild/NuGet actually use, so isolation needs no imports or overrides.
- **Unbracketed 15.0.5 floor**: [Certain] XpSearch pins `[15.0.5]` exactly; a 15.0.6 floor would make xbk unrestorable. The plan avoids that conflict instead of discovering it.
- **Conditional Suite.Admin reference** mirrors xbk's existing `kentico.xperience.admin` condition, so live-site-only deployments stay admin-free.
- **Human checkpoint for the analyzer change**: LUCENE_30 → LUCENE_48 changes tokenization; a rebuild plus real search is the only meaningful proof.
- **Boundaries** protect XpSearch, root config and sibling packages.

## 3. Enterprise Gaps Identified

1. "No new warnings" (AC-2) had no baseline → unverifiable.
2. AC-3 assumed `SeparatedAdmin=true` builds today. [Certain] xbk references `XperienceCommunity.Lucene.Search.Admin` unconditionally, so that build's health is unknown and not this plan's to fix.
3. NuGet lowest-applicable resolution: [Likely] with only Lucene.Core referenced, the Suite would restore Kentico.Xperience.Core 31.0.0 standalone and 31.9.1 inside xbk — the package would be tested against a different platform version than consumers get.
4. xbk/ and XperienceCommunity.sln are not in any git repo → edits had no rollback trail.
5. LICENSE holder: [Certain] plan said "Josh Kerbaugh"; GEO's LICENSE says "Citro Digital".
6. `TreatWarningsAsErrors` + SDK NuGetAudit: [Likely] a transitive advisory (NU1901–NU1904) would break the build, inviting a later blanket `NoWarn` that hides vulnerabilities.
7. Unneeded dependencies: Kentico.Xperience.Lucene.Admin (Suite uses its own admin app) and Kentico.Xperience.WebApp (no consumer yet) widen the dependency surface of a public package.
8. Lock files were produced but never verified reproducible (`--locked-mode`).
9. Checkpoint had no failure/rollback instruction for a human operator.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | No baseline for warnings / SeparatedAdmin build | Tasks, AC-2, AC-3 | New Task 0 captures exit codes + warning counts; AC-2 compares to baseline; AC-3 fallback via `-getItem:ProjectReference` if baseline already fails |
| 2 | Tested ≠ shipped Kentico version | Task 1 action/verify, AC-1 | Suite references Kentico.Xperience.Core 31.9.1 directly; verify it resolves to 31.9.1 standalone |
| 3 | Untracked host edits irreversible | Task 0, new AC-5, frontmatter | Byte-identical backups in project-sidecar/backups/01-01/; SUMMARY carries diffs |
| 4 | Wrong LICENSE holder | Task 1 action, verification | Copy GEO's holder line; flag for user confirmation |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | NuGetAudit vs TreatWarningsAsErrors | Task 1, AC-1, boundaries | `WarningsNotAsErrors` NU1901–NU1904; listed in SUMMARY; NoWarn forbidden |
| 2 | Unneeded dependencies | Task 1, boundaries | Dropped Lucene.Admin and WebApp |
| 3 | Lock-file reproducibility | AC-1, Task 1 verify, verification | `dotnet restore --locked-mode` after commit |
| 4 | Checkpoint lacked rollback | Checkpoint task | Restore Program.cs from backup, rebuild index, report |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | NuGet PackageId / prefix availability | Nothing is packed or published until a release phase |
| 2 | CI pipeline (GEO has azure-pipelines.yml) | Release work; local locked-mode restore covers reproducibility now |
| 3 | Hard-coded Management API secret in xbk/Program.cs | Already a deferred item in STATE.md; outside the Suite |
| 4 | Root global.json pins 8.0.101 | Outside the Suite repo; the Suite's own global.json makes it irrelevant |

## 5. Audit & Compliance Readiness

- **Evidence:** baseline.txt + SUMMARY diffs + lock files give a reconstructable before/after record, including for untracked files.
- **Silent failures:** vulnerability advisories can no longer be silently suppressed; a stale lock file fails locked-mode restore.
- **Reconstruction:** backups make every host change reversible.
- **Ownership:** LICENSE holder explicitly confirmed by the user rather than inferred.

## 6. Final Release Bar

- Must be true: all five ACs pass, Task 0 evidence exists, user confirms the LICENSE holder, checkpoint approved.
- Remaining risk: xbk's SeparatedAdmin build health depends on XpSearch (out of scope); index rebuild under LUCENE_48 changes search behavior for XpSearch too until it is removed.
- I would sign off on the plan as amended.

---

**Summary:** Applied 4 must-have + 4 strongly-recommended upgrades. Deferred 4 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
