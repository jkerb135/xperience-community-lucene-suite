# Roadmap: XperienceCommunity.Lucene.Suite

## Overview
Marketer-facing search tuning, measurement and testing for Kentico.Xperience.Lucene. Milestones: tune and start capturing data (v0.1), measure (v0.2), relevance (v0.3), test (v0.4), instant search (v0.5). Event capture starts in v0.1 because reports need accumulated history.

## Current Milestone
**v0.3 Relevance** (v0.3.0)
Status: Not started — next: /paul:plan for Phase 14
Phases: 0 of 6 complete

**Focus:** Marketers control how queries are read and ranked, beyond rules and synonyms: words that don't matter, which fields count most, forgiving typos, guiding visitors to better queries, and lifting what visitors actually click.
**Depends on:** v0.2 (insights, suggestions, analytics pipeline, public API baseline)
**Decisions (2026-10-12, user):**
- Stopwords are removed at query time from a per-index, admin-managed list (language presets). No reindex; the Kentico analyzer is untouched (it is created by reflection).
- Field weights become an admin editor per index, stored in the DB. appsettings `LuceneSuite:SearchFields` remain the defaults.
- Typo tolerance: fuzzy variants (1 edit, 2 for long words, minimum word length) weighted below exact matches, like synonyms. Configurable per index.
- Suggestions gain both "Did you mean" (spelling corrections for zero- or low-result searches) and curated suggestions (marketers add, pin, reorder).
- Popularity boost is split: per-item click data starts collecting in the first phase so history exists when the boost ships last.
**Cross-cutting constraints:**
- One query-rewrite order, documented and tested: stopwords → synonyms → typo variants → field weights → rules/pins.
- Rules and analytics keep seeing the visitor's original text.
- Search overhead stays within p95 < 10 ms, with fuzzy expansions capped.
- Item popularity is identity-free: counts per item, no visitor data.
- Every new public type goes through PublicAPI.Unshipped.txt.
- Every admin screen follows the design-screen flow and Conventions 1–10.

| Phase | Name | Scope | Depends on |
|-------|------|-------|------------|
| 14 | Item Popularity Data | Per-item click rollup (installer v8: daily clicks per index + ItemGuid from the existing interaction events, same job, lock and retention), so history accrues early; report shows top clicked items | v0.2 rollup job |
| 15 | Stopwords | Per-index stopword list (module class, language presets, admin listing/editor under Search tuning, Manage synonyms); removed at query time before synonyms; query tester shows removed words; queries made only of stopwords fall back to the original text | 14 (none functionally) |
| 16 | Field Weights Editor | Admin editor of per-index field weights over the indexed fields (DB, cached, web-farm invalidated), appsettings as defaults; query tester compares before/after weights; validation against the index's fields | — |
| 17 | Typo Tolerance | Fuzzy variants per query word (min length, max edits, max expansions, weight below exact) through the query factory, fail-open; per-index settings in the admin; query tester shows matched corrections; analytics unchanged | 15, 16 (rewrite order) |
| 18 | Suggestions: Did You Mean + Curated | "Did you mean" corrections for zero/low-result searches (surfaced queries + index vocabulary), shown by the results widget and in ISuiteSuggestions/endpoint; curated per-index suggestions (add, pin, reorder) merged with analytics-derived ones; blocklist still wins | 17 (shares edit-distance logic) |
| 19 | Popularity Boost | Per-index opt-in boost from the Phase 14 click data (window, weight, decay, minimum clicks), applied like a rule boost below pins; query tester shows the effect; insights flag items boosted without recent clicks | 14 (≥ several weeks of data) |

**Plans:** created by `/paul:plan` when each phase starts.

## Completed Milestones

<details>
<summary>v0.2 Measure - 2026-10-13 (8 phases, 21 plans)</summary>

| Phase | Name | Plans | Completed |
|-------|------|-------|-----------|
| 7 | Report Data | 2/2 | 2026-10-12 |
| 8 | Reports Admin | 2/2 | 2026-10-12 |
| 8.1 | Admin UI Consistency Audit | 4/4 (08.1-04 merged into 03) | 2026-10-12 |
| 9 | Suggestions | 3/3 | 2026-10-12 |
| 10 | Custom Activities | 1/1 | 2026-10-12 |
| 11 | Search Insights | 2/2 | 2026-10-12 |
| 12 | Public API Audit | 1/1 | 2026-10-12 |
| 13 | Enterprise Screens | 6/6 (incl. 13-02.1, 13-05) | 2026-10-13 |

Full detail: `.paul/milestones/v0.2.0-ROADMAP.md` · Summary: `.paul/MILESTONES.md`

</details>

<details>
<summary>v0.1 Tune & Capture - 2026-10-12 (6 phases, 21 plans)</summary>

| Phase | Name | Plans | Completed |
|-------|------|-------|-----------|
| 1 | Foundation | 3/3 | 2026-10-09 |
| 2 | Event Capture | 4/4 | 2026-10-09 |
| 3 | Rules Engine | 7/7 | 2026-10-11 |
| 4 | Synonyms | 2/2 | 2026-10-11 |
| 5 | Preview Sandbox | 1/1 | 2026-10-11 |
| 6 | Widgets Package | 4/4 | 2026-10-12 |

Full detail: `.paul/milestones/v0.1.0-ROADMAP.md` · Summary: `.paul/MILESTONES.md`

</details>

## Later Milestones

### Further milestones

| Milestone | Contents |
|-----------|----------|
| v0.4 Test | A/B testing of rule sets on one index + comparison reports (can compare v0.3 relevance settings too) |
| v0.5 Instant Search | Public opt-in search endpoint (index + field allow-list, rate limit, searchId analytics via the JS client contract) + Algolia-InstantSearch-style JS widget library (search box, hits, refinements, pagination, stats) building on Phase 9 suggestions |

---
*Roadmap created: 2026-10-08 · Updated: 2026-10-13 (v0.2 Measure complete; v0.3 Relevance current)*
