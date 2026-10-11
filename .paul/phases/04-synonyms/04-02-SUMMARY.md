---
phase: 04-synonyms
plan: 02
subsystem: admin-ui
tags: [kentico-admin, form-components, react, synonyms, query-tester]

requires:
  - phase: 04-synonyms (04-01)
    provides: SynonymInfo, SynonymSerializer (errors by path), SynonymEngine.MatchKey, AppliedSynonyms/SynonymsFailed
  - phase: 03-rules-engine (03-06, 03-07)
    provides: native ModelEditPage + custom component pattern, permission gates, ownership, concurrency token, query tester rule display
provides:
  - SuiteSearchRequest.ApplySynonyms (default true)
  - Synonyms listing + create/edit pages behind Manage synonyms (native templates)
  - SynonymTermsFormComponent (tag/chip editor) with server Validate command
  - Query tester: Apply synonyms switch, "Synonyms applied (N)" with edit links, failure warning
affects: [05-preview-sandbox]

tech-stack:
  added: []
  patterns:
    - "Empty custom-field value + RequiredValidationRule: Kentico skips Required on load/change and skips invisible fields entirely (31.9.1 FormDataValidator), so errors appear only at save"
    - "Listing column formatter reads sibling columns from the IDataContainer row (hidden searchable columns)"
    - "Chip editor commits a typed term on blur (no silent loss)"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Synonyms/SynonymEditModel.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Synonyms/SynonymFormComponents.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Synonyms/SynonymSummaries.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SynonymListingPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SynonymEditPages.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/synonyms/SynonymTermsFormComponent.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/synonyms/state.ts
  modified:
    - src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchRequest.cs
    - src/XperienceCommunity.Lucene.Suite/Synonyms/SynonymSerializer.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx

key-decisions:
  - "No terms is stored in the form as an empty value (not []) so Kentico's Required rule reports it only at save"
  - "Input carries RequiredValidationRule; safe because invisible (two-way) fields are never validated"
  - "Add button stacked under the input (edit card is too narrow at ~790 px for a row layout)"

duration: ~1 session
completed: 2026-10-11T00:00:00Z
description: "Synonyms listing and editor (native pages + chip terms editor) behind Manage synonyms; query tester shows applied synonyms with links and can switch synonyms off"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 4 Plan 02: Synonyms admin + query tester Summary

**Marketers create, edit, check and delete an index's synonyms in the admin; the query tester shows which synonyms shaped a result and can compare with synonyms off.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1 session (2026-10-11) |
| Tasks | 2 auto + 1 checkpoint, all complete |
| Files | 9 created (5 C#, 3 client, 1 test), 13 modified |
| Tests | .NET 474 → 497 passed (3 opt-in skipped); client 22 → 29 |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Listing per index | Pass | Terms summary (⇔ / →), Kind, Enabled, Last modified; searchable on Terms + hidden Input; Create/Delete only with Manage synonyms; Delete gated + ownership; View read-only (checkpoint step 5) |
| AC-2: Editor | Pass | Radio Kind, Input only for one-way, chips, Enabled; save validation per field; create → edit page, edit → "Synonym saved."; concurrency/ownership/permission gates mirror rules (registration tests) |
| AC-3: Chip editor | Pass | Enter/Add/blur commit, duplicates by shared MatchKey vectors, 100-char limit, 20 terms, focus after remove, labelled list, read-only mode (checkpoint) |
| AC-4: Query tester | Pass | ApplySynonyms switch (stays enabled), applied list with links (one WhereIn), deleted → no link (unit), SynonymsFailed warning; ApplySynonyms=false skips even a failing provider (test) |
| AC-5: Design + live | Pass | 790/1440 px, keyboard pass by Claude; user created two-way + one-way in the UI, checked query tester and roles, deleted both (0 rows) |

## Deviations

| Type | Description | Impact |
|------|-------------|--------|
| Auto-fix (checkpoint) | New form showed "Must have 2–20 terms" on load, and "Required" on Input when switching to one-way → empty Terms = "" + RequiredValidationRule; Input RequiredValidationRule; field rules pass blank | Errors only after a failed save, as in native forms; verified in decompiled FormDataValidator |
| Auto-fix | Duplicate message (alert bar + field message) → alert only while the field is not invalid | Cleaner errors |
| Auto-fix | Add button overflowed the card at 790 px → stacked under the input | Layout holds at both widths |
| Design | useServerErrors (rules) sends only `{value}`; the chip editor uses a local hook sending Kind/Input too, leaving the shared rules helper untouched | Boundary kept |
| Design | npm Input exposes no maxLength/onKeyDown → maxLength set on the DOM element via inputRef; Enter via onKeyPress | Works; noted for kit drift |

## Recorded (KNOWN-LIMITATIONS)

- Same term in several synonyms: merged at search, no editor warning.
- Kind change on a saved synonym without confirmation (event log records it).

## Skill audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN and APPLY (before markup) |
| Admin UI design validation | ✓ | npm typings checked, 790/1440 px, keyboard, states, tokens |
| /dotnet:csharp-refactoring | ✓ | Loaded this session |
| /kentico-web-development:content-retrieval | ✓ | Loaded; ObjectQuery for listing/ownership/applied-synonym existence |

Skill audit: all required skills invoked ✓

## Next Phase Readiness

**Ready:** Phase 4 complete. Phase 5 preview can reuse ApplyRules/ApplySynonyms to show results side by side.

**Concerns:** npm Table rows still not keyboard-reachable (Phase 5 uses the same table); leftover XpSearch objects decision pending.

**Blockers:** None

---
*Phase: 04-synonyms, Plan: 02*
*Completed: 2026-10-11*
