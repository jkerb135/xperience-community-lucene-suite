# Enterprise Plan Audit Report

**Plan:** .paul/phases/06-widgets-package/06-02-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The plan is the first public, anonymous HTML surface the Suite renders. It gets the analytics mechanics right: one search per request, a server-minted id on submit, `sid`/`seq` continuation, de-duplication, and Sec-Fetch-Site against shared links.

Two gaps would ship real harm:

- **Script injection through result links.** Indexed URL values are rendered into `href` with HTML encoding only. Encoding does not stop a `javascript:` URL. Search ids would also be appended to links that point to other sites.
- **Caching.** The plan says nothing about caching. A results page held by output caching or a CDN replays one visitor's `sid`/`seq` links and contact-group-specific results to everyone, and silently records nothing.

Error handling also failed the wrong way: a search outage would turn every page with a results component into a 500. I would approve with the changes below applied.

## 2. What Is Solid

- **The URL is the only state.** No session or cookie is added, and the rollup's earliest-wins de-duplication absorbs reloads.
- **Sec-Fetch-Site gating.** A pasted, bookmarked or cross-site link starts a new search, so it never collides with the original visitor's rows. This is cheap and needs no state.
- **Visitor input fails closed.** Tampered input becomes the empty/invalid state, and the parser never throws.
- **Contact groups are handled conservatively.** The lookup never creates a contact, excludes recipient lists, is capped and cached, and the groups are not stored in analytics (core capture does not persist Contexts).
- **No inline script or style.** The output is CSP-safe and the views are overridable.
- **The click beacon never prevents navigation.**

## 3. Enterprise Gaps Identified

1. A hit URL with a scheme like `javascript:` or `data:`, or a protocol-relative `//host` value, becomes a live link (stored XSS via index content).
2. `sr=` and click data are added to links that leave the site, leaking search ids to third parties and recording clicks the Suite cannot attribute.
3. No caching control. Output cache or CDN caching replays `sid`/`seq` and personalised results across visitors and skips analytics.
4. "Any other exception propagates" means a search outage breaks the whole page. A wrong index name is indistinguishable from "no results" (silent misconfiguration).
5. A selected facet value missing from the top values disappears, so the visitor cannot remove that filter.
6. Facet and paging links invite crawlers into combinatorial URL spaces.
7. There is no noindex control for results pages.
8. The contact-membership cache dependency is coarse: any membership change flushes every contact's entry.
9. `seq` caps at 10 000; the next link starts a new search.
10. Going back in the browser and then filtering again can reuse a `seq`. The duplicate row is dropped, so that request goes uncounted.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Unsafe hit hrefs; ids leaving the site (gaps 1–2) | AC-4, Task 2 tests | Links only for relative paths (not `//`) or http/https URLs. `sr=` and data attributes only for same-site links. Tests for `javascript:`, `data:`, `//evil`, external https and same-host absolute URLs. |
| 2 | Cached search HTML (gap 3) | AC-2, Task 1 action + tests, verification | After a search, set `Cache-Control: no-store` and turn off output-cache storage for the request. Tested, and documented in the contract. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Search outage fails the page; misconfigured index is silent (gap 4) | AC-2, Task 1 | Unknown index throws `InvalidOperationException` (developer error). Other failures render an "unavailable" state with a throttled error log that contains no query text. Aborted requests still propagate. |
| 2 | Active facet filters can't be removed (gap 5) | AC-4, Task 2 tests | Values selected in the URL always render (selected, count 0). |
| 3 | Crawl traps (gap 6) | AC-4, Task 2 tests | `rel="nofollow"` on paging and facet links. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Noindex header (gap 7) | A site decision (some sites index search landing pages). The view can't reach `<head>`, and sites can add `X-Robots-Tag` in their own pipeline. Mention in the contract docs. |
| 2 | Coarse cache dependency (gap 8) | Only costs extra membership queries after changes; correctness is unaffected. Revisit when real traffic shows the load. |
| 3 | `seq` cap and back-button `seq` reuse (gaps 9–10) | Both lose at most one request's count, with no corruption. Recorded in the plan's KNOWN-LIMITATIONS list. |

## 5. Audit & Compliance Readiness

- **Evidence:** rendering tests over hostile values, URL safety, caching headers and the failure states, plus the URL-model and click-payload tests.
- **Silent failures:**
  - A misconfigured index is now loud.
  - Search outages are logged (throttled) and visible to visitors as "unavailable".
  - Visitor tampering is debug-only by design.
- **Privacy:**
  - Contexts are used only for the search and never stored.
  - Search ids never leave the site.
  - Caching is disabled, so personalised results are never replayed to other visitors.
- **Open:** the page-size and contact-group options are site-wide, which departs from the per-widget decision. It is technically required by the shared search, and the user must acknowledge it at approval.

## 6. Final Release Bar

- **Must be true before shipping:**
  - No unsafe href and no off-site ids.
  - Search pages are never cached.
  - Search failures never take a page down.
  - Every active filter can be removed.
  - The rendering tests prove all of the above.
- **Remaining risks:**
  - Duplicate or capped `seq` can lose single requests.
  - Results pages are indexable unless the site adds noindex.
  - The membership cache flushes on any group change.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 2 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY (pending the user's acknowledgement of the site-wide options)

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
