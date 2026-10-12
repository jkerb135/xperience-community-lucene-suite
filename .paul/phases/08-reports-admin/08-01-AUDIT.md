# Enterprise Plan Audit Report

**Plan:** .paul/phases/08-reports-admin/08-01-PLAN.md
**Audited:** 2026-10-12
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The server side is sound. One VIEW-gated command reads only aggregates through the already-audited service, and the index always comes from the page. The risks are in the client, where wrong numbers would look plausible:

1. **Off-by-one dates.** Reports are keyed by UTC calendar days, but the plan doesn't say how a date picked in `DateTimeRangeInput` (a JavaScript `Date`, local time) becomes `yyyy-MM-dd`. The usual `toISOString()` shifts the date by a day for every user east of UTC. `new Date("yyyy-MM-dd")` shifts displayed dates for every user west of UTC. A marketer would compare the wrong periods without noticing.
2. **Misleading rate deltas.** The service's deltas are relative. A CTR moving from 30 % to 33 % would read "+10 %", which marketers read as ten points.

## 2. What Is Solid

- **Single command, aggregates only, index from the page.** There is no client-controlled index and no raw data, and the permission sits on both the page and the command.
- **Range limits come from server properties** (`Today`, `MaxDays`), and the service re-validates every request.
- **Latest-request-wins** reuses the tested query-tester helper.
- **The chart is accessible by design:** `role="img"` plus a summary, a table equivalent, and pattern plus legend for days that aren't final.
- **No new dependency.** amCharts was rejected with a stated reason (license and bundle size).

## 3. Enterprise Gaps Identified

1. Date conversion between JavaScript `Date` values and UTC day strings is unspecified (off-by-one in both directions).
2. Rate deltas are shown as relative changes.
3. A command failure (server or network) has no defined client handling, which could leave an unhandled rejection or a blank page.
4. On the host, an empty watermark marks every seeded day as not final. The host check could misjudge correct rendering, or miss a stuck rollup job.
5. Writing `from`/`to` to the URL with `history.replaceState` inside Kentico's admin single-page app may conflict with its router; this is unverified.
6. The Report command has no server-side throttling, so repeated 25-month requests cost about 0.3 s each.
7. The chart has no hover tooltips.

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Date conversion (gap 1) | AC-2, Task 2 | Days stay `yyyy-MM-dd` strings end to end: picked dates convert with their local calendar fields, never `toISOString`; strings are never parsed with `Date` for display; the UI states that days are UTC. Node tests run under New York and Tokyo time zones. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Rate deltas (gap 2) | AC-3, Task 2 | Counts get relative %, rates get percentage points, average position gets an absolute change. |
| 2 | Command failure (gap 3) | Design states, Task 2 | Rejections go to the `NotificationBarAlert` and the last good result stays on screen. |
| 3 | Watermark on the host (gap 4) | Task 3 | Read the watermark first; investigate the rollup job if it is stuck; record the finding. |
| 4 | URL vs admin router (gap 5) | AC-2 | Drop URL persistence (recorded as a deviation) if it conflicts, rather than working around the router. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | No server throttling (gap 6) | Only authenticated users with View can call it; the cost is bounded (volume-tested); recorded in KNOWN-LIMITATIONS. |
| 2 | No chart tooltips (gap 7) | The table view gives the exact values accessibly; recorded in KNOWN-LIMITATIONS. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - Registration tests (permissions, slug, template).
  - A DB test showing the page's numbers equal the service's.
  - Node tests for every formatting and date rule, including time zones.
  - A host check comparing the page with SQL sums.
- **Silent failures closed:** off-by-one periods, misread rate changes, blank pages on error.
- **Privacy:** no query text on this page (that comes in 08-02 and stays behind the surfacing threshold); aggregates only.
- **Accountability:** the page owns authorization for the report service, which closes the 07-01 note.

## 6. Final Release Bar

- **Must be true:**
  - UTC-day strings end to end, proven under two time zones.
  - Percentage-point deltas for rates.
  - Error state handled.
  - Host check recorded with the watermark state.
- **Remaining risks:**
  - No server throttling.
  - No chart tooltips.
  - UTC-only days (no site time zone).
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 4 strongly-recommended upgrades. Deferred 2 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
