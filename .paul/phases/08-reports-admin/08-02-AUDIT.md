# Enterprise Plan Audit Report

**Plan:** .paul/phases/08-reports-admin/08-02-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The query table itself is low risk: one VIEW-gated command, surfaced text only, and server pages that keep their own permission gates. The risk is in the handoff, which writes into editors that also edit existing data.

**Data-integrity gap.** The plan guards consumers with target + index + expiry + "form is empty". The rule edit page and the rule create page use the same conditions editor, and an existing rule can have empty conditions. Example:
1. A marketer clicks "Create rule".
2. Within two minutes they open an existing condition-less rule on the same index instead.
3. The pending handoff fills that rule, and the next save silently changes it.

That edit is unrequested and goes unnoticed, which is release-blocking.

**Plausible-but-wrong outputs.** Analytics text can contain the scrubber's `{email}` / `{number}` placeholders. A rule or synonym built from such a text can never match a live query.

## 2. What Is Solid

- **Prefill is client-only into our own components.** Server create pages keep their Manage-rules / Manage-synonyms gates, so a disabled-looking button that is enabled anyway cannot bypass authorization.
- **No query text leaves the surfaced set.** The rows come straight from the 07-01 service, and the browse row and "(other)" are excluded from actions.
- **Row actions use the npm Table's `ActionCell`.** Its focusable buttons avoid the known row-focus gap.
- **No URL or server state.** This is consistent with the 08-01 router finding.

## 3. Enterprise Gaps Identified

1. A pending handoff can be consumed by the edit page of an existing rule or synonym with an empty field.
2. Placeholder texts (`{email}`, `{number}`) produce rules and synonyms that can never match.
3. The query tester's prefilled search must stay untracked; that was implied but not stated.
4. A round trip to a target page loses the report's range, sort and page (no URL state), which breaks the measure → act → return loop.
5. The handoff only works within the same tab; it doesn't survive a new tab.
6. The rule name is not prefilled.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Handoff could edit existing items (gap 1) | AC-3, Task 2, tests | The handoff is bound to the exact target page path; consumers take it only when `location.pathname` ends with that path. A test covers a sibling edit path. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Placeholder texts (gap 2) | AC-3, Task 1, tests | The server flags `HasPlaceholder` using `QueryScrubber`'s constants; Create rule and Add synonym are disabled with an explanatory title; DB fixture test. |
| 2 | Untracked prefilled search (gap 3) | AC-3 | The prefilled run uses the tester's existing `TrackAnalytics = false` path. |
| 3 | Report state lost on return (gap 4) | AC-2, Task 2, tests | Range, sort and page are kept per index in sessionStorage and restored when still allowed; never in the URL. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Same-tab-only handoff (gap 5) | This was the user's chosen mechanism. The failure mode is an empty form, never wrong data. Recorded in KNOWN-LIMITATIONS. |
| 2 | No rule-name prefill (gap 6) | The name is a native server field. The marketer types it (one field), and the required-field validation already prompts. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - DB tests for every sort, paging, Other, errors and placeholders.
  - Node tests for every handoff guard, including the sibling-path case.
  - A host SQL comparison.
  - A check that existing items stay untouched.
- **Silent failures closed:** edits to existing rules and synonyms, rules that can never match, analytics pollution from report links.
- **Authorization:** unchanged server gates; client disabling is cosmetic and documented as such.

## 6. Final Release Bar

- **Must be true:**
  - Path-bound handoff, with its test.
  - Placeholder rows cannot become rules or synonyms.
  - The host check shows existing items untouched.
  - All tests green.
- **Remaining risks:** same-tab handoff; manual rule name; full-page navigation.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 3 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
