# Enterprise Plan Audit Report

**Plan:** .paul/phases/02-event-capture/02-01-PLAN.md
**Audited:** 2026-10-09
**Verdict:** Conditionally acceptable → acceptable after applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The capture design (decorator, bounded drop-on-full queue, background bulk insert, no identity columns) matches the agreed constraints. But the plan protected search only from database failures, not from failures in its own capture code; its installer would race on a web farm at startup; a single over-length value could fail a whole 500-row batch; and the session cookie could throw when the response had already started. With the upgrades below I would approve it.

## 2. What Is Solid

- **Off-path capture with DropWrite**: correct trade-off — analytics loss over search latency.
- **No identity columns**, IP/UA used only transiently: matches PROJECT.md compliance constraints.
- **Explicit exclusions** (TrackAnalytics, bots, empty index) with the admin tester hard-wired to false.
- **Module class + verified Kentico bulk APIs**, CI disabled: follows the user's decision with verified signatures.
- **End-to-end DB check** in xbk instead of trusting unit tests alone.

## 3. Enterprise Gaps Identified

1. Exceptions inside the capture path (scrubber regex, cookie write, enqueue) could break the search request.
2. Concurrent installer runs on multiple instances at startup could duplicate the class or crash startup.
3. One value longer than its column fails `BulkInsert` for the entire batch.
4. Writing the cookie after `Response.HasStarted` throws.
5. Singleton decorator could capture scoped/request services (cookie-level provider) — wrong visitor's level or a DI validation failure.
6. Browser prefetch/prerender of search URLs would create phantom searches.
7. `ReceivedUtc` comes from different nodes' clocks; ordering by time across nodes is unreliable.
8. The dev-only demo controller had no proof it is unreachable outside Development.
9. AC-8's expected row count was wrong in the plan (said 4, listed 3).
10. Language/channel dimensions not captured.
11. Clock drift tolerance of the 30 s rule.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Capture exceptions reach the caller | AC-6, Task 3 | Whole capture path wrapped; rate-limited log; result untouched |
| 2 | Installer race on web farm | AC-7, Task 1 | Serialized creation + concurrent-install test |
| 3 | One long value fails a batch | AC-6, Task 3 | Length-check/truncate every field before insert |
| 4 | Cookie write after response start | AC-3, Task 2 | HasStarted → reuse existing value, skip write; tested |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Scoped services captured by singleton | AC-3, Task 2 | Resolve per call from the request |
| 2 | Prefetch phantom searches | AC-5, Task 3, Task 4 | Exclude by `Sec-Purpose`/`Purpose` header; verified |
| 3 | Cross-node clock ordering | Task 3 | Order by client Sequence; drift tolerance recorded in KNOWN-LIMITATIONS |
| 4 | Demo endpoint exposure | Task 4 | 404 verified in non-Development |
| 5 | Wrong expected row count | Task 4 verify | Corrected to 3 rows |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Language/channel dimensions | No source in the request yet; added when the search API carries them |
| 2 | Clock-drift tolerance beyond NTP | Recorded limitation; rollup uses Sequence for ordering |
| 3 | Unit-level DB writer test | Verified end-to-end against the real database in Task 4 |
| 4 | No real-traffic consumer in xbk | Product question raised to the user (Suite search widget/page) |

## 5. Audit & Compliance Readiness

- **Evidence:** DB rows shown for each scenario, concurrency test for the installer, mutation check on exclusions.
- **Silent failures:** drops are counted and logged; batch failures logged once per minute with size.
- **Privacy:** schema listing in the APPLY report proves no identity columns; session cookie only at Visitor/All.

## 6. Final Release Bar

- Must be true: AC-1..AC-8 with evidence; installer concurrency test green; capture-exception test green.
- Remaining risk: no production traffic path in xbk until a Suite front end exists.
- I would sign off on the plan as amended.

---

**Summary:** Applied 4 must-have + 5 strongly-recommended upgrades. Deferred 4 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
