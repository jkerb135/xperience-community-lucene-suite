---
phase: 10-custom-activities
plan: 01
subsystem: api
tags: [kentico-activities, contact-management, privacy, events-contract]

requires:
  - phase: 02-event-capture
    provides: SearchCaptureService, ISearchInteractionLogger, AnalyticsRequestFilter, installer
  - phase: 06-widgets-package
    provides: events endpoint, click beacon, results view
provides:
  - Custom activity types lucenesuite_search / _search_noresults / _resultclick (installer v7, disabled)
  - SearchActivityLogger (Core) for submitted searches, 0-hit searches and result clicks
  - Events contract v2 (optional click query/title, activity-only)
  - Public SuiteActivityTypes constants
affects: [11-public-api-audit, v0.3 A/B (activity data per variant is out of scope)]

tech-stack:
  added: []
  patterns:
    - "Suite-written Kentico activities always set ActivityURL/Referrer explicitly without query strings (Kentico MVC default = raw Referer)"
    - "Marketer opt-in via native type Enabled flag; installer creates once, never updates"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Activities/SuiteActivityTypes.cs
    - src/XperienceCommunity.Lucene.Suite/Activities/SearchActivityLogger.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/SearchActivityLoggerTests.cs
    - docs/activities.md
  modified:
    - src/XperienceCommunity.Lucene.Suite/Analytics/AnalyticsModuleInstaller.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/SearchCaptureService.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/ISearchInteractionLogger.cs
    - src/XperienceCommunity.Lucene.Suite/Analytics/SearchInteractionLogger.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Events/SuiteEventsEndpoints.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/wwwroot/lucene-suite-results.js
    - src/XperienceCommunity.Lucene.Suite.Widgets/Views/Shared/Components/LuceneSuiteResults/Default.cshtml
    - docs/analytics-client-contract.md

key-decisions:
  - "User: types installed disabled; no-results in addition to search performed; click contract v2 with browser-supplied query/title; Core, submitted searches only"
  - "Click activity URL = results page (scrubbed Referer), not the beacon endpoint"
  - "The analytics request filter (bots, prefetch, disabled/untracked) also gates activities, for clicks too"

patterns-established:
  - "Pure tests of Kentico-dependent loggers via Func seams (Info objects cannot be built without Kentico); real lookups exercised by one DB test"

duration: ~90min (Tasks 1–2 delegated to an implementer agent; reviewed, adjusted, re-verified; host E2E by me)
completed: 2026-10-12T00:00:00Z
description: "Tracked contacts' submitted searches, zero-result searches and result clicks become Kentico custom activities (opt-in per type, consent by Kentico), with scrubbed text and query-free URLs, verified end to end on the host"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 10 Plan 01: Search Activities Summary

**Marketers can enable "Search performed", "Search without results" and "Search result clicked" in Contact management and segment contacts by what they search for.** Kentico decides consent, and Suite analytics stay identity-free.

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Types installed disabled (v7) | Pass | Host and test DB: three custom types, Enabled = 0; a marketer-enabled, renamed type survives re-runs (DB test) |
| AC-2: Search activities | Pass | Host: exact titles, values and comment; 0 hits adds the second activity; continuation and browse log nothing. Unit: untracked, filtered, no HTTP context, logger throws → search unchanged. The URL never carries the query (`?q=jane@example.com` test; host SQL 0 rows for "jane") |
| AC-3: Click contract v2 + click activity | Pass | v1/v2/malformed bodies → 202; text never in SearchInteraction; host click title/position/value plus WebPageItemGUID matching Hario V60; control characters stripped; URL = results page without query |
| AC-4: No cost when off, no regressions | Pass | Disabled path: one cached provider check and no lookup. Admin toggle on and off took effect without a restart. Build 0 warnings; .NET 739 passed / 5 skipped; JS 45/45; rules-overhead test passed alone; docs written |

## Verification Results

- `dotnet build XperienceCommunity.Lucene.Suite.slnx`: 0 warnings. Full `dotnet test` (comm_projects_test, now v7): 739 passed, 0 failed, 5 skipped. Widgets `npm test`: 45/45.
- Host comm_projects: v7.
  - Disabled run: SearchEvent 20→21, Interaction 2→3, 0 Suite activities.
  - Enabled run: 4 expected Suite activities (IDs 5019, 5020, 5022, 5025), no "jane" in any column.
  - Toggle off: 0 further activities.
- Host restored: types disabled, the 4 activities deleted (0 Suite activities remain). The CommunityProjects host build has 60 warnings, all in host code (Program.cs, a tag helper), none in Suite projects.

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Spec adjustment | 1 | Click ActivityURL = scrubbed Referer (the results page) instead of the request URL (the beacon endpoint); test added |
| Implementation choice | 4 | Func seams for tests; text cleaning inside the guarded activity call; the filter gates click activities; over-long endpoint text dropped (not trimmed), as the plan's tests specify |
| Host procedure | 1 | No consent action was needed: the pane was already a tracked contact, so the plan's "ask before granting consent" step never triggered |

- **Kentico behaviour confirmed by decompiling 31.9.1:**
  - In MVC, `ActivityMvcUrlService.GetActivityUrl()` returns the raw Referer, and Kentico fills ActivityURL only when it is null. The audit's must-have was therefore a real leak, now closed.
  - The `IInfoProvider<ActivityTypeInfo>.Get(codeName)` extension uses Kentico's eager name cache, the same call Kentico's own CustomActivityLogger makes.
- **New limitations** (KNOWN-LIMITATIONS):
  - `WebPageItemInfo` is an internal Kentico type;
  - LogClick's new optional parameters break third-party implementers;
  - click text is forgeable for the forger's own contact;
  - Kentico's page-visit URL keeps the raw `q`.

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Loaded in this session (feature work, no refactor needed) |
| /kentico-web-development:content-retrieval | ✓ | Loaded in this session; ObjectQuery for the type and web page checks |
| /kentico-web-development:page-builder-widgets | ○ (optional) | Results view change is one attribute |

## Next Phase Readiness

**Ready:**
- Phase 11 (Public API Audit) has a concrete list:
  - `SuiteActivityTypes`;
  - `ISearchInteractionLogger.LogClick` (consider a request record);
  - `BlockSuggestion*` DTOs;
  - `MapLuceneSuite*` namespaces;
  - an `ISuiteSuggestions` review.

**Concerns:**
- APPROVAL-CHECKS #4–#13 pending milestone review, including two open questions: #12 (audit trail) and #13 (click text).

**Blockers:** None

---
*Phase: 10-custom-activities, Plan: 01*
*Completed: 2026-10-12*
