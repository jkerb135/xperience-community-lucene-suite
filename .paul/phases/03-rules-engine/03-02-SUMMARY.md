---
phase: 03-rules-engine
plan: 02
subsystem: admin
tags: [query-tester, design-kit, side-panel, filter-panel, abort-controller, latest-request-wins, node-test, a11y, design-validation]

requires: [03-01 Site Search app + query tester placement]
provides:
  - Pure, node-tested query-tester state helpers (state.ts): mergeSelectedIntoFacets, humanizeFieldName, visibleValues, latestRequest (abort + latest-wins)
  - Content-hub-style Filters SidePanel (draft; Clear all / Cancel / Apply) + applied-filters bar (Tags + Clear all), composed from exported npm components
  - Kit states: Table skeleton while loading, QuickTip empty state with Clear filters, warning Callout on failed search (previous results kept), inline-only validation Callout
  - Hit detail in SidePanel; heading roles; humanized headers; display title first; title without index name
  - C# polish: "Site search" (identifier/slug/permission names unchanged, pinned by literal-asserting tests), Icons.Magnifier on app + query tester, empty-listing QuickTip, Entries via column formatter
  - SPECIAL-FLOWS: Admin UI design validation is REQUIRED for any admin UI change
affects: [03-05 rules admin UI (reuses filter panel, side panel, table states; inherits non-focusable npm Table rows), Phases 4–5 admin pages (design validation required)]

tech-stack:
  added: []
  patterns: [Client state logic as erasable-TS pure functions tested with node --test (no framework), executeCommand + AbortController latest-request-wins, draft/apply filter panel from exported SidePanel + Checkbox + Button, focus return to the opener (or a tabIndex=-1 wrapper when the kit element can't take focus), reflection tests assert role-grant identifiers as literals]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/{state.ts, state.test.ts}]
  modified: [src/XperienceCommunity.Lucene.Suite.Admin/Client/{package.json, tsconfig.json, src/query-tester/QueryTesterTemplate.tsx}, src/XperienceCommunity.Lucene.Suite.Admin/UIPages/{QueryTesterPage.cs, SiteSearchApplication.cs, SiteSearchIndexListingPage.cs}, tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs, .paul/SPECIAL-FLOWS.md]

key-decisions:
  - "Checkpoint (user): facets move from a left column into a content-hub-style Filters SidePanel with a draft and Clear all / Cancel / Apply; kit FilterPanel / FilterStatusIndicator are not exported in admin-components 31.9.1, so the panel and bar are composed from exported components (no kit source copied)"
  - "The red 'Applied filters:' label came from the admin shell root rule `.app___dEm8E { color: red }` on unstyled text; fixed with --color-text-low-emphasis, not an override of the shell"
  - "npm Table row buttons are display:contents and can't take focus → detail-panel close focuses the table wrapper; rows are not keyboard-reachable (KNOWN-LIMITATIONS; carried to 03-05)"
  - "Application identifier, slug and permission names asserted as string literals in AdminRegistrationTests (audit must-have: role grants are stored under them)"

duration: APPLY → 58c3126 (2026-10-09 16:26 -04:00); checkpoint fixes f014963 (2026-10-10 07:52 -04:00)
completed: 2026-10-10
---

# Phase 3 Plan 02: Query Tester Design & State Fixes Summary

**The query tester can no longer get a filter stuck, show out-of-order results or blank out on a failed call, and both Site Search pages now follow the kit's components, tokens and states. At the checkpoint the user asked for a content-hub-style filter panel; it replaced the facet column in f014963 and the checkpoint was approved on 2026-10-10. Two gaps remain: the 1440 px visual check and the rapid-click re-test on the new controls were not run, and hit rows are not keyboard-reachable (npm Table limitation).**

Evidence attribution: **[O]** = orchestrator-verified in the browser pane (~800 px content width) and on git. **[U]** = user decision/approval. **[A]** = APPLY report (typecheck, node tests, mutations, builds, dotnet test, xbk scratch builds); not re-run at UNIFY.

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Facet selection can never get stuck | Pass | [A] mergeSelectedIntoFacets unit-tested; mutation "drop the merge" failed a test. [O] Step 2 passed; after fixes: zero-hit query keeps ticked values at (0) in the Filters panel; applied-filters bar lists every value, Tag × and Clear all apply immediately. Bar built from exported Tag + Button (FilterStatusIndicator not exported) |
| AC-2: Request handling | Pass (partial re-test) | [A] latestRequest (AbortController + latest-wins) unit-tested; mutation "apply every response" failed a test. Failed/undefined executeCommand keeps previous results + warning Callout. AddErrorMessage removed → one inline Callout (step 4 [O]). [O] Step 3 rapid facet clicks passed on the pre-fix layout; **not re-run on the Filters panel / Apply / bar controls** (gap). Pagination has no `disabled` prop in 31.9.1 — relies on abort (KNOWN-LIMITATIONS) |
| AC-3: Layout at laptop widths | Pass at ~800 px; 1440 px unverified | [O] Step 1 at ~800 px: results visible in the first screen (table now full width, facets in the panel), kit SearchInput pattern, Enter submits, explanationText below. Humanized facet headings and column headers; stats spacing fixed (kit Row inline margin-top cancelled Stack spacing). Show more/less over the first 5 returned values (server cap recorded in KNOWN-LIMITATIONS). **1440 px not checked — the pane is ~800 px wide** (gap) |
| AC-4: Kit states and detail view | Pass with deviation | [A]/[O] Table skeleton (rows = undefined) while loading; zero hits → QuickTip with Clear filters. Row click → SidePanel with stored fields as plain text, focus moves in; Escape/close returns focus to the **table wrapper**, not the row (deviation 3) |
| AC-5: Accessibility and polish | Pass | [A] role="heading" + aria-level wrappers (kit Headline is a div); title without index name; display title first, ItemGuid in detail. C#: "Site search", Icons.Magnifier on app + query tester, empty-listing QuickTip, Entries via formatter keyed by index name (not caption). Guard tests QueryTester_HasAMenuIcon, IndexListing_FormatsEntriesByIndexNameAndHasAnEmptyStateCallout. [O] Step 6 passed |
| AC-6: Tests and design validation in place | Pass | [A] `npm run typecheck` clean; `node --test` 7/7; Release 0/0; `dotnet test` 245 passed / 21 skipped / 0 failed; locked restore ok; xbk scratch builds 0 errors / 279 warnings both modes; xbk/packages.lock.json restored to original bytes. SPECIAL-FLOWS lists Admin UI design validation as required (58c3126) |

## Verification Results

| Check | Result |
|-------|--------|
| `npm run typecheck` | [A] clean |
| `npm test` (node --test) | [A] 7/7 |
| Mutations (latest-wins, selected merge) | [A] each caught by a failing test; reverted, diff clean |
| Release build | [A] 0 warnings / 0 errors; bundle loads (import resolution unchanged for webpack/babel) |
| `dotnet test` | [A] 245 passed, 21 skipped, 0 failed (243 → 245) |
| Locked-mode restore | [A] ok |
| xbk scratch builds (normal + SeparatedAdmin) | [A] 0 errors, 279 warnings each; xbk/packages.lock.json restored to original bytes |
| Browser checkpoint (pre-fix) | [O] steps 1–4 and 6 pass; step 5 failed (focus not returned) + red "Applied filters:" label |
| Browser re-check (post-fix, ~800 px) | [O] apply 2 filters → 2 hits + 2 Tags, focus on Filter button; label rgb(82, 82, 82); zero-hit query keeps ticked values at (0) + Clear filters QuickTip; Escape closes the panel and returns focus |
| Checkpoint | [U] Approved 2026-10-10 |
| Git | [O] 58c3126 + f014963 on main, not pushed |

## What Was Built

| File | Purpose | Lines |
|------|---------|-------|
| src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/state.ts | Pure helpers: selected merge, humanize, visible values, latest-request sequencer | 69 |
| src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/state.test.ts | node --test coverage of the helpers | 63 |
| src/XperienceCommunity.Lucene.Suite.Admin/Client/src/query-tester/QueryTesterTemplate.tsx | SearchInput, Filters SidePanel (draft/Apply), applied-filters bar, Table states, detail SidePanel, focus handling | 361 |
| src/XperienceCommunity.Lucene.Suite.Admin/Client/{package.json, tsconfig.json} | `test` script; allowImportingTsExtensions + erasableSyntaxOnly | — |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/QueryTesterPage.cs | Menu icon; IndexName property removed; no AddErrorMessage snackbar | 152 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SiteSearchApplication.cs | Display name "Site search", Icons.Magnifier (identifier unchanged) | 31 |
| src/XperienceCommunity.Lucene.Suite.Admin/UIPages/SiteSearchIndexListingPage.cs | Entries via formatter, empty-listing QuickTip | 89 |
| tests/XperienceCommunity.Lucene.Suite.Tests/AdminRegistrationTests.cs | Literal identifier/slug/permission asserts; icon + listing tests | 92 |
| .paul/SPECIAL-FLOWS.md | Admin UI design validation required | — |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: State fixes as tested pure helpers + wiring | Done | `58c3126` |
| 2: Layout, kit states, side panel, a11y; C# polish | Done; layout reworked at checkpoint | `58c3126`, `f014963` |
| 3: Make design validation required | Done | `58c3126` |
| 4: Checkpoint (human-verify) | Step 5 + label failed → fixes → approved 2026-10-10 | `f014963` |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Facet left column replaced by a Filters SidePanel with draft / Clear all / Cancel / Apply; "Filter" button with badge; Tag click opens the panel | User request at checkpoint (content-hub pattern); kit FilterPanel / FilterStatusIndicator not exported in 31.9.1, so composed from exports | Table gets full width; panel counts are from the last executed search (KNOWN-LIMITATIONS) |
| 2 | "Applied filters:" label explicitly styled `--color-text-low-emphasis` | Admin shell root `.app___dEm8E { color: red }` applies to any unstyled text | Any future unstyled text in Suite templates will also render red — always use kit Text or a token colour |
| 3 | Detail-panel close returns focus to the table wrapper, not the clicked row; rows not keyboard-reachable | npm Table row button is `display: contents` and cannot take focus | Detail panel is mouse-only for keyboard users (KNOWN-LIMITATIONS; carried to 03-05) |
| 4 | Pagination not disabled while loading | No `disabled` prop in admin-components 31.9.1 | Covered by abort + latest-wins (KNOWN-LIMITATIONS) |
| 5 | Column headers humanized (display only); stats Row wrapped in a div | Checkpoint polish; kit Row inline margin-top cancelled Stack spacing | — |

## Key Patterns / Decisions

- Keep admin client state logic in erasable-TS pure functions tested with `node --test` — no test framework, no bundle-resolution change.
- Filter UIs: draft in a SidePanel, commit on Apply (one page-1 search), applied-filters bar acts immediately; Cancel / × / Escape discard the draft; focus returns to the opener.
- Never leave text unstyled in a Suite template (admin shell root colour is red).
- Role-grant identifiers are asserted as literals so a constant rename can't silently orphan grants.
- KNOWN-LIMITATIONS appended (project-sidecar/docs/KNOWN-LIMITATIONS.md): Show more stops at the server facet cap; aborted searches still run server-side; node type-stripping tests with transitive @types/node; headings via role wrappers; filter-panel counts from the last search; focus to table / rows not keyboard-reachable.

## Skill Audit

| Expected | Invoked | Notes |
|----------|---------|-------|
| Admin UI design validation (required) | ✓ | Kit source, stories and npm `dist` exports checked (FilterPanel/FilterStatusIndicator found unexported); tokens only; kit loading/empty/error states; focus/heading a11y; browser check at ~800 px. **Gap: 1440 px visual check not done** |
| /dotnet:csharp-refactoring (required) | ○ | C# changed in 58c3126 (QueryTesterPage, SiteSearchApplication, SiteSearchIndexListingPage, AdminRegistrationTests); invocation not evidenced in the plan (skills table still ○) or commit record |
| /kentico-web-development:content-retrieval | n/a | No content/module-class queries changed |

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| Resolved: admin UI design-validation concern (STATE Blockers/Concerns) — review fixes shipped, validation now required | Closed |
| 1440 px visual check and rapid-click re-test on the Filters panel / Apply / bar controls not run | Next admin UI checkpoint (03-05) or a wider browser pane |
| npm Table rows not keyboard-reachable; detail focus returns to the table | 03-05 rules listing (needs a per-row action or a focusable row) |
| Filter-panel counts don't reflect draft selections | KNOWN-LIMITATIONS; if testers need it |
| Audit deferral: server keeps executing aborted searches | KNOWN-LIMITATIONS; if search cost grows |
| @types/node only transitive | Add as direct devDependency if it disappears |
| Earlier deferrals (03-01 role grants README, old URLs; custom permissions not yet enforced on any command) | Unchanged — packaging / 03-05 rules admin UI (renumbered from 03-04) |

## Next

Phase 3: 2 of 5 plans complete. Next: `/paul:plan` 03-03 Rule model + pure engine.

---
*Completed: 2026-10-10*
