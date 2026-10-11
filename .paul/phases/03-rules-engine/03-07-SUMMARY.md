---
phase: 03-rules-engine
plan: 07
subsystem: admin-ui
tags: [kentico-admin, form-components, lucene, react, query-tester]

requires:
  - phase: 03-rules-engine (03-05)
    provides: PinnedByRuleId on hits; pins in SearchExecutor
  - phase: 03-rules-engine (03-06)
    provides: native rule listing/editor, StoredRule mapper, server-only validation rules, useServerErrors/useTouched/useFocusTargets
provides:
  - SuiteItemLookup (core, internal) — ItemGuid → stored title / content type, one TermsFilter query, cap 350
  - Pins and Hides custom form components with a shared index-search item picker
  - Rule editor fields Pinned items / "Pinned items must match the applied filters" / Hidden items
  - Query tester: Apply rules switch, Fired rules region with edit links and custom data, Pinned column + "Pinned by", RulesFailed warning
affects: [04-synonyms admin pages, 05-preview-sandbox (side-by-side with/without rules)]

tech-stack:
  added: []
  patterns:
    - "Form-component commands get page context by overriding GetFormItems (Kentico 31.9.1 EditPageBase.ComponentCommand calls it) — never from the client"
    - "Cross-field client logic reads FormComponentProps.fieldValues (all form values), key matched case-insensitively"
    - "SidePanel focus: render content only while visible and focus via a mount callback ref (rAF focus races the panel)"
    - "IPageLinkGenerator.GetPath is admin-root-relative; client prefixes the admin base taken from location.pathname"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteItemLookup.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleItemFormComponents.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/ItemPicker.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/RulePinsFormComponent.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/RuleHidesFormComponent.tsx
    - tests/XperienceCommunity.Lucene.Suite.Tests/SuiteItemLookupTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleEditModel.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleSectionValidator.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/RuleEditPages.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx

key-decisions:
  - "ActionsJson builds all actions from the form (no merge with stored actions): the serializer disallows unknown members, so there is nothing to preserve"
  - "Index for component commands set on the components in RuleEditPageBase.GetFormItems (verified in decompiled 31.9.1 and live)"
  - "Pinned/hidden exclusion in the picker via fieldValues, no extra server round trip"
  - "Apply rules switch stays enabled during a search (disabling it dropped keyboard focus); latestRequest drops stale responses"

patterns-established:
  - "Pattern: page-bound context for form-component commands via GetFormItems override"
  - "Pattern: mount-ref focus inside SidePanel content"

duration: ~1 session
completed: 2026-10-11T00:00:00Z
description: "Pins and hides editable through an index-search picker; query tester shows fired rules, pinned hits and rule failures, with an Apply rules switch"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 3 Plan 07: Pins/hides editor + query tester rule display Summary

**Marketers pin and hide items by searching the index from the rule editor, and the query tester shows which rules fired, what they pinned, their custom data, and can switch rules off for comparison.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1 session (2026-10-11) |
| Tasks | 3 auto + 1 checkpoint, all complete |
| Files | 6 created, 16 modified |
| Tests | .NET 423 → 438 passed (2 skipped); client 14 → 22 |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Pins and hides editable through the picker | Pass | Fields after Boosts; picker searches with rules/analytics off, 20 results, title + content type; next free positions; focus returns to opener; added items checked+disabled, other list's items disabled ("Pinned by this rule"); "Not in this index" tag; field-level conflict rule on Hides; View-only read-only (checkpoint step 6) |
| AC-2: Lookup safe and bounded | Pass | One TermsFilter query, first doc per ItemGuid, missing omitted, cap 350; Search de-duplicates by ItemGuid (live: T-shirts listed once in the multilingual index); unresolved index / cap / exceptions → error result (tests) |
| AC-3: Query tester shows what rules did | Pass | Switch re-runs search; Fired rules (N) with links and JSON custom data; names in one WhereIn query; deleted rule → "Rule {id}" without link; Pinned column + "Pinned by"; RulesFailed warning; rules off → no region (checkpoint steps 3–4) |
| AC-4: Design validation + carried gaps | Pass with one gap | Design/keyboard checked at ~790 and 1440 px for both pages (closes 03-02 1440 px gap); 03-02 rapid-click re-test run; roles, listing keyboard pass approved at checkpoint. **Gap:** stored Valid-from SQL check not run — the checkpoint rule was deleted before Claude checked |

## Verification Results

- `dotnet build` (solution): 0 warnings, 0 errors
- `dotnet test` with comm_projects_test: Passed 438, Skipped 2, Failed 0
- Client: `npm run typecheck` clean; `npm test` 22/22; `npm run build` succeeded
- Live (CommunityProjects host, DancingGoatSample): picker search/tick/add by keyboard, cross-list exclusion, Escape focus return, switch keyboard toggle, layouts at 790/1440 px
- comm_projects: 0 rules after checkpoint (test rule deleted)

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed (checkpoint) | 5 | UX/a11y fixes, no scope change |
| Design changes | 2 | Simpler than planned |
| Gaps | 1 | Valid-from SQL check |

**Design changes:**
1. `RuleEditMapping.ActionsJson(model)` builds pins, hides, pinsRespectFilters, boosts and userData from the form only; "keep unknown stored properties" dropped — `RuleSerializer` disallows unmapped members, so none can exist.
2. Conflict message lists ItemGuids, not titles (server has no titles without a lookup; the picker prevents the case in normal use).

**Auto-fixed during checkpoint:**
1. Picker search field focus raced the SidePanel (rAF) → content renders only while visible, focused by a mount callback ref.
2. Pin row at 790 px squeezed Position into the title → responsive columns (`colsLg`).
3. "Apply rules" label clipped at 790 px → switch stacks under the query below `lg`.
4. Switch `disabled={inProgress}` dropped keyboard focus to body → never disabled.
5. Edit links: `GetPath` is admin-root-relative → `adminHref` prefixes the admin base from the current path (tested).

**Issue:** `npx prettier` downloaded prettier (not a project dependency) and reformatted `ItemPicker.tsx` at 80 columns; re-wrapped to the project's 140. No other files touched.

## Findings recorded (KNOWN-LIMITATIONS)

- Picker shows the first 20 results, no paging.
- Titles from the first configured search field; multilingual title from the lowest doc id (may be another language).
- Query tester: two filter removals inside one React render keep one (stale closure; scripted clicks only, tags/results stay consistent).

## Skill audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN, and re-invoked in APPLY before markup (npm typings checked) |
| Admin UI design validation | ✓ | 790/1440 px both pages, keyboard, states, tokens |
| /dotnet:csharp-refactoring | ✓ | Loaded for Task 1 |
| /kentico-web-development:content-retrieval | ✓ | Loaded; rule names via ObjectQuery (non-content), index reads via Lucene |

Skill audit: all required skills invoked ✓

## Next Phase Readiness

**Ready:** Phase 3 complete — every rule action can be created and checked in the admin. Patterns (component-command context, fieldValues, SidePanel focus) carry to Phase 4 Synonyms admin.

**Concerns:**
- Valid-from round trip on the host still unverified by SQL (unit test covers the mapping).
- npm Table rows still not keyboard-reachable in the query tester (03-02 limitation stands).
- Leftover XpSearch DB objects decision still pending.

**Blockers:** None

---
*Phase: 03-rules-engine, Plan: 07*
*Completed: 2026-10-11*
