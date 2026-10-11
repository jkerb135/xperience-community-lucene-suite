# Enterprise Plan Audit Report

**Plan:** .paul/phases/04-synonyms/04-02-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan reuses the 03-06/03-07 admin patterns, which are proven on this host:
- native pages
- Submit, Change and Delete gated on the custom permission
- ownership and concurrency checks
- server-only validation rules
- links built with ancestor parameters

The editor stays native except for the chip component the user chose.

One defect would have shipped a silent data loss. A term typed into the chip editor's Add input but never "added" is dropped when the marketer clicks Save. That is the most likely real-world mistake with tag inputs, so it is release-blocking.

With that fixed, plus the non-ASCII storage issue, I would approve it.

## 2. What Is Solid

- **Native ListingPage and ModelEditPage**, with Kind as a native RadioGroup and Input shown through VisibleIfEqualTo. Kentico's own permission, read-only and visibility machinery does the work, so there is no custom page markup to keep accessible.
- **Writes gated on Manage synonyms server-side.** Submit, Change and Delete override Kentico's system-permission defaults, the same proven fix as 03-06, and a View-only user gets a read-only form.
- **The server re-validates every save with SynonymSerializer.** The client checks are UX only, so a client/server mismatch can never store invalid data.
- **Applied-synonym links use one WhereIn query scoped to the index.** No N+1 queries and no cross-index leaks.
- **`ApplySynonyms` mirrors `ApplyRules`**, defaults to true, and leaves site behaviour unchanged.
- **The Kentico event log records synonym changes** (LogEvents on SynonymInfo since 04-01).

## 3. Enterprise Gaps Identified

1. **Silent loss of a typed but un-added term on Save.**
2. **Non-ASCII terms are stored escaped** (`"café"`) by the default JsonSerializer encoder. The listing's text search runs on the stored JSON, so "café" can't be found.
3. **Client duplicate detection could disagree with the server's MatchKey.** JS and .NET differ on character classes and lowercasing. That means confusing errors, but no data risk, because the server re-validates.
4. **The chip list has no list semantics.** Screen-reader users get no count or grouping, and the Add input had no length limit.
5. **No ownership test for synonyms.** The rules have one (DB test), and synonym deletion depends on the same check.
6. **The same term may appear in several synonyms.** They are merged at search time, without a warning.
7. **Changing Kind on a saved synonym silently changes its semantics.**

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Typed term lost on Save (gap 1) | AC-3, Task 2 state helpers | Leaving the Add input commits a valid term. An invalid one stays in the input with its error. A `commitPending` helper handles Enter, Add and blur, with node tests. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Non-ASCII stored escaped (gap 2) | Task 1 tests, boundaries, files | `SerializeTerms` uses UnsafeRelaxedJsonEscaping (storage only). A "café" round-trip test is added. The boundary explicitly allows this encoding-only change. |
| 2 | Client/server MatchKey parity (gap 3) | Task 1 tests, Task 2 state | The same 6 vectors are asserted in C# and node; `matchKey` uses `\p{L}\p{Nd}` and `toLowerCase()`. |
| 3 | Chip list semantics (gap 4) | AC-3 | `role="list"` with an "Terms" label and listitems; the Add input is labelled with maxLength 100. |
| 4 | Ownership test (gap 5) | Task 1 tests | `SynonymOwnership.Find` DB test, mirroring rules. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Same term in several synonyms (gap 6) | Search merges them correctly (deduplicated variants). A warning is UX polish. Recorded in KNOWN-LIMITATIONS. |
| 2 | Kind change without confirmation (gap 7) | The editor shows both modes clearly and the save is logged in the event log. Recorded in KNOWN-LIMITATIONS. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - .NET tests for mapping, validation-rule routing, permissions, ownership, ApplySynonyms, the applied-synonym mapping and encoding.
  - Node tests for the chip state (including commit-on-blur and the MatchKey vectors) and the query tester rows.
  - A checkpoint covering roles, keyboard use and both widths.
- **Silent failures:** typed terms are no longer silently dropped. SynonymsFailed is shown, and invalid stored rows show "Invalid — open to fix".
- **Reconstruction:** the Kentico event log records synonym create, update and delete. The query tester shows exactly which substitutions shaped a result.
- **Ownership:** the Manage synonyms permission controls every write, and ownership checks keep each synonym inside its index.

## 6. Final Release Bar

- **Must be true before shipping:**
  - No typed-term loss.
  - Writes are gated server-side.
  - Ownership is enforced.
  - Non-ASCII terms are searchable in the listing.
  - The checkpoint passes, including View-only.
- **Remaining risks:**
  - Duplicate terms across synonyms.
  - Unconfirmed Kind changes.
  - The 04-01 limits: weight is a preference, and matching is literal.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 4 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
