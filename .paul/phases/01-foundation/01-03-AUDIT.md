# Enterprise Plan Audit Report

**Plan:** .paul/phases/01-foundation/01-03-PLAN.md
**Audited:** 2026-10-08
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The Proxy/Embedded split follows Kentico's documented model ([Certain] docs fetched), the placement under IndexEditPage uses verified public types, and the checkpoint covers the 01-02 shell gaps. But the only security boundary in the plan (AC-5) was optional to verify, the embed step keyed on `Configuration == Release` could ship an empty client from any other non-Debug configuration, and the build would execute third-party npm install scripts on every non-Debug build. Fixed below.

## 2. What Is Solid

- **Proxy only via appsettings.Development.json**: production falls back to Embedded by default ([Certain] docs), so no environment can accidentally proxy.
- **Incremental npm target + loud failure on empty dist**: prevents the silent "empty admin" failure mode.
- **Embedding test** gives a durable, automated check of the packaging pipeline.
- **No custom application or permissions** — minimal surface, consistent with the user's placement decision.
- **xbk is http + Development** ([Certain] launchSettings), so Proxy mode needs no SSL configuration.

## 3. Enterprise Gaps Identified

1. AC-5 (permissions) was verifiable only "if a second user is handy" — the plan's one access-control claim could ship unverified.
2. `Configuration == Release` misses `dotnet pack` with other configs and custom configurations → empty client shipped.
3. `npm ci` runs dependency install scripts inside every non-Debug build (supply-chain exposure on dev machines and CI).
4. Missing Node.js would fail with an opaque Exec error.
5. Stored fields can contain content HTML; rendering method was unspecified (XSS risk if rendered as HTML).
6. Admin behaviour when the proxy dev server is down in Development was unknown.
7. Field discovery could open the live index in a way that takes its lock.
8. Query tester shows all stored fields to anyone with Lucene VIEW.
9. Node becomes a build prerequisite for any non-Debug build of xbk/Suite.
10. No npm vulnerability scanning.
11. No client unit tests.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Unverified access control | AC-5, checkpoint step 10, verification | Step 10 required with a real test role; AC-5 cannot pass without it |
| 2 | Empty client from non-Release configs | AC-1, Task 1 | Condition `'$(Configuration)' != 'Debug'` |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | npm install scripts in the build | Task 1 | `npm ci --ignore-scripts`; any exception recorded |
| 2 | Opaque failure without Node | AC-1, Task 1 | `node --version` pre-check with a clear error |
| 3 | HTML in stored fields | Task 2 | Plain-text rendering only, no dangerouslySetInnerHTML |
| 4 | Proxy-down behaviour unknown | Checkpoint step 7b | Observe and record |
| 5 | Index lock risk during field discovery | Task 3 | DirectoryReader only, never IndexWriter |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | All stored fields visible to Lucene VIEW users | Admin-only audience; folds into the logged permissions concern before Phase 3 |
| 2 | Node required for non-Debug builds | Inherent to the chosen embed approach; documented in the build error; CI setup is release-phase work |
| 3 | npm vulnerability scanning | Belongs with the CI pipeline (already deferred) |
| 4 | Client unit tests | Template is presentational; behaviour covered by the checkpoint |

## 5. Audit & Compliance Readiness

- **Evidence:** embedding test, build logs (npm ran / skipped / failed loudly), checkpoint step results, diffs of untracked xbk files.
- **Silent failures:** empty-client and missing-Node cases now fail loudly.
- **Reconstruction:** Program.cs backup; appsettings.Development.json is new (rollback = delete).
- **Access control:** verified with a real role, not assumed.

## 6. Final Release Bar

- Must be true: AC-1..AC-6 with evidence, checkpoint steps 1–10 incl. 7b approved.
- Remaining risk: permissions inherit Lucene's app (logged concern); row-action-to-grandchild navigation unverified until APPLY.
- I would sign off on the plan as amended.

---

**Summary:** Applied 2 must-have + 5 strongly-recommended upgrades. Deferred 4 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
