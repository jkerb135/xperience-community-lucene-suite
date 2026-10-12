# Enterprise Plan Audit Report

**Plan:** .paul/phases/13-enterprise-screens/13-02-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The plan keeps the risky parts native:
- Kentico's ModelEditPage, Save and unsaved-changes prompt stay;
- stored JSON and the mapping are untouched;
- the index and user come from the server;
- the contact-group logic is extracted, not duplicated.

Three gaps would have shipped:
- **Checkpoint conflict.** The plan ended in a **blocking human-verify checkpoint**, which contradicts the standing project decision (2026-10-11) that verification is recorded in APPROVAL-CHECKS and reviewed at milestone end.
- **Polluted facet counts.** FacetValues switched off rules but not **synonyms or contexts**, so the counts offered as "the index's values" could include synonym-expanded matches.
- **Unprotected JSON.** "Byte-identical JSON" had no automated guard against the classic picker failure: a component re-emitting a normalized value on mount once its picker data arrives. That turns "open and save" into a silent rewrite of every rule's conditions.

## 2. What Is Solid

- **Native edit page kept** (user decision), with no custom save bar or discard dialog to get wrong.
- **Mapping, serializer and engine declared untouchable;** error-path mapping stays tied to visible controls.
- **The index for component commands comes from GetFormItems (03-07 pattern);** commands fail closed.
- **ContactGroupAccess extracted with a refactor gate** (query tester tests green first); recipient lists stay excluded, and the 500 cap is shared and documented.
- **Stale facet values and contexts stay visible,** labelled, rather than silently dropped.
- **Status logic reused from 13-01,** not re-implemented.

## 3. Enterprise Gaps Identified

1. Blocking checkpoint vs the APPROVAL-CHECKS decision.
2. FacetValues counts affected by synonyms or contexts.
3. No automated guard against mount-time value rewrites (AC-5 relied on one manual SQL check).
4. Browser time zone vs UTC validity dates in the live status (13-01's server status uses UTC).
5. Revealed free-text inputs had no defined accessible names.
6. Contact groups beyond 500 are truncated (shared with the query tester).
7. The no-permission path may not be testable on the host if no such role exists.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Blocking checkpoint vs project decision (gap 1) | Frontmatter, tasks, verification | `autonomous: true`; the checkpoint becomes Task 3: perform checks 1–8 on the host and record them as a Pending APPROVAL-CHECKS entry; restore rules and data |
| 2 | Facet counts polluted by synonyms/contexts (gap 2) | AC-6, Task 1 + tests | FacetValues sets ApplySynonyms = false and no Contexts in addition to rules/analytics off; a test asserts it |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Mount-time rewrites (gap 3) | AC-5, Task 2 | No onChange on mount or on picker-data arrival; node tests prove group/custom/listed/stale fixtures map back to identical stored strings |
| 2 | UTC vs browser time zone (gap 4) | AC-2, Task 2 | Status compares UTC instants; node test under TZ=America/New_York |
| 3 | Accessible names of revealed inputs (gap 5) | AC-1, Task 2 | "Facet value" / "Custom context" labels, checked in the a11y tree |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | >500 contact groups truncated (gap 6) | Same cap as the query tester; already planned for KNOWN-LIMITATIONS; Custom context covers the rest |
| 2 | No-permission role on the host (gap 7) | Task 3 records it as test-covered if no such role exists; the server test covers the gate |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - node tests for choice mapping, the no-rewrite guarantee and UTC status;
  - C# tests for command boundaries;
  - a host record in APPROVAL-CHECKS, including byte-identical JSON checked by SQL.
- **Silent failures:** picker-load failures show an error bar and fall back to free text. Nothing can be rewritten silently (new guard).
- **Trust boundary:** the index and user come from the server; contact-group visibility follows Kentico's Contact groups → View.

## 6. Final Release Bar

- **Must be true:**
  - existing rules save byte-identical;
  - facet counts are the index's own;
  - picker commands fail closed;
  - verification is recorded for milestone review.
- **Remaining risks:** the 500-group cap; native form limitations (stacked fields, CheckBox not Switch), already planned as handoff deviations.
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 2 must-have + 3 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
