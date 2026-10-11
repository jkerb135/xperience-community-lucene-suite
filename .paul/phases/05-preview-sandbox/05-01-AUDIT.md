# Enterprise Plan Audit Report

**Plan:** .paul/phases/05-preview-sandbox/05-01-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan correctly avoids building a second preview page. It reuses the query tester's search, guards, regions and the proven picker pattern, and it touches no search core.

The plan crossed an authorization boundary without checking it. The new ContactGroups command would give every Site search → View user the list of the site's contact groups. Those are Kentico Digital marketing data, owned by the Contact groups application and its permissions.

It also left the rank comparison undefined for multilingual indexes. This host returns several documents per ItemGuid, so markers would be wrong exactly where this project already hit duplicates (03-05, 03-07).

With these fixed, I would approve it.

## 2. What Is Solid

- **No search-core change.** Contexts already flow through `SuiteSearchRequest`, and the core validates them: count, length and blanks [Certain, SearchExecutor.Validate]. The page adds no parallel validation.
- **Each column has its own latest-request guard.** Rapid toggling can't mix answers between columns.
- **Hit lists are focusable buttons.** Compare mode doesn't inherit the npm Table's row-focus gap (03-02).
- **Defaults make the first comparison meaningful.** Baseline has rules and synonyms off; Tuned has them on.
- **Free text and contact groups end up as the same context strings the site sends.** The preview exercises exactly the rule conditions production uses.

## 3. Enterprise Gaps Identified

1. **ContactGroups command leaks Digital marketing data across app permissions.** Site search View is not Contact groups View.
2. **Rank diff undefined for duplicate ItemGuids** (multilingual).
3. **Analytics separation unproven.** Compare doubles searches; they must stay out of analytics.
4. **Silent truncation at 500 contact groups.**
5. No saved comparison presets or URLs (scope).
6. Contact-group membership is not actually evaluated, only the code name is sent. This is documented and inherent to the design.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Cross-application data exposure (gap 1) | AC-2, Task 1, design outline, tests | The command checks View on Kentico's Contact groups app with the 03-01 IApplicationPermissionEvaluator; the app identifier is verified at APPLY. Without it, the command returns `Allowed = false` and queries nothing. The client hides the picker, and free text still works. A test covers it. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Duplicate ItemGuids in rank diff (gap 2) | AC-5, Task 2 helpers + tests | Rank is the first occurrence per ItemGuid, positions count distinct ItemGuids, and duplicates get no marker. A multilingual test is added. |
| 2 | Analytics separation (gap 3) | AC-5, Task 1 tests | A request-mapping test asserts TrackAnalytics is false, and that contexts and page size are mapped. |
| 3 | Truncation at 500 (gap 4) | AC-2, Task 1, outline, KNOWN-LIMITATIONS | A `Truncated` flag (TopN 501) is added, and the panel notes "Showing the first 500 contact groups". |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Saved presets or URLs (gap 5) | Out of scope by user decision; settings are cheap to re-pick. |
| 2 | Real membership evaluation (gap 6) | The suite passes contexts and never resolves them (PROJECT key decision). Already in the plan's KNOWN-LIMITATIONS. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - .NET tests: request mapping (contexts, page size, TrackAnalytics), the permission-gated contact groups, truncation, and recipient-list exclusion (DB).
  - Node tests for the diff, including duplicates and context limits.
  - A checkpoint showing a context-targeted rule firing only in the simulating column.
- **Silent failures:** errors return as messages per column, failure warnings per column, and truncation is visible.
- **Authorization:**
  - Read-only commands need Site search View.
  - Contact-group data also needs Contact groups View.
  - Nothing writes.
- **Privacy:** no analytics from previews. Contact groups are read by name only, never their members.

## 6. Final Release Bar

- **Must be true before shipping:**
  - Contact groups are visible only with Kentico's permission.
  - Rank markers are correct with duplicate ItemGuids.
  - Previews are untracked.
  - The checkpoint passes.
- **Remaining risks:**
  - Compare uses the top 20 hits.
  - The picker caps at 500 with no search.
  - Membership is not simulated, only names are sent.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 3 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
