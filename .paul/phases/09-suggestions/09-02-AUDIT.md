# Enterprise Plan Audit Report

**Plan:** .paul/phases/09-suggestions/09-02-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan gets the important things right: progressive enhancement, byte-identical HTML when off, textContent-only rendering, the ARIA 1.2 pattern, and a layered public JS API. It would fail in production in three predictable places:
- **Two search boxes on one page** (header plus page body is common). "Stable ids derived from the component instance" was unspecified, and duplicate ids break `aria-controls` and `aria-activedescendant`.
- **Strict Content-Security-Policy sites**, which enterprise customers run. Nothing forbade inline scripts or styles.
- **The rate limiter.** The client had no back-off, so a 429 would be retried on every keystroke.

The "byte-identical" proof also lacked a baseline. With the upgrades applied, I would approve it.

## 2. What Is Solid

- **Off means unchanged.** No risk to sites that don't opt in.
- **textContent-only rendering with an XSS regression test.** It closes the injection path for visitor-originated text.
- **Pure state machine plus a thin DOM binding.** The keyboard model is testable in node and reusable by custom UIs and v0.4.
- **Latest-request-wins with AbortController and a per-page cache.** Correct results under fast typing.
- **No host content edits** (single-index fallback). Verification doesn't mutate customer content.

## 3. Enterprise Gaps Identified

1. **Duplicate ids** with several boxes per page; the script and stylesheet would also be emitted twice.
2. **CSP.** Inline config or style attributes would be blocked under `script-src 'self'` / `style-src 'self'`.
3. **No back-off after 429,** so a client keeps hitting the limiter.
4. **No request guard for input over 100 characters,** guaranteeing 400s.
5. **Byte-identical claim without a pre-change golden.** A post-change comparison would be circular.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Duplicate ids / double script | AC-1 | Per-request counter for ids; script and styles emitted once per request |
| 2 | Byte-identical proof without a baseline | Task 2 step 7 | Golden HTML captured before the view changes |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | CSP compatibility | AC-1 | No inline script or style; data-* config; the `hidden` attribute and classes |
| 2 | 429 hammering | AC-2, Task 1 tests | 30 s back-off after 429 |
| 3 | Over-long input | AC-2, Task 1 tests | No request above 100 characters |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Localised strings and RTL | Widgets are English-only by recorded limitation; localisation is a package-wide change |
| 2 | Suggestion-use analytics (selected vs typed) | Out of scope by plan; belongs with activities/instant search |
| 3 | Mobile virtual-keyboard nuances beyond pointerdown/blur | The pattern covers touch selection; refine from real-device feedback |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - node tests (state, debounce, stale, cache, errors, back-off, XSS);
  - rendering tests against the golden;
  - host checks (accessibility tree, network, counts);
  - APPROVAL-CHECKS #11.
- **Accessibility:** WAI-ARIA 1.2 combobox with unique ids and a polite status. Verifiable in the accessibility tree.
- **Security:** no HTML injection path; CSP-friendly; no tracking.

## 6. Final Release Bar

- **Must be true:**
  - unique ids and a single asset emission with two boxes;
  - HTML identical to the golden when off;
  - XSS test passes;
  - back-off and length guards are tested;
  - host keyboard and accessibility checks are recorded.
- **Remaining risk:** English-only announcement text.
- **Sign-off:** I would sign off on the plan as amended.

---

**Summary:** Applied 2 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
