# Enterprise Plan Audit Report

**Plan:** .paul/phases/13-enterprise-screens/13-05-PLAN.md
**Audited:** 2026-10-13
**Verdict:** Conditionally acceptable → acceptable after the upgrades below

---

## 1. Executive Verdict

Conditionally acceptable. The design is the right size:
- one Core object-event handler covers admin and API writes alike;
- Kentico's own `CMSActionContext.LogEvents` flag mutes bulk loops instead of a custom flag;
- the label change is text only.

The plan as written would have overclaimed. Its success criterion promised that every change "can be traced to a person and a time". The trail lives in Kentico's event log, whose size is a site setting (`CMSLogSize`); the host keeps 1,000 rows, about two days. Documenting an audit trail without stating its retention is a compliance misstatement. This is the release-blocking item.

## 2. What Is Solid

- **The handler lives in Core,** so changes through `IInfoProvider` from host code are logged too, not only admin pages.
- **No automated writers exist** (verified: Core only reads Rule/Synonym/Blocked through providers), so the trail is people's changes, not system noise.
- **The bulk summary is kept and per-item entries are muted,** so one action is one entry, and the 1,000-row log isn't flushed by a 500-rule bulk delete.
- **Logging is fail-safe:** an exception while logging never fails the save.
- **The scope is tight:** no TYPEINFO, schema or installer changes; analytics types are untouched.

## 3. Enterprise Gaps Identified

1. Retention of the trail is bounded by the site's event-log size and was not stated (overclaim).
2. Description size was unbounded: a synonym can hold 20 terms of up to 100 characters each.
3. Behaviour without an authenticated user (API, background) was unspecified.
4. Test noise: DB fixtures that write many rules one by one would flood the test DB's event log and slow the suite.
5. An unedited Save logs an UPDATE (Kentico raises Update even without changes).
6. If a save ran inside a transaction that later rolled back, the log could claim a change that didn't persist. No Suite save uses an outer transaction today; Kentico's own object logging has the same property.
7. The entry says that something changed, not what (no diff).

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Retention overclaim (gap 1) | AC-4, success criteria | Docs and KNOWN-LIMITATIONS state that retention follows CMSLogSize (host: 1,000 rows, about 2 days) and that sites needing a longer history raise it; the success criterion is reworded |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Unbounded description (gap 2) | AC-1, Task 1, test | One line, max 1,000 characters with "…", no rule JSON; a long-synonym test case |
| 2 | No-user context (gap 3) | AC-1, Task 1 | Still logged, with no user, and never fails |
| 3 | Test noise (gap 4) | Task 1 | Wrap bulk fixture writes in LogEvents = false if they write per item; compare suite duration |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | UPDATE on an unedited save (gap 5) | It still records who saved and when; harmless for an audit |
| 2 | Rollback mismatch (gap 6) | No outer transactions around Suite saves; the same as Kentico's own object logging |
| 3 | No field-level diff (gap 7) | Out of scope (plan boundary: no version history); the name/text plus time identifies the change |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - a DB test (one entry per change, muted under LogEvents = false, capped description);
  - host CMS_EventLog rows with the user;
  - one row for a bulk action.
- **Silent failures:** a logging failure becomes an exception entry, never a lost save. A lost log entry is possible only when the event log itself fails, which Kentico logs.
- **Post-incident reconstruction:** who, when, which object, which index, and the display text, for as long as the event log retains it (documented).

## 6. Final Release Bar

- **Must be true:**
  - one entry per change, with the user;
  - bulk = one entry;
  - retention honestly documented;
  - local-time labels shipped.
- **Remaining risk:** short retention on default-sized logs (now documented); no diffs.
- I would sign off once the plan is implemented as amended.

---

**Summary:** Applied 1 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
