---
phase: 03-rules-engine
plan: 01
subsystem: admin
tags: [ui-application, ui-permission, custom-permissions, page-extender, action-state-evaluator, validate-page, reflection-tests, role-checkpoint]

requires: [01-03 Search tuning section + query tester + client module]
provides:
  - SiteSearchApplication (identifier XperienceCommunity.Lucene.Suite.SiteSearch, slug site-search, Digital marketing, Icons.MagnifierPlus, SECTION_LAYOUT) with UIPermission VIEW + LuceneSuite.ManageRules / ManageSynonyms / ManageExperiments (public constants in SiteSearchPermissions)
  - SiteSearchIndexListingPage (slug indexes, read-only Lucene index listing; Entries via ILuceneClient.GetStatistics with warning fallback; row click opens the index section)
  - Chain app → listing → SearchTuningSectionPage → QueryTesterPage; nothing parented to Kentico Lucene pages; SearchTuningRootPage removed
  - Lucene index listing cogwheel = permission-aware shortcut into the Site Search section (hidden without Site Search → View, natively disabled as well)
affects: [03-04 rules admin UI (adds pages under SearchTuningSectionPage, enforces the custom permissions on its commands), Phases 4–5 admin pages (same section), packaging README (roles must be granted Site Search → View)]

tech-stack:
  added: []
  patterns: [Suite-owned UIApplication to declare custom permissions, cross-application shortcut via AddLink + AddLinkParameters.ActionStateEvaluator with a per-request Lazy permission check, ValidatePage index-exists check on every routed node, reflection tests pinning the page chain]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite.Admin/SiteSearchPermissions.cs, src/XperienceCommunity.Lucene.Suite.Admin/UIPages/{SiteSearchApplication.cs, SiteSearchIndexListingPage.cs}, tests/XperienceCommunity.Lucene.Suite.Tests/{AdminRegistrationTests.cs, SiteSearchAccessTests.cs}]
  modified: [src/XperienceCommunity.Lucene.Suite.Admin/UIPages/{IndexListingPageExtender.cs, SearchTuningSectionPage.cs, QueryTesterPage.cs}]

key-decisions:
  - "Custom permissions live on a Suite-owned application (option A): marketers get search tuning without Lucene index rights"
  - "IUITreeNodeUIPermissionEvaluator is internal in 31.9.1 → shortcut uses CMS.Membership.Internal.IApplicationPermissionEvaluator + IsAdministrator bypass (user option a; KNOWN-LIMITATIONS)"
  - "Permission enforcement left to Kentico's [UIEvaluatePermission(VIEW)] (CheckNodeAccessPermission runs before ValidatePage); ValidatePage keeps only the index-exists check, on both section and query tester, because Kentico validates only the requested node (user change, b362fa0)"
  - "AddLink<T> natively sets PagePermissionEvaluation = typeof(T) (disabled + tooltip); kept alongside the hide"

duration: APPLY → commits 0aa45f2 (13:52 -04:00) and b362fa0 (13:59 -04:00) on 2026-10-09
completed: 2026-10-09
---

# Phase 3 Plan 01: Placement & Permissions Summary

**Search tuning now lives in a Suite-owned "Site Search" application under Digital marketing with its own permissions (View + Manage rules / synonyms / experiments). A role with only Site Search → View can use the query tester; a role with only Lucene Search → View is refused on every Site Search URL and does not see the cogwheel shortcut. All ACs pass; this also closes the unverified 01-03 AC-5. One deviation: Kentico's cross-app evaluator is internal, so the shortcut uses a Kentico Internal-namespace API (logged).**

Evidence attribution: **[O]** = orchestrator-verified (commits, clean tree, `dotnet test -c Release` counts, browser checkpoint steps 1–4). **[U]** = user-verified (checkpoint steps 5–6). **[A]** = APPLY report (mutations, xbk scratch builds, decompile findings); not re-run at UNIFY because UNIFY does not start xbk.

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Site Search application with its own permissions | Pass | [O] Step 1: "Site Search" under Digital marketing. Step 4: Roles → Permissions → Site Search offers View, Manage rules, Manage synonyms, Manage experiments (inspected without saving). Constants LuceneSuite.ManageRules/ManageSynonyms/ManageExperiments in SiteSearchPermissions.cs; reflection-guarded (AC-5) |
| AC-2: Index listing and per-index section inside the new app | Pass | [O] /admin/site-search/indexes read-only (dancinggoat, MySearchIndexingStrategy, Standard, 38 entries), no create/delete/rebuild. Index row → /admin/site-search/indexes/1/query-tester (listing slug in the route — the "equivalent route Kentico generates" the AC allows). id 99999 → "This object doesn't exist." Old /admin/lucene/indexes/search-tuning/… no longer shows the query tester (falls back to the Lucene listing); SearchTuningRootPage removed |
| AC-3: Permissions are the Suite's, not Lucene's | Pass | [U] Step 5: "Suite viewer" (Site Search → View only) uses Site Search and runs the query tester. [U] Step 6: "Lucene only" (Lucene Search → View only) refused on the section and query tester URLs, no cogwheel. [A] Both pages [UIEvaluatePermission(VIEW)]; Search command Permission = VIEW; custom permissions reserved for 03-04 |
| AC-3b: Permission-aware shortcut and page validation | Pass | [A] AddLink<SearchTuningSectionPage>(AddLinkParameters) with ActionStateEvaluator → Visible from CanViewSiteSearch (IAuthenticatedUserAccessor; IsAdministrator bypass; else IApplicationPermissionEvaluator for the Site Search app + View), Lazy per request — unit test proves one evaluation across 3 rows. Native PagePermissionEvaluation = typeof(SearchTuningSectionPage) also disables + tooltips; no conflict, both kept. ValidatePage index-exists on section AND query tester (decompile: PageInvokerBase.GetPageAndExtenders validates only the requested node, for page loads and commands); child-route rejection unit-tested. Guard test Pages_EvaluateViewAndTheSearchCommandRequiresView. [U] Step 6 child URL refused |
| AC-4: Shortcut and regressions | Pass | [O] Step 3: icon-only cogwheel on the Lucene index list → /admin/site-search/indexes/1/query-tester. Step 2: empty query 38, "coffee" 28, Coffees facet 8 (aria-label "ProductFieldCategory: Coffees (8)"), 257 chars → "Query must not exceed 256 characters." — same as 01-03. [O] `dotnet test -c Release` 243 passed / 21 skipped / 264 total. [A] xbk scratch builds normal + SeparatedAdmin 0 errors / 279 warnings; Suite.Admin.dll absent in SeparatedAdmin |
| AC-5: Registration is test-guarded | Pass | [A] AdminRegistrationTests (5, reflection only): UIApplication identifier/slug/category, four UIPermission attributes, parent chain app → listing → section → query tester, no Suite page parented to a Kentico.Xperience.Lucene.Admin type, [UIEvaluatePermission(VIEW)] on both pages + Search command VIEW. Mutation "re-parent to Lucene" failed it |

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet test -c Release` | [O] 243 passed, 21 skipped, 264 total (254 → 264) |
| Mutations (each failed expected tests, reverted, clean diff) | [A] invert shortcut visibility; re-parent section to Lucene's IndexListingPage; drop query-tester index check (pre-refactor); drop [UIEvaluatePermission] (post-refactor) |
| xbk scratch builds (normal + SeparatedAdmin) | [A] 0 errors, 279 warnings each; Suite.Admin.dll absent in SeparatedAdmin |
| Browser checkpoint 1–4 | [O] Pass (above) |
| Role checkpoint 5–6 | [U] Pass (above) |
| Git | [O] 0aa45f2 + b362fa0 on main, tree clean, not pushed |

## What Was Built

| File | Purpose | Lines |
|------|---------|-------|
| src/XperienceCommunity.Lucene.Suite.Admin/SiteSearchPermissions.cs | Custom permission names + display names | 23 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SiteSearchApplication.cs | UIApplication + four UIPermissions | 31 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SiteSearchIndexListingPage.cs | Read-only index listing, Entries column, row click → section | 86 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SearchTuningSectionPage.cs | Re-parented under the listing; root page removed; ValidatePage index-exists | 48 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs | Parent chain unchanged in code; ValidatePage index-exists added | 156 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/IndexListingPageExtender.cs | Cogwheel retargeted to Site Search, permission-aware, per-request Lazy check | 49 |
| tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs | Reflection guards (5) | 73 |
| tests/XperienceCommunity.Lucene.Suite.Tests/SiteSearchAccessTests.cs | Shortcut visibility once per request; Validate_RequiresAnExistingIndex ×2; SectionAndQueryTester_BothRejectAMissingIndexOnTheirOwnRoute | 80 |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: Application, permissions, listing; move the section | Done | `0aa45f2` |
| 2: Retarget the Lucene shortcut + registration tests | Done (deviation 1; user change 2) | `0aa45f2`, `b362fa0` |
| 3: Checkpoint (human-verify) | Approved: steps 1–4 [O], 5–6 [U] | — |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Shortcut check uses `CMS.Membership.Internal.IApplicationPermissionEvaluator` (+ IsAdministrator bypass, IAuthenticatedUserAccessor) instead of `IUITreeNodeUIPermissionEvaluator` | The planned evaluator (and PermissionConfiguration, IUITreeAccessor) is internal in Kentico.Xperience.Admin 31.9.1; user approved option a | Unsupported public API; an upgrade would break the build, not silently. Native PagePermissionEvaluation still disables the action. KNOWN-LIMITATIONS entry |
| 2 | Explicit permission check removed from ValidatePage on both pages (b362fa0) | User change: Kentico's CheckNodeAccessPermission enforces [UIEvaluatePermission(VIEW)] before ValidatePage; ValidatePage keeps only index-exists, on both pages because only the requested node is validated | Access proven by guard test + mutation and by checkpoint step 6 |
| 3 | Route includes the listing slug: /admin/site-search/indexes/{id}/query-tester | Kentico generates the route from the listing page's slug | Allowed by AC-2 ("or the equivalent route") |
| 4 | Entries column computed via ILuceneClient.GetStatistics with warning fallback (implementation detail) | AC-2 asks for an entries column | Cost grows with index count (KNOWN-LIMITATIONS) |

## Key Patterns / Decisions

- Kentico custom permissions can only be declared on an application class; owning the application is what decouples Suite access from Lucene index rights.
- Every routed Suite node carries its own [UIEvaluatePermission] and its own index-exists ValidatePage — never rely on a parent node's checks (Kentico validates only the requested node, for loads and commands).
- Cross-application row actions: hide via ActionStateEvaluator with one evaluation per request; Kentico's native PagePermissionEvaluation remains as a second layer.
- KNOWN-LIMITATIONS appended (project-sidecar/docs/KNOWN-LIMITATIONS.md): Kentico-internal permission evaluator for the shortcut; Entries column via full statistics call.

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| /dotnet:csharp-refactoring | ✓ | Invoked in both APPLY runs |
| /kentico-web-development:content-retrieval | ○ | Not evidenced; the listing reads LuceneIndexItemInfo through Kentico's ListingPage ObjectType (no hand-written query) |
| Admin design kit (phase override) | n/a | No client/template changes |

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| Resolved: 01-03 AC-5 (permissions approved without observations) — real-role checks 5–6 passed | Closed |
| Resolved: permissions concern (custom permission vs. Lucene app UPDATE) — option A implemented and role-verified | Closed |
| Audit deferral 1: existing roles must be granted Site Search → View | README at packaging |
| Audit deferral 2: old /admin/lucene/indexes/search-tuning/… URLs no longer resolve (fallback to Lucene listing) | None (unreleased) |
| Custom permissions not yet enforced on any command | 03-04 rules admin UI |
| Admin UI not formally validated against the admin design components repo | Open concern in STATE |

## Next

Phase 3: 1 of 4 plans complete. Next: `/paul:plan` 03-02 Rule model + pure engine.

---
*Completed: 2026-10-09*
