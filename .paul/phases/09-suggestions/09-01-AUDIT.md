# Enterprise Plan Audit Report

**Plan:** .paul/phases/09-suggestions/09-01-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The architecture is right: a pure engine, a Core API with the endpoint as a thin adapter, opt-in per index, and its own limiter. But this is the first time the Suite **publishes analytics-derived text to anonymous visitors**. The plan reused the admin-report exposure rule (the surfacing threshold) unchanged, and it claimed a blocklist change applies "at once" while also setting a 60-second public cache. That claim was false for any browser or CDN. With the upgrades applied, I would approve it.

## 2. What Is Solid

- **Opt-in per index, 404 by default.** Nothing leaks from indexes a site didn't choose.
- **Strings only, no search, no analytics writes.** The smallest possible public surface, and it is identity-free.
- **One shared Surfacing helper.** Reports and suggestions can never disagree on which text is publishable, and report parity is protected by the existing DB tests.
- **Placeholder and no-result filtering.** No `{email}` artefacts and no dead-end suggestions.
- **A separate limiter partition set** from events. A typing visitor cannot exhaust the click budget.

## 3. Enterprise Gaps Identified

1. **The public exposure floor equals the admin floor.** Reports are behind admin permissions; suggestions go to everyone. A query surfaced long ago but searched once in the window would still be published.
2. **Block latency misrepresented.** The server cache is invalidated immediately, but `public, max-age=60` lets browsers and CDNs keep serving a blocked term.
3. **Cache pollution through the public C# API.** Arbitrary index names would create empty cache entries.
4. **Proxy/CDN rate limiting.** Behind a proxy without ForwardedHeaders, every visitor shares one IP budget. The first 300 requests a minute lock out the whole site.
5. **XSS contract unstated.** Suggestion text is visitor-originated. The consumer must render it as text.
6. **Seeded demo data becomes public** on the host through suggestions.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Public exposure floor | AC-2 | `Suggestions.MinSearches` (default 5) searches inside the window, on top of the surfacing threshold |
| 2 | Block latency vs public cache | AC-5 | `SuggestionsMaxAgeSeconds` (default 60, 0 → no-store); docs state the real latency |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Cache pollution | AC-4 | Index checked via ILuceneIndexManager before any query or cache |
| 2 | Proxy rate limiting | AC-5 | ForwardedHeaders requirement documented |
| 3 | XSS contract | AC-5 | Untrusted text: render with textContent (docs plus the 09-02 contract) |
| 4 | Seeded data exposure | Task 4 | Noted in APPROVAL-CHECKS #10, linked to #4 |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Duplicate blocklist entries (same MatchKey and mode) | Prevented by 09-03 admin validation; harmless to matching meanwhile |
| 2 | Language-aware suggestions for multilingual indexes | Queries are language-agnostic text today; revisit with language dimensions (02-01 deferral) |
| 3 | CORS | Same-origin only by default (no CORS headers); a cross-origin need appears in v0.4 |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - engine unit tests;
  - DB tests (surfacing parity, floors, block immediacy, no analytics writes);
  - endpoint tests for 200/404/400/429 and headers;
  - host curl;
  - APPROVAL-CHECKS #10.
- **Privacy:** published text passes the surfacing threshold, a window floor, placeholder scrubbing and the marketer blocklist. Nothing identifies visitors.
- **Reconstruction:** options are validated on start with key names; docs state every filter.

## 6. Final Release Bar

- **Must be true:**
  - the window floor is applied;
  - the max-age is configurable and documented;
  - index checked before cache;
  - endpoint strings-only and opt-in;
  - no analytics writes;
  - report tests unchanged.
- **Remaining risk:** repeated free-text personal data (for example a name typed by many visitors) can surface. Mitigated by the threshold, the window floor and the blocklist, and documented as a residual risk.
- **Sign-off:** I would sign off on the plan as amended.

---

**Summary:** Applied 2 must-have + 4 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
