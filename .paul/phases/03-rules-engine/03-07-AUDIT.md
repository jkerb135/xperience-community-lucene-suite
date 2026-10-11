# Enterprise Plan Audit Report

**Plan:** .paul/phases/03-rules-engine/03-07-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan reuses the proven 03-06 patterns: native pages, component commands, server-only validation, and permission, ownership and concurrency checks. It keeps the index context on the server, and its checkpoint finally closes the carried verification gaps.

Two defects would have shipped wrong behaviour:
1. **Duplicate picker results.** Searches return one hit per document, and the CommunityProjects index is multilingual (Spanish documents share ItemGuids, 03-05). The picker would list one item twice; ticking both produces a duplicate-pin error.
2. **The command trust boundary was underspecified.** If Kentico doesn't run the page's `GetFormItems` on component-command requests, the plan didn't say what happens: no index, or worse, a client-supplied one. `Describe` also took an unbounded client array that throws.

With these fixed, I would approve it for APPLY.

## 2. What Is Solid

- **Index name taken from the page's bound parameter, not the client.** That's the right trust boundary for read commands.
- **A single TermsFilter lookup for up to 350 guids,** one entry per item. This reuses the 03-05 multilingual lesson.
- **Picker built as a Checkbox list in a SidePanel,** avoiding the known non-focusable Table rows (03-02), with focus returned to the opener.
- **Per-field validation by JSON path for pins and hides,** reusing `RuleSerializer` as the single source of truth.
- **The query tester shows rule effects from data the response already carries** (FiredRuleIds, UserData, RulesFailed, PinnedByRuleId). The Apply rules switch reuses the existing flag.
- **Concurrency (LastModified) still guards** the now-editable pins and hides; unknown stored properties are preserved.
- **The checkpoint explicitly closes the 03-02 and 03-06 gaps.**

## 3. Enterprise Gaps Identified

1. **Duplicate picker results in multilingual indexes.**
2. **Component commands with an unresolved index:** behaviour was unspecified, with a risk of a client-supplied index or unhandled errors.
3. **`Describe` input:** unbounded and not de-duplicated, and throwing on more than 350 would surface as a server error.
4. **"Pinned and hidden" claimed to show on the Hides field at save,** but `ProcessFormData` errors are form-level only (03-06 implementation).
5. **Fired-rule name lookup** wasn't specified as one query; per-rule queries could mean up to 500 round trips.
6. **Search exception handling** wasn't specified beyond ArgumentException.
7. **Stored fields (titles, content type) are visible to View-only users** through the picker.
8. **The picker is limited to 20 results without paging.**
9. **Multilingual titles:** the title shown is whichever language document has the lowest doc id.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Duplicate picker results (gap 1) | AC-2, Task 1, tests | Search returns each ItemGuid once (the first hit), with a test of two language documents giving one item. |
| 2 | Fail-closed commands (gaps 2, 3, 6) | AC-2, Task 1, tests | Unresolved index → error result. Describe de-duplicates and caps at 350 → error result. ArgumentException and InvalidOperationException → error result. APPLY confirms `GetFormItems` runs on command requests, with a server-side fallback (never the client). |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Field-level error for pinned and hidden (gap 4) | AC-1, Task 1, tests | `RuleHidesConflictValidationRule` on the Hides field reads Pins via IFormFieldValueProvider; the picker never offers a pinned item for hiding or the reverse. |
| 2 | One fired-rule query (gap 5) | Task 1 | `WhereIn(RuleID)` + IndexName in a single query. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Stored fields visible to View users (gap 7) | Existing 01-03 deferral; the query tester already shows them to View users. Revisit with field-level visibility settings. |
| 2 | Picker limited to 20 results (gap 8) | Searching narrows it; recorded in KNOWN-LIMITATIONS. Paging only if marketers ask. |
| 3 | Multilingual title choice (gap 9) | Display only. Requests have no language dimension yet (02-01 deferral); recorded in KNOWN-LIMITATIONS. |

## 5. Audit & Compliance Readiness

- **Evidence:** pure tests for the lookup, de-duplication, caps, conflict rule and state helpers. The checkpoint covers the real index, keyboard use, both widths, roles and the date check.
- **Authorization:** read-only commands need View; writes stay behind the 03-06 Manage rules gates. The index context never comes from the client.
- **Silent failures:** command failures return explicit error states; "Not in this index" is shown for stale pins and hides.
- **Traceability:** rule changes are in the Kentico event log (03-03).

## 6. Final Release Bar

- **Must be true before this ships:**
  - The picker lists each item once.
  - Commands fail closed.
  - Pinned and hidden conflicts are refused on the right field.
  - The query tester shows fired rules, pinned markers and the failure warning.
  - The carried gaps are closed or explicitly re-recorded.
- **Remaining risks:**
  - View users can see stored fields.
  - The picker shows 20 results.
  - Multilingual titles are chosen by doc id.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 2 must-have + 2 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
