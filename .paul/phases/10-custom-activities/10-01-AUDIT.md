# Enterprise Plan Audit Report

**Plan:** .paul/phases/10-custom-activities/10-01-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The architecture is right:
- Kentico owns consent;
- types ship disabled;
- activity text never enters Suite tables;
- logging is skipped cheaply when a type is off.

But the plan as written would leak unscrubbed query text into contact activities through a field it never mentions:
- Kentico fills ActivityURL and ActivityURLReferrer from the current request when the caller leaves them empty (strong inference; verified during APPLY).
- For a search, that URL is the results page with the raw `?q=`. For a click beacon, the referrer is.
- Every scrubbing step in the plan would be bypassed, and an email address typed into the search box would be stored against an identified contact.

That is release-blocking for a feature whose compliance story is "scrubbed text, consent-gated". A second must-have: the plan didn't define what happens without an HTTP context, and Kentico documents that custom activities need one.

## 2. What Is Solid

- **Types installed disabled and never updated afterwards.** There is no data growth on upgrade, the marketer's choice survives re-installs, and the opt-in is native Kentico UI.
- **Consent and cookie level left to Kentico's logger.** This matches the PROJECT constraint; there is no parallel Suite gate to drift.
- **Activity text kept out of SearchInteraction and SearchEvent.** The identity-free analytics boundary is untouched.
- **Click contract v2 is additive and lenient** (malformed optional fields dropped, not rejected), so click counts and v1 senders are unaffected.
- **Submitted searches only.** Continuations and browse are excluded, so paging cannot inflate activities.
- **Cheap disabled path:** one cached type check, and the web-page lookup runs only when the click type is enabled.

## 3. Enterprise Gaps Identified

1. ActivityURL/ActivityURLReferrer would be auto-filled from the raw request, so the unscrubbed `q` (possibly an email) is stored. For clicks, the beacon's Referer is the results page.
2. Callers without an HTTP context (background jobs, custom services) were not handled.
3. Control characters in client-supplied title/query would reach the admin activity log.
4. The type-enabled cache was left open (provider or IProgressiveCache). A custom cache risks stale toggles across a web farm.
5. No check that toggling a type takes effect without a restart.
6. Kentico's own page-visit activity on the results page stores the URL with the raw `q`. This is outside the Suite, but sites need to know.
7. Forged beacons can write arbitrary titles into the forger's own contact activities only, because the current contact is per cookie.
8. Activity rows for searches add per-contact data volume once enabled.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Raw query leaks through ActivityURL/Referrer (gap 1) | AC-2, AC-3, Task 1 action + tests, Task 3, verification | Suite always sets URL and referrer without a query string; unit test with `?q=jane@example.com`; host SQL check over all activity columns |
| 2 | No-HTTP-context behaviour (gap 2) | AC-2, Task 1 action + tests | Return early when `IHttpContextAccessor.HttpContext` is null; test |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Control characters (gap 3) | AC-3, Task 1 | Stripped from title and value |
| 2 | Type cache choice (gap 4) | Task 1 | Use Kentico's ActivityTypeInfo provider cache (web-farm invalidated); no Suite cache |
| 3 | Toggle without restart (gap 5) | AC-4, Task 3 | Host check |
| 4 | Page-visit leak outside the Suite (gap 6) | Task 3 docs | Privacy note with options in docs/activities.md |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Forged click text (gap 7) | Affects only the forger's own contact; the beacon is already documented as forgeable and rate-limited; the user decision is recorded in APPROVAL-CHECKS #13 |
| 2 | Activity volume (gap 8) | Opt-in per type by a marketer; Kentico's activity retention applies |

## 5. Audit & Compliance Readiness

- **Evidence:** unit tests cover filters, URL scrubbing and failure isolation. The host verification records OM_Activity rows for both the disabled and enabled paths, plus an email-in-query SQL check. Counts are taken before and after.
- **Silent failures:** activity failures are logged through a throttled warning and never break search, which is acceptable for a best-effort marketing signal.
- **Reconstruction:** activities themselves carry the contact, time and URL (scrubbed), which is Kentico's standard trail.
- **Ownership:** enabling is a marketer action in native Kentico UI; consent is the site's configuration.

## 6. Final Release Bar

- **Must be true:**
  - no unscrubbed query in any Suite-written activity column;
  - nothing is logged without consent, an HTTP context or an enabled type;
  - analytics are unchanged.
- **Remaining risks:**
  - forgeable click text on the forger's own contact;
  - Kentico's page-visit URL (documented).
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 2 must-have + 4 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
