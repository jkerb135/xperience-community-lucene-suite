# Enterprise Plan Audit Report

**Plan:** .paul/phases/03-rules-engine/03-01-PLAN.md
**Audited:** 2026-10-09
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The move to a Suite-owned application is the only Kentico-supported way to get custom permissions, and the user's additions (permission-gated cogwheel, explicit ValidatePage) are sound defence in depth. One gap would undermine the whole access model: the explicit validation was specified only on the section page, while users reach the query tester by its own child URL — if Kentico doesn't run the parent's ValidatePage for child routes, the explicit check is bypassed. Fixed below.

## 2. What Is Solid

- **Own application with UIPermission attributes**: the documented mechanism; Roles UI shows the permissions natively.
- **Cross-app permission check** for the shortcut via IUITreeNodeUIPermissionEvaluator (verified signature).
- **Reflection tests** pin the page chain and forbid Lucene parents — a regression to the old placement fails CI.
- **Real-role checkpoint** in a private window closes the unverified AC-5 from 01-03.

## 3. Enterprise Gaps Identified

1. Explicit validation only on the section; child route (query tester) and its command might bypass it.
2. ActionStateEvaluator runs per row; an uncached cross-app permission check per row is N lookups per listing.
3. Existing roles with Lucene access lose the query tester until granted Site Search View (pre-release, no installed base).
4. Old bookmarked URLs under /admin/lucene/indexes/search-tuning/… will 404.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Child-route bypass | AC-3b, checkpoint step 6 | Verify parent ValidatePage coverage for child routes; otherwise query tester overrides ValidatePage; user pastes the child URL at the checkpoint |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Per-row permission lookups | AC-3b | Evaluate once per listing request |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Roles must be re-granted | Unreleased package; document in the README when packaging |
| 2 | Old URLs 404 | Unreleased; no external links exist |

## 5. Audit & Compliance Readiness

- **Evidence:** reflection tests, unit tests for both access decisions, mutation on the visibility decision, real-role checkpoint including direct child URLs.
- **Access control:** layered — app permission (Kentico), explicit ValidatePage, command permission.

## 6. Final Release Bar

- Must be true: AC-1..AC-5 incl. AC-3b with the child-route proof and the user's step 5–6 observations.
- I would sign off on the plan as amended.

---

**Summary:** Applied 1 must-have + 1 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
