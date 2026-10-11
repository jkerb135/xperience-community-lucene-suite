---
phase: 06-widgets-package
plan: 04
completed: 2026-10-12T00:00:00Z
duration: ~20min
description: "CommunityProjects index stores the content type's display name in the ContentType facet, so Site search filters read \"Product page\" / \"Article page\""
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 6 Plan 04: Readable Facet Values (Host Quick Fix) Summary

**The CommunityProjects index now stores the content type's display name in the `ContentType` facet. The Site search filters read "Product page (24)" and "Article page (4)", with the same counts as before, and toggling a filter works.**

## AC Result

| Criterion | Status |
|-----------|--------|
| AC-1: Readable facet values on the live Site search page | Pass. After the index rebuild, `/site-search?q=coffee` shows "Product page (24)" and "Article page (4)". Selecting "Article page" gives 4 results. Host build has 0 errors, no new host log errors, and the Suite tests pass (588 passed, 3 skipped). |

## Files Changed

| File | Change |
|------|--------|
| `F:/Personal/CommunityProjects/src/Search/DancingGoatSearchIndexingStrategy.cs` (host, untracked) | Modified. `ContentTypeLabel` uses `DataClassInfoProvider.GetDataClassInfo(...).ClassDisplayName` and falls back to the code name. Backed up to `project-sidecar/backups/widgets-2026-10-11`. Byte-preserving edit. |
| DancingGoatSample index (host) | Rebuilt from the Lucene admin app |
| `project-sidecar/docs/KNOWN-LIMITATIONS.md` (git-ignored) | Old code-name filter URLs no longer match; display-name collisions would merge facet values |

---
*Completed: 2026-10-12*
