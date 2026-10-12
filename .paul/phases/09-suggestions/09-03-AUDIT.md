# Enterprise Plan Audit Report

**Plan:** .paul/phases/09-suggestions/09-03-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The design is minimal and its authorization is sound:
- native pages;
- one shared validator;
- the index resolved on the server;
- Manage synonyms checked on every write.

Three gaps would have shipped:
- **Unvalidated create page.** The create page lacked its own index-exists validation, which the PROJECT constraint requires on every routed node.
- **Unhandled command failures.** The one-click command had no specified failure path: a missing index threw, and a rejected command or double click was not covered.
- **No proven audit trail.** Nothing showed that "who hid this suggestion" can be reconstructed. That matters more now, because a one-click action with no confirmation changes public site behaviour.

With these fixed I would approve it.

## 2. What Is Solid

- **The index comes from the page parameter, never the client.** A Manage synonyms user cannot write into another index.
- **Server-side re-validation of the client text.** The report command runs the same validator as the create page.
- **Native Delete with an ownership check,** following the SynonymListingPage precedent (03-06/04-02 patterns).
- **Submit and Change explicitly gated on Manage synonyms.** This avoids the 31.9.1 system-permission trap.
- **Idempotent block command.** A repeat click reports "already blocked" instead of creating a duplicate.
- **Immediate effect.** It relies on the existing 09-01 cache dependency, so there is no new invalidation path.
- **No custom React form component or handoff,** so there is less surface.

## 3. Enterprise Gaps Identified

1. The create page had no index-exists ValidatePage. A stale create URL for a deleted index could store orphan rows.
2. The command's failure behaviour was unspecified:
   - a missing index (the Report/Queries pattern throws InvalidOperationException);
   - a 403 or network failure;
   - a double click while the request is in flight.
3. A one-click public-facing change had no undo cue. The marketer must know where to reverse it.
4. Audit trail: LogEvents = true is declared, but it was never verified that create and delete are logged with the user through both paths.
5. MatchKey length: lower-casing can lengthen some Unicode text (e.g. "İ"). Text ≤ 100 did not guarantee MatchKey ≤ 100, which could truncate the column or fail the save.
6. The Match column formatter did not handle unknown stored values. The engine treats them as exact, so the listing must too, or the UI and behaviour disagree.
7. DB tests had no cleanup rule, so they could leave entries in comm_projects_test and affect later suggestion tests.
8. Two concurrent creates of the same entry can both pass the duplicate check.
9. Report rows give no "already blocked" indicator.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Create page lacks index-exists validation (gap 1) | AC-2, Task 1 action + tests | Own ValidatePage on the create page; registration test asserts it |
| 2 | Command failure path unspecified (gap 2) | AC-3, Task 2 action + tests, verification | Missing index → error message, not an exception; rejected command shown in the NotificationBar with the last report kept; button disabled while in flight; DB test and host failure check |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | No undo cue for the one-click block (gap 3) | AC-3, Task 2 | Success message names where to undo |
| 2 | Audit trail not proven (gap 4) | New AC-5, Task 3, verification | Event log checked on the host for each create and delete; deviation + KNOWN-LIMITATIONS entry if missing |
| 3 | MatchKey can exceed 100 (gap 5) | AC-2, Task 1 validator + tests | Validate MatchKey length too; Unicode test case |
| 4 | Formatter vs engine mismatch; DB test pollution (gaps 6, 7) | Task 1, Task 2 tests | Non-"word" shown as "Exact query"; DB tests delete their entries |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Concurrent duplicate creates (gap 8) | A duplicate entry is harmless to matching (09-01 audit) and visible in the listing; a unique index would need installer v7 for a near-zero-probability race |
| 2 | "Already blocked" indicator on report rows (gap 9) | Scope limit; the repeat click reports it. Revisit if marketers ask |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - DB tests cover the store, idempotency, cross-index isolation and refusal paths;
  - host E2E plus curl proves the public effect;
  - APPROVAL-CHECKS #12 records the human review.
- **Silent failures:** none remain after must-have 2. Every command outcome is either a toast or an error message.
- **Reconstruction:** depends on the Kentico event log (AC-5). This is verified now rather than assumed. If it fails, it is recorded as a limitation rather than left silent.
- **Ownership:** writes are only possible with Site Search → Manage synonyms, which is an explicit, role-assigned permission.

## 6. Final Release Bar

- **Must be true before shipping:**
  - AC-1 to AC-5 pass;
  - the event log shows blocklist changes with the user;
  - the failure paths store nothing;
  - host counts are unchanged.
- **Remaining risks:**
  - a misclick hides a suggestion until someone undoes it, which is reversible and logged;
  - the duplicate race (harmless).
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 2 must-have + 4 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
