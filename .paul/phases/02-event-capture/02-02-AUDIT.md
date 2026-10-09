# Enterprise Plan Audit Report

**Plan:** .paul/phases/02-event-capture/02-02-PLAN.md
**Audited:** 2026-10-09
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The API shape (ICustomActivityLogger-style, sync validation, async storage) and the reuse of 02-01's pipeline are right. But Task 1 instructed dropping Suite objects in the user's live xbk database to prove the lock — irreversible data loss on the user's environment — and the client contract had no answer for the most common conversion case (conversion on a later page), which would leave conversion metrics structurally empty for identity-free visitors. Fixed below.

## 2. What Is Solid

- **Validation throws, storage never throws**: matches ICustomActivityLogger expectations and separates caller errors from infrastructure faults.
- **Attribution checks deferred to rollup**: keeps the hot path DB-free and web-farm safe.
- **Shared exclusion filter** extracted behaviour-neutrally with 88 existing tests as the guard.
- **Executable test vectors** make the merge contract testable across C# and the future JS client.
- **Owed lock-removal mutation** scheduled with concrete evidence requirements.

## 3. Enterprise Gaps Identified

1. Destructive drops/mutations against the user's xbk database (5+ real rows, plus all Kentico data in the same DB).
2. Cross-page conversions unattributable without device storage; contract silent.
3. Retried beacons duplicate events when EventId is server-minted per call.
4. Rolling deploy/rollback: older code meeting a newer installed version had undefined behaviour.
5. Position limit duplicated the 1000 constant.
6. Shared queue: a search flood could crowd out interaction events (single capacity).
7. Reading CMSCookieLevel directly would bypass Kentico's channel-default logic.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Destructive steps on the user's DB | AC-5, Task 1 | Backup → restore as `xperience_suitetest`; all drops/mutations there; copy dropped afterwards; never DROP/DELETE on `xperience` |
| 2 | Cross-page conversions unattributable | AC-6, Task 3 | Contract specifies carrying searchId+sequence on result links (random ids only) or existing server state |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Retry duplicates | AC-6, Task 3 | Contract: callers send a stable EventId; rollup dedupes |
| 2 | Downgrade/rollback | AC-5, Task 1 | Installed version > code version → warning, no change; tested |
| 3 | Duplicated constant | AC-2 | Reuse SearchExecutor.MaxResultWindow |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Shared queue capacity starvation | Drop counts are logged per kind; separate capacities only if Phase 6 traffic shows starvation |
| 2 | Direct CMSCookieLevel read | Task 2 decides from real-provider evidence; any shortcut must be recorded — revisit if it bypasses channel defaults |
| 3 | Rate limiting for custom endpoints | Developer-owned by design (documented); the Widgets endpoint (Phase 6) adds it |

## 5. Audit & Compliance Readiness

- **Evidence:** DB rows for click/conversion, mutation runs on a disposable copy, upgrade-path test, real-provider observation for the cookie concern.
- **Data safety:** no destructive operation can touch the user's database.
- **Privacy:** cross-page attribution uses random ids in URLs only; no new device storage.

## 6. Final Release Bar

- Must be true: AC-1..AC-7 with evidence; disposable-DB procedure followed and the copy removed.
- Remaining risk: conversion attribution depends on site developers following the contract.
- I would sign off on the plan as amended.

---

**Summary:** Applied 2 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
