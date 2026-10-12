---
phase: 13-enterprise-screens
plan: 02
subsystem: ui
tags: [kentico-admin, form-components, rule-editor, contact-groups, facets]

requires:
  - phase: 13-enterprise-screens
    provides: RuleListing.Of status rule and StatusText (13-01)
  - phase: 03-rules-engine
    provides: rule form components, GetFormItems index injection, concurrency token
  - phase: 05-preview-sandbox
    provides: contact-group loading (now ContactGroupAccess)
provides:
  - Rule edit regrouped into native FormCategory sections with the handoff copy
  - RuleStatusFormComponent (server-computed live status)
  - Context picker (contact groups by display name / Custom context) and facet picker (dimensions, values with counts / Other value)
  - ContactGroupAccess shared by the query tester and the rule editor
affects: [13-03, 13-04, v0.3 Relevance (rule editor changes)]

tech-stack:
  added: []
  patterns:
    - "Display-only form component computes state with a server command (one rule, no client copy)"
    - "Pickers never call onChange on mount or when data arrives; stored values round-trip unchanged"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/ContactGroupAccess.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/RuleStatusFormComponent.tsx
  modified:
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleEditModel.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Rules/RuleFormComponents.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/UIPages/RuleEditPages.cs, QueryTesterPage.cs
    - src/XperienceCommunity.Lucene.Suite.Admin/Client/src/rules/RuleConditionsFormComponent.tsx, RuleBoostsFormComponent.tsx, RulePinsFormComponent.tsx, RuleHidesFormComponent.tsx, state.ts, RuleListingTemplate.tsx, entry.tsx
    - src/XperienceCommunity.Lucene.Suite.Admin/PublicAPI.Unshipped.txt

key-decisions:
  - "Status via a server Status command running RuleListing.Of (13-01 had no client status rule; no duplicate logic)"
  - "FacetValues reuses SuiteFacet / query-tester Facet shapes; ContactGroups reuses QueryTesterContactGroups"
  - "Category subtext shown as tooltip (FormCategory has no subtext in 31.9.1)"

patterns-established:
  - "After an npm ci failure, restart the long-running admin dev server (it caches the broken module graph)"

duration: ~2h (implementation delegated; host checks, environment repair and findings by me)
completed: 2026-10-12T00:00:00Z
description: "Rule edit page matches the enterprise handoff on the native form: sections, live status, contact-group and facet pickers with counts; existing rules save byte-identical — two pre-existing defects found during host checks"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 13 Plan 02: Rule Edit Summary

**Marketers build a query + facet + contact-group condition by picking, not typing.** Facet values come with counts, contact groups appear by name, and existing rules save unchanged.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Sections and copy | Pass | General / Conditions / Actions / Custom data; handoff explanations; Custom data is a 4-row area and still validates JSON; revealed inputs labelled. Cosmetic: two section titles repeat as field labels |
| AC-2: Live status | Pass | Host: Active ↔ Disabled unsaved; future start → "Scheduled · starts … UTC"; server rule = listing rule; UTC-safe (tests under TZ=America/New_York) |
| AC-3: Contexts from contact groups | Pass (permission path test-only) | Host: group by display name, recipient list absent, `["CoffeeGrinders"]` stored, reopens by name |
| AC-4: Facets from the index | Pass (stale/failure paths test-only) | Host: ContentType values with counts (sum = index doc count), Other value… |
| AC-5: Nothing else changes | Pass | Unedited save byte-identical (SQL); errors on the right control; Kentico unsaved prompt present; pins/hides behaviour unchanged |
| AC-6: Trust boundary | Pass | Index from GetFormItems; FacetValues with rules/synonyms/contexts/analytics off; fail closed (tests) |

## Verification Results

- Build Debug + Release: 0 warnings.
- .NET 835 passed, 6 skipped; client 92/92, also under New York time; tsc clean. Re-run by me.
- Host checks 1–8 performed and recorded in APPROVAL-CHECKS #16.
- Test data removed: rule 505 and synonym #6. Rule 4 is restored identically.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Design choice | 1 | Status computed by a server command instead of a client helper (13-01 had none); stronger agreement with the listing |
| Shape reuse | 1 | FacetValues returns the existing SuiteFacet shape |
| Native limits | 2 | Category subtext as tooltip; no "Other…" for facet dimensions |
| Environment | 1 | node_modules lock incident (my shell cwd) → implementer restored; I restarted the dev server, which had cached the broken build |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Admin "Something went wrong" on every Suite page | Stale webpack dev server after the node_modules incident; restarted, compiled successfully |
| Second Save without reload → "changed by someone else" (rules **and** synonyms, pre-existing since 03-06/04-02) | Deferred issue + APPROVAL-CHECKS #16 with fix proposal (update the LoadedVersion item before GetClientProperties) |
| "Valid from/until (UTC)" labels vs browser-local DateTimeInput (pre-existing) | Deferred issue + APPROVAL-CHECKS #16 (relabel) |

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /xbyk-admin-ui:design-screen | ✓ | PLAN (other session) and APPLY start (me) |
| Admin UI design validation | ✓ | Both widths, overflow, labels/focus, states partly (failure bars test-only) |
| /dotnet:csharp-refactoring | ✓ | Loaded in session; ContactGroupAccess extraction gated by green query-tester tests |
| /kentico-web-development:content-retrieval | ✓ | Loaded in session |

## Next Phase Readiness

**Ready:** 13-03 (search reports) and 13-04 (query tester). ContactGroupAccess and the shared facet shapes are reusable.

**Concerns:**
- The second-save false conflict affects marketers today; it's worth a quick fix before the milestone review.
- The (UTC) labels.

**Blockers:** None

---
*Phase: 13-enterprise-screens, Plan: 02*
*Completed: 2026-10-12*
