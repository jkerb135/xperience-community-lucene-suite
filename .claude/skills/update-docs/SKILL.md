---
name: update-docs
description: Update and ship the GitHub Pages docs (docs/) after a PAUL phase completes. Run during UNIFY of every phase, or when the user asks to refresh/publish docs.
---

# update-docs

Keeps `docs/` (Jekyll + just-the-docs, deployed by `.github/workflows/docs.yml` on push to `main`) in step with the code.

## Steps

1. **Scope the change.** Read the phase's PLAN/SUMMARY under `.paul/phases/` and `git diff --stat <phase-start>..HEAD -- src/`. List user-visible changes: new/changed public API, `LuceneSuite:*` settings, admin UI pages, widgets/endpoints, DB schema version, activity types. Internal-only phases: report "no doc impact" and stop.
2. **Map to pages.** `getting-started`, `configuration`, `rules-and-synonyms`, `widgets`, `samples`, `suggestions`, `activities`, `search-reports`, `analytics-client-contract`. Edit the existing page; add a new page only for a new feature area (front matter `title`, `nav_order`).
3. **Write from source.** Delegate to the `doc-specialist` agent for anything beyond a few lines. Document only what the code does; verify signatures and defaults by reading source. Samples must compile; check in `project-sidecar/scratch/` (gitignored) and delete afterwards. Update `analytics-test-vectors.json` only if the contract changed.
4. **Check.** Every `docs/*.md` has front matter; relative links resolve; no page documents a setting or API that no longer exists (grep the names).
5. **Ship.** Commit only `docs/` (and `.github/workflows/docs.yml` if touched) as `docs(<phase>): <summary>` and push. Pages redeploys on push to `main`. If the branch is not `main`, push and say the site won't update until merge. Never include unrelated files.
6. **Record.** Note "docs updated" (pages touched) or "no doc impact" in the phase SUMMARY / STATE.md.

## Do not
- Rewrite pages the phase did not affect.
- Publish `.paul/` or `project-sidecar/` content.
