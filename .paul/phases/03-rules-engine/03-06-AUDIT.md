# Enterprise Plan Audit Report

**Plan:** .paul/phases/03-rules-engine/03-06-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. Building on Kentico's native listing and edit pages is the right call: they carry confirmation dialogs, discard handling, keyboard-reachable rows and event logging. Using the server `RuleSerializer` as the single validator keeps the client from drifting.

Three authorization and data-integrity gaps would fail a real review:
1. **Native command permissions are unspecified.** The Site Search app declares only View plus custom permissions. If Kentico's `EditPageBase.Submit` or listing delete requires the system Create/Update/Delete permission, no role can ever be granted it, so nobody can save. If neither checks anything, saves are unprotected.
2. **Rule ownership is unchecked.** A crafted command could delete or overwrite another index's rule by id.
3. **Lost updates of fields this form can't edit.** Pins and hides taken from the form's load state can silently undo a concurrent change (and 03-07 will edit them).

## 2. What Is Solid

- **Native ListingPage and ModelEditPage** for the production versions of the kit's listing and edit templates, which npm 31.9.1 doesn't export. That gives confirmation, discard, empty and paging states for free.
- **Custom React is limited to conditions and boosts,** built from npm-exported kit components, with the design-screen run recorded.
- **One validator:** `RuleSerializer` server-side, via a pure form-component command, with errors placed by JSON path.
- **Read-only treatment for View-only users is designed in**, not bolted on.
- **The rules cache invalidates through RuleInfo**, so edits apply without a restart (proven in 03-04).
- **LogEvents on RuleInfo** records who created, changed or deleted each rule.

## 3. Enterprise Gaps Identified

1. **Native submit/delete permission requirement unknown** relative to the app's declared permissions.
2. **No server-side check that a RuleID belongs to the section's index** (delete, edit load, submit).
3. **Pins and hides preserved from load state**, not re-read at submit.
4. **No optimistic concurrency:** two marketers editing one rule means the last write silently wins.
5. **Date and time-zone handling unverified.** The native date input may convert between browser and UTC.
6. **No read-only fallback** if `ModelEditPage` lacks a read-only mode in 31.9.1.
7. **No rule history or rollback** beyond the event log.
8. **The Validate command is callable at typing speed.**

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Native command permissions (gap 1) | AC-1, Task 1, tests, checkpoint 6b | Read the 31.9.1 docs first; gate submit and delete on Manage rules explicitly; prove with two roles (View + Manage rules; View only). |
| 2 | Rule ownership (gap 2) | AC-1, Task 1, tests | Delete, edit load and submit require IndexName = section index; a mismatch changes nothing. |
| 3 | Lost update of pins/hides (gap 3) | AC-2, Task 1 | ProcessFormData re-reads the stored row and takes pins, hides and pinsRespectFilters from it. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Optimistic concurrency (gap 4) | AC-2, Task 1, tests | A hidden LastModified; a stale save is rejected with a reload message and nothing is written. |
| 2 | UTC round trip (gap 5) | AC-2, tests, checkpoint 6c | A mapping test plus a host check of the database value. |
| 3 | Read-only fallback (gap 6) | Task 1, checkpoint 6 | If there's no read-only mode, View-only users can't open the editor; the custom components honour disabled; the choice is recorded. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Rule history or rollback (gap 7) | Event log (LogEvents) gives who and when; versioning is an editor feature beyond v0.1. Carried from the 03-03 audit. |
| 2 | Validate command frequency (gap 8) | Debounced at 400 ms client-side, pure CPU work, View-gated, changes nothing. |
| 3 | Boost field names not checked against the index schema | Existing scope limit and KNOWN-LIMITATIONS entry (03-04). |

## 5. Audit & Compliance Readiness

- **Evidence:** every AC clause maps to unit tests, registration tests or numbered checkpoint steps, including two-role permission proof.
- **Authorization:** server-side gates on every mutating command, with ownership checks against tampering.
- **Integrity:** fresh re-read and optimistic concurrency prevent silent overwrites.
- **Traceability:** the Kentico event log records rule create, update and delete with the user.

## 6. Final Release Bar

- **Must be true before this ships:**
  - Only Manage rules can create, edit or delete, proven with real roles.
  - A rule from another index can't be touched.
  - Pins and hides are never lost on edit.
  - Validation errors land on the right controls.
  - Design validation passes at ~790 and 1440 px, including a keyboard pass.
- **Remaining risks:** no rule history beyond the event log; free-text boost fields.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 3 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
