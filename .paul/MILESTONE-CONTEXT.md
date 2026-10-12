# Milestone Context

**Generated:** 2026-10-12
**Status:** Ready for /paul:milestone

## Features to Build

- **Analytics reports:** admin report pages per index, built on the `SearchDaily` rollups.
  - Metrics: CTR, no-results queries, top queries and slowest queries (from the latency histogram).
  - Controls: a date range and filters.
  - Reports recompute on every condition change. There is no polling or push.
- **Seed generator:** a dev-only tool that writes realistic synthetic raw search and interaction events across many days into the disposable test DB, and optionally into the CommunityProjects host.
  - The data follows the analytics client contract (sid/seq, typos, continuations, clicks).
  - The real rollup job then turns it into daily rows, so reports are built and verified against realistic history.
- **Query suggestions:** as-you-type suggestions built from surfaced queries (the existing surfacing threshold of ≥ 5 searches on ≥ 2 days).
  - Served by a rate-limited GET endpoint in the Widgets package that returns suggestion strings only, never results.
  - A small progressive-enhancement script on the search box shows them. Without JavaScript the box works as today.
  - Suggestion requests are never tracked as searches.
- **Blocked suggestions:** a marketer-managed blocklist per index. Blocked terms never appear as suggestions; reports still show what visitors searched.
- **Custom activities** through Kentico activity tracking and its consent/cookie level (the Suite adds no gate of its own):
  - **Search performed:** one per logical search (new search id / submit, not every page or filter change). Uses scrubbed query text.
  - **Result clicked:** item and query.
  - **No results:** query that returned nothing.
  - These only happen when Kentico has a current contact and activity logging is allowed.

## Scope

**Suggested name:** v0.2 Measure
**Estimated phases:** 4
**Focus:** Marketers see how site search performs and act on it: reports on real (or seeded) history, suggestions that steer visitors, and search behaviour flowing into Kentico contacts.

## Phase Mapping

| Phase | Focus | Features |
|-------|-------|----------|
| 7 | Report data | Report query service over `SearchDaily` (KPIs, top, no-results, CTR, slowest; date range, `(other)` handling) + seed generator (contract-faithful synthetic events → real rollup) |
| 8 | Reports admin | Report pages in the Site Search app per index (design via /xbyk-admin-ui:design-screen), recompute on condition change, links into rules/synonyms (e.g. create a rule for a no-results query) |
| 9 | Suggestions + blocklist | Suggestion index/source from surfaced queries, suggest endpoint (rate-limited, strings only, untracked), search box script (progressive enhancement, accessible combobox), blocklist module class + admin editor |
| 10 | Custom activities | Kentico custom activity types (search performed, result clicked, no results) logged from the capture pipeline when a contact exists and consent allows |

## Constraints

- Identity-free Suite analytics are unchanged. Custom activities are Kentico's contact data and stay governed by Kentico consent/cookie level; the Suite never copies contact data into its own tables.
- Reports read the rollups (`SearchDaily`), never raw events. They respect the surfacing threshold and show the `(other)` bucket as such. The fixed histogram edges and UTC days stay fixed.
- The suggest endpoint returns no results and no counts beyond what the surfacing threshold allows (k-anonymity). It is rate-limited like the events endpoint and never reconfigures the host.
- The seed generator is dev-only. It is never part of the packaged runtime path, and it writes to the host only on explicit request.
- Settings stay in appsettings `LuceneSuite` (validated on start). No admin settings page in v0.2 (user decision).
- New admin screens follow SPECIAL-FLOWS (design-screen in PLAN and APPLY, design validation).
- Human-verify checkpoints are recorded in `.paul/APPROVAL-CHECKS.md` and reviewed at the end of the milestone.
- Web-farm and SaaS safe. No new dependencies unless unavoidable. Lucene.Net.* at the exact host beta if any are added (e.g. Suggest).

## Additional Context

- v0.1 decisions to revisit consciously:
  - "Server-rendered widgets, no public search API" is extended by a suggestions-only endpoint (user decision 2026-10-12). It still serves no search results.
  - The contract already says as-you-type dropdowns send `TrackAnalytics = false`.
- Out of scope (unchanged): PDF export, live push updates, unique-user metrics, admin settings page.
- Open items to settle when planning:
  - Suggestion matching: prefix vs typo-tolerant. Ranking: count vs recency.
  - Suggestion freshness: cache and invalidate with each rollup.
  - Activity value formats.
  - Whether reports offer CSV export.
  - The XpSearch leftovers on the host (DB objects, scheduled tasks, macro) are still pending.

---

*This file is temporary. It will be deleted after /paul:milestone creates the milestone.*
