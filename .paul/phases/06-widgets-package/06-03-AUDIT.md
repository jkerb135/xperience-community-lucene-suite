# Enterprise Plan Audit Report

**Plan:** .paul/phases/06-widgets-package/06-03-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan would have shipped widgets that never appear. Kentico only discovers `RegisterWidget` attributes in assemblies marked `[assembly: AssemblyDiscoverable]`. The core and Admin assemblies have that attribute; the Widgets assembly does not. Every unit test would pass, and the failure would only show up at the host step or on a customer's site.

The preview exclusion is the right control, but it was specified to fail open. If Kentico's preview state cannot be read, editing traffic would be recorded as visitor searches.

With these upgrades applied, I would approve the plan.

## 2. What Is Solid

- **Widgets are thin wrappers over the 06-02 components.** They share the same views and the same model building, so there is one place to fix markup and site overrides apply to both.
- **Preview and edit-mode exclusion lives in the Widgets package's page search.** Core capture is not changed, which keeps the core contract stable.
- **The results page is chosen with a page selector and resolved at render time.** This survives URL changes, with a defined fallback.
- **Editors get messages, visitors get nothing.** A misconfigured index shows a message in edit mode and renders nothing on the live site.
- **The contact-group query is proven on the disposable database.** The test cleans up after itself, and `comm_projects` contact groups stay read-only.
- **The host step is controlled.**
  - Host files are backed up before they change.
  - Only the host instance Claude started will be restarted.
  - The permanent page was the user's decision.
  - The SQL evidence for the end-to-end check is spelled out.

## 3. Enterprise Gaps Identified

1. The Widgets assembly is missing `AssemblyDiscoverable`, so no widgets get registered.
2. Preview detection fails open on error.
3. In Page Builder edit mode, submitting the search box form would navigate the editor frame away.
4. Fragment caching (`<cache>` tag helpers), Kentico page output caching or a CDN around pages that contain the widgets would replay one visitor's `sid`/`seq` links to everyone and skip analytics. Nothing documents this.
5. A results page selected from another website channel would resolve to a relative path on the wrong domain.
6. The test traffic from the end-to-end check stays in the host's analytics.
7. Widget names, labels and the placeholder are English-only.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Widgets never registered (gap 1) | AC-1, Task 1 action + tests | Added `[assembly: AssemblyDiscoverable]`; the registration test asserts it is present. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Preview detection fails open (gap 2) | AC-2, Task 1 | An exception is treated as preview (untracked), with a throttled warning. Tested. |
| 2 | Edit-mode form navigates the editor frame (gap 3) | AC-2, Task 1 | The search box model gets `IsEditMode`, and the controls sit inside a disabled fieldset in edit mode. Tested. |
| 3 | Caching layers defeat tracking (gap 4) | Task 2 docs | The contract doc warns against `<cache>`, page output caching and CDNs on these pages. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Results page in another channel (gap 5) | Search pages and results normally live in one channel. Kentico's page selector already scopes to the current channel by default. Revisit for multi-channel sites. |
| 2 | Test traffic in the host's analytics (gap 6) | The host is a test site. Keeping its traffic was the user's decision. Retention purges raw rows. |
| 3 | English-only widget strings (gap 7) | Already listed for KNOWN-LIMITATIONS. Localization expressions can be added without breaking anything. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - registration test (including discoverability);
  - preview and edit-mode tests;
  - contact-group database test;
  - end-to-end SQL rows, response headers and network status on the real host.
- **Silent failures closed:**
  - missing discovery (tested);
  - preview read errors (fail closed and logged);
  - misconfigured index (edit-mode message and log).
- **Privacy:** no contact lookup in preview, and contact groups are never stored.
- **Change control on the untracked host:** backups first. The decision to keep the page is recorded.

## 6. Final Release Bar

- **Must be true before shipping:**
  - The widgets appear in Page Builder.
  - Editing and preview never create analytics rows.
  - The end-to-end evidence matches AC-5.
  - The contact-group query is proven.
  - Item #3 is written to APPROVAL-CHECKS.md.
- **Remaining risks:**
  - Results pages in other channels.
  - English-only strings.
  - Test traffic in the host's data.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
