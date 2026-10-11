---
phase: 03-rules-engine
plan: 06
subsystem: admin
tags: [kentico-admin, listingpage, modeleditpage, form-components, validation-rules, react]

requires:
  - phase: 03-rules-engine (03-03)
    provides: RuleSerializer (strict parse, JSON paths), RuleInfo, RuleLimits
  - phase: 03-rules-engine (03-04)
    provides: rules cache invalidation on RuleInfo change (edits apply without restart)
  - phase: 03-rules-engine (03-01)
    provides: Site Search app, SiteSearchPermissions.MANAGE_RULES, section/ValidatePage pattern
provides:
  - Rules listing per index (native ListingPage) with summaries, create, delete (Manage rules)
  - Rule create/edit (native ModelEditPage) — details, conditions, boosts, custom data; pins/hides preserved
  - Conditions and Boosts custom form components with live server validation by JSON path
  - Server-only validation rules (RuleJsonValidationRule, RuleValidityValidationRule) + always-pass client rule
affects: [03-07 pins/hides editor + query tester rule display, Phase 4 synonyms admin (same patterns)]

tech-stack:
  added: []
  patterns:
    - "Override EditPageBase.Submit/Change with [PageCommand(Permission = <app permission>)] — base requires system Update, which custom apps may not declare"
    - "Child-page links (AddEditRowAction/AddLink) pass ancestor PageParameterValues explicitly"
    - "Server-only Kentico validation rule = custom ValidationRule + client rule that always passes"
    - "Pure mapping on a StoredRule record; RuleInfo setters need a running Kentico"
    - "Per-control server errors shown after blur or a failed save (useTouched)"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/RuleListingPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/RuleEditPages.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleEditModel.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleSectionValidator.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleFormComponents.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleSummaries.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/*
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleEditModelTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/entry.tsx
    - tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleProviderDatabaseTests.cs

key-decisions:
  - "Create uses ModelEditPage too (native CreatePage requires system Create, not declared by Site search)"
  - "Submit and Change re-gated on Manage rules (reflection: EditPageBase.Submit/Change require Update in 31.9.1)"
  - "Concurrency token carried by a hidden custom form component (RuleHidden)"
  - "Per-control errors only after blur or failed save"

duration: ~3h
completed: 2026-10-11T04:00:00Z
description: "Rules listing and editor in Site Search — native listing/edit pages, server-validated conditions and boosts components, Manage rules enforced server-side, pins/hides preserved, optimistic concurrency"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 3 Plan 06: Rules Admin UI Summary

**Marketers can list, create, edit and delete an index's rules in Site search → index → Rules: details, conditions, boosts/buries and custom data, with errors shown on the right controls. Saving and deleting need Manage rules on the server; rules change search immediately. Pins/hides editing and the query tester rule display are 03-07.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~3 h |
| Tasks | 2 auto PASS + 1 checkpoint approved (role and date steps recorded as gaps) |
| Files | 11 created, 3 modified (+ KNOWN-LIMITATIONS) |
| Tests | 423 .NET with DB (was 394), 2 opt-in skipped; 14 client node tests (was 7) |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Listing per index, permission-aware | Pass, with gaps | Listing, summaries, native empty state, create/delete gated (registration tests pin Manage rules on Create/Submit/Change/Delete); ownership DB test. Gap: live View-only and View + Manage roles not exercised (checkpoint 6/6b) |
| AC-2: Create/edit with native fields | Pass, with gap | Mapping, pins/hides preserved from fresh row, server validation by path, normalized storage, concurrency, edits apply without restart (checkpoint 2–5). Gap: browser time-zone handling of the native date input (6c) not verified on the host |
| AC-3: Conditions and boosts components | Pass | Add/remove with limits, live server validation per control, unmatched errors in a notification bar, focus to first input on add / previous remove or Add on remove, labels, tokens only |
| AC-4: Design validation | Pass, with gap | Native listing/edit structure, npm-exported kit components (props checked in d.ts and $DS), ~790 and 1440 px checked, keyboard pass in the editor. Gap: keyboard pass through listing rows not run (listing was empty during the pass) |
| Checkpoint | Approved | Steps 1–5 and 7 by the user; 6/6b/6c recorded as verification gaps |

## Verification Results

- `dotnet build XperienceCommunity.Lucene.Suite.slnx` → 0 warnings; `dotnet test` with comm_projects_test → 423 passed, 2 skipped.
- Client: `npm run typecheck` clean; `npm test` 14 passed; `npm run build` OK.
- Browser pane (host + dev server): listing empty state, create form, components, focus and error timing, ~790 and 1440 px; console errors only from Kentico scripts.
- Host log: only the pre-existing XpSearch macro errors.

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `UIPages/RuleListingPage.cs` (101) | Created | Native listing, summaries, index filter, create/delete (Manage rules), RuleOwnership |
| `UIPages/RuleEditPages.cs` (174) | Created | Create/edit on ModelEditPage; Submit/Change gates; ownership, fresh re-read, concurrency, save/navigate |
| `Rules/RuleEditModel.cs` (199) | Created | Form model (native components + custom), StoredRule, pure mapping/validate/apply |
| `Rules/RuleSectionValidator.cs` (79) | Created | Per-field RuleSerializer validation |
| `Rules/RuleFormComponents.cs` (158) | Created | Conditions/Boosts/Hidden components, Validate commands, server-only validation rules |
| `Rules/RuleSummaries.cs` (104) | Created | Listing summaries |
| `Client/src/rules/*` (656) | Created | state (pure, tested), shared hooks, two components |
| `Client/src/entry.tsx` | Modified | Exports components, hidden component, client rule |
| `RuleEditModelTests.cs` (142), `AdminRegistrationTests.cs`, `RuleProviderDatabaseTests.cs` | Created/extended | Mapping, validation, summaries, concurrency, UTC; registration and permission gates; ownership |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN run (outline in plan) and APPLY re-run before markup |
| Admin UI design validation | ✓ | npm exports + $DS props, tokens, states, focus, ~790/1440 px; listing-row keyboard pass not run (empty listing) |
| /dotnet:csharp-refactoring | ✓ | Loaded this session |
| /kentico-web-development:content-retrieval | ✓ | Loaded this session; ListingPage/IInfoProvider use |

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 4 | Essential (permissions, 500, premature errors, testability) |
| Additions | 2 | Hidden component, client always-pass rule |
| Verification gaps | 3 | Recorded in STATE |

1. **Native command permissions** (Task 1): reflection showed `EditPageBase.Submit` and `Change` require system Update and the native create page requires Create — neither declared by Site search. Both commands overridden with Manage rules; create implemented as a ModelEditPage. Audit must-have 1 confirmed.
2. **Listing 500** (checkpoint prep): `AddEditRowAction`/`AddLink` threw "Parameter for SearchTuningSectionPage is missing"; pass the section's PageParameterValues explicitly.
3. **Premature errors** (design pass): a new condition was flagged before typing; per-control errors now appear after blur or a failed save.
4. **Testability** (Task 1): `RuleInfo` setters need Kentico's container; the mapper works on a `StoredRule` record with `From`/`CopyTo` adapters.
5. **Additions:** `RuleHiddenFormComponent` carries the concurrency token (ModelEditPage round-trips only component-backed fields); `ServerValidatedValidationRule` client rule (ClientRuleName is abstract).

### Verification gaps

- Role checks (6, 6b): View-only read-only behaviour and a View + Manage rules role not exercised live; permission gates pinned by registration tests.
- Date (6c): native DateTimeInput time-zone conversion not verified on the host; mapping proven by unit test.
- Listing keyboard pass with rows not run.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Bash heredoc failures on large edits | Edits via script files |
| Browser pane required sign-in | User signed in |
| Screenshot coordinates vs emulated viewport | Element refs / DOM clicks |
| User's host held the bin folder | User stopped it |

## Next Phase Readiness

**Ready:**
- 03-07 extends the editor (pins/hides with the index-search picker; respect-filters checkbox) and the query tester (FiredRuleIds, UserData, RulesFailed, PinnedByRuleId already in the response).

**Concerns:**
- 03-07 checkpoint should close the role (6/6b) and date (6c) gaps and the listing keyboard pass.
- "Featured content" action deferred (user: not now).

**Blockers:** None.

---
*Phase: 03-rules-engine, Plan: 06 · Completed: 2026-10-11*
