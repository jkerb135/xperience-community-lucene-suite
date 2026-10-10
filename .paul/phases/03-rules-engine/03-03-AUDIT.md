# Enterprise Plan Audit Report

**Plan:** .paul/phases/03-rules-engine/03-03-PLAN.md
**Audited:** 2026-10-10
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The layering is right: the model, the strict parser, the pure engine and storage are separate, and the engine has no I/O and no clock. Four gaps would have produced wrong or crashing behaviour in 03-04 on the live search path:
- The engine normalized the input query with an unspecified step. Reusing QueryScrubber would break any digit-run pattern.
- Stored rows that fail parsing had no defined outcome.
- Pin shifting had no ceiling, and duplicates within one rule were ambiguous.
- userData risked living as a JsonElement backed by a disposed document.

With these applied, I would approve it for APPLY.

## 2. What Is Solid

- **Pure, deterministic engine with time as an input.** It can be proved with table tests, and it fits the p95 < 10 ms overhead target.
- **Strict parsing that collects every error with its JSON path, and never throws.** This is the right contract for 03-05's inline errors and for safely skipping bad stored rows.
- **Conflict policy matches the recorded Key Decisions:** explicit priority with an ID tie-break, and hide beats pin. Pins respect filters by default, so no content leaks.
- **Token-level matching.** "grinder" does not match "grinders", which avoids the substring false-positive class entirely.
- **Patterns are normalized without PII placeholders**, so "{email}" can't end up in a stored rule.
- **Installer v4 follows the proven applock, idempotency and downgrade-guard pattern** from versions 1–3, and the existing database tests already cover concurrency.
- **The database tests are hard-guarded** to a database whose name contains "test". comm_projects is explicitly protected.
- **CI/CD is disabled on RuleInfo**, so a CI/CD restore can't overwrite marketer data edited in production. Event logging gives a record of who changed what and when.

## 3. Enterprise Gaps Identified

1. **Input query normalization was unspecified.** AC-2 says "raw query", while Task 2 says "the normalized query". The obvious reuse, QueryScrubber.Normalize, replaces runs of 7–10+ digits (SKUs, model numbers) with `{number}`, so a rule on "1234567890" would never fire.
2. **Stored rows that fail parsing had no defined behaviour.** This happens with hand-edited JSON or after a later build tightens a limit. An exception in ToDefinition at search time (03-04) would break search.
3. **RuleMetadata was not validated.** That covers an empty or overlong index name, priority 0 or negative (which contradicts "1 runs first"), and Local DateTimeKind validity bounds, which would compare wrongly against NowUtc.
4. **Pin shifting had no ceiling.** Collisions across rules could push a pin past position 300.
5. **Duplicates within a rule were ambiguous:** the same item pinned twice, hidden twice, both pinned and hidden, or the same boost listed twice.
6. **A JsonElement taken from a disposed JsonDocument throws ObjectDisposedException** when read later, which in 03-04 would be on the search path.
7. **Some limits were missing:** boost count, boost field and value lengths, and facet dimension and value lengths. Unbounded input defeats the 1 ms engine budget and the column-free JSON storage.
8. **A rule with no actions** would appear in FiredRuleIds while doing nothing, which is misleading in the query tester (03-05).
9. **Hides was returned as an unordered set.** Shuffled-input determinism was only provable as set equality, and enumeration order could leak to callers.
10. **The single timing test could be flaky:** there were no warm-up runs and no way to filter it out.
11. **There is no change history for rules** beyond Kentico's event log: no versioning and no rollback.
12. **The stored JSON has no schema version.** Combined with strict rejection of unknown properties, a downgrade after a future field addition makes those rules invalid.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Input query normalization (gap 1) | AC-2, Task 1, Task 2 | The engine normalizes the raw query with the shared non-PII pattern helper, never QueryScrubber.Normalize. Added a test that a digit run matches. |
| 2 | Stored rows that fail parsing (gap 2) | AC-4, Task 3 | ToDefinition returns errors and no definition, never throws. Added a database test with invalid Actions JSON. |
| 3 | Metadata validation (gap 3) | AC-1, Task 1 | Index name required and at most 200 characters; priority at least 1; Unspecified kind treated as UTC and Local rejected. |
| 4 | Pin shift ceiling and duplicates within a rule (gaps 4, 5) | AC-1, AC-3, Task 2 | A pin shifted past 300 is dropped. Duplicate pin, hide, pin-and-hide and boost entries are validation errors. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | userData lifetime (gap 6) | AC-1, Task 1 | userData is cloned or deserialized into a self-owned JsonElement; its size is measured on the compact UTF-8 form. |
| 2 | Missing limits (gap 7) | AC-1, Task 1 RuleLimits | 50 boosts; boost field 100 characters; boost value, facet dimension and facet value 200 characters. |
| 3 | Rule with no actions (gap 8) | AC-1 | One error at path "actions". |
| 4 | Deterministic Hides (gap 9) | AC-3, Task 2 | Hides becomes an IReadOnlyList sorted by Guid; the shuffle test compares sequences. |
| 5 | Timing test flakiness (gap 10) | Task 2 | 20 warm-up runs and `[Trait("Category","Performance")]`. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Rule change history and rollback (gap 11) | LogEvents=true records who changed what and when in the event log. Versioning is an editor feature for 03-05 or later, and no history exists to migrate. |
| 2 | Schema version in the stored JSON (gap 12) | Upgrade 2 already makes an unparseable row fail safe: it is skipped, not crashed on. Add a version field when the model first changes. |
| 3 | Facet and context matching is ordinal (case-sensitive) | 03-05 picks values from the index's own facet values, so the stored case matches the indexed case. Revisit if marketers type values by hand. |
| 4 | Location of the test-database backup and RESTORE permissions | An operational step during APPLY. The DatabaseFact name guard prevents running against the wrong database. |

## 5. Audit & Compliance Readiness

- **Evidence:** every validation and matching clause maps to a named test row (verification checklist item), and installer runs log the class creation and the version.
- **Silent failures:** invalid stored rules now produce errors rather than exceptions. 03-04 must log the skipped rule IDs; that falls in 03-04's scope and is noted for that plan.
- **Reconstruction:** the outcome is a pure function of (rules, input, nowUtc), so any reported result can be replayed exactly. Rule edits appear in the Kentico event log.
- **Ownership:** rules are marketer data (CI/CD off). Editing is gated by LuceneSuite.ManageRules in 03-05.

## 6. Final Release Bar

- **Must be true before this ships:**
  - The upgraded AC-1 to AC-4 clauses are all covered by tests.
  - No exception path from stored data into the engine.
  - Installer v4 is idempotent under concurrency and respects the downgrade guard.
- **Remaining risks:**
  - No history or rollback of rule edits beyond the event log.
  - Case-sensitive facet matching.
  - Machine-dependent timing assertion, now filterable.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 4 must-have + 5 strongly-recommended upgrades. Deferred 4 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
