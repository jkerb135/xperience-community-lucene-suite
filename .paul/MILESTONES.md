# Milestones

Completed milestone log for this project.

| Milestone | Completed | Duration | Stats |
|-----------|-----------|----------|-------|
| v0.1 Tune & Capture | 2026-10-12 | 5 days (2026-10-08 → 2026-10-12) | 6 phases, 21 plans |
| v0.2 Measure | 2026-10-13 | 2 days (2026-10-12 → 2026-10-13) | 8 phases, 21 plans |

---

## ✅ v0.2 Measure

**Completed:** 2026-10-13
**Duration:** 2 days (2026-10-12 → 2026-10-13)

### Stats

| Metric | Value |
|--------|-------|
| Phases | 8 (7, 8, 8.1, 9, 10, 11, 12, 13) |
| Plans | 21 (incl. inserted 13-02.1 and review follow-up 13-05; 08.1-04 merged into 08.1-03) |
| Commits | 67 since v0.1.0 |
| Files changed | 162 outside `.paul` (+18 224 / −1 127 lines) |
| Tests at close | 859 .NET passed (6 opt-in skipped), 103 admin client node tests |
| Approval checks | #4–#21 approved 2026-10-13 |

### Key Accomplishments

- **Reports on real or seeded history:**
  - A report service over SearchDaily: overview with previous-period deltas, trend, query tables with "(other)", and latency percentiles.
  - A dev-only seed runner for demo history.
  - The Search reports dashboard: KPI tiles with sparklines, trend, breakdown and latency panels, and query tables with data bars and row actions.
- **Search insights:** five finding kinds, ranked by searches actually lost, with pre-filled fixes ("Fix these first" and "All insights").
- **Query suggestions:**
  - A public `ISuiteSuggestions` API and a strings-only suggest endpoint.
  - An accessible, CSP-safe autocomplete.
  - Per-index blocked suggestions, including a one-click Block from reports.
- **Kentico custom activities** for searches, no-result searches and result clicks. They are installed disabled and follow Kentico's consent and cookie level.
- **Public API audited and frozen:** PublicApiAnalyzers baseline, `LogClick(SearchClick)`, one Widgets namespace, and docs/public-api.md.
- **Admin UI consistency (Conventions 1–10), then the enterprise screens per the design handoff:**
  - a custom rules listing with bulk actions;
  - a regrouped rule editor with context and facet pickers and a conflict Reload;
  - reports and query tester restyled with data bars, score bars, rank movement and full loading, empty and error states.
- **Audit trail:** every rule, synonym and blocked-suggestion change is in Kentico's event log with the user.

### Key Decisions

- Reports read rollups only. "(other)" is computed by SQL. Insights are computed on read and ranked by searches actually affected.
- Suggestions return strings only, from threshold-surfaced queries. The blocklist affects suggestions, never reports.
- Activity types are installed disabled. Suite-written activity URLs carry no query strings.
- Admin services stay internal; every public change is a PublicAPI.Unshipped.txt diff.
- Admin forms use page commands for live validation; Kentico marks the form busy during form-component commands. A successful edit save reloads the page; Kentico applies returned values only on failed submits.
- Phase 13 handoff deviations:
  - Spinner, not skeletons.
  - Stale state fades graphics only.
  - Configurable Compare columns kept.
  - Pins keep typed positions.
- Audit trail through Core object events; bulk actions log one summary. Retention is the site's CMSLogSize.
- Seeded demo history is kept on the dev host (#4).

### Archive

`.paul/milestones/v0.2.0-ROADMAP.md`

---

## ✅ v0.1 Tune & Capture

**Completed:** 2026-10-12
**Duration:** 5 days (2026-10-08 → 2026-10-12)

### Stats

| Metric | Value |
|--------|-------|
| Phases | 6 |
| Plans | 21 (incl. 06-04 review follow-up) |
| Files changed | 159 outside `.paul` (+31 854 / −221 lines) |
| Tests at close | 588 .NET passed (3 opt-in skipped), 27 Widgets JS + admin client node tests |

### Key Accomplishments

- **Suite search API** over Kentico Lucene: field boosts, facets with sideways counts, timing, and a replaceable query factory. A latency budget held: about 8 ms p95 with 300 pins and 500 rules.
- **Identity-free, web-farm-safe analytics.**
  - Search, click and conversion capture through background bulk writes.
  - A shared client contract with executable test vectors.
  - A single-runner rollup job with a watermark, retention with a privacy ceiling, and a surfacing threshold with an "(other)" fold.
- **Suite-owned "Site Search" admin app** with its own permissions (View, Manage rules, Manage synonyms, Manage experiments) and a per-index Search tuning section.
- **Rules engine:**
  - Structured JSON rules evaluated by a pure engine: pin, boost, bury, hide and custom data.
  - Conditions on query text, facets and contexts, with priority and validity windows.
  - Pins counted in totals and facets.
  - Native admin editors, including a pin/hide item picker.
- **Query-time synonyms:** two-way and one-way, weighted below exact matches, with a tag/chip admin editor and applied synonyms shown in the query tester.
- **Preview sandbox:** query tester Compare mode with Baseline vs Tuned columns, simulated contact-group/free-text contexts, and rank-change markers.
- **Optional Widgets package:**
  - A rate-limited events endpoint (it never reconfigures the host) and the contract JS client.
  - Server-rendered search box, results and facets that share one URL-driven search.
  - Page Builder widgets; preview and edit mode are never tracked.
- **Real traffic on the test host:** the CommunityProjects "Site search" page records searches, continuations and clicks.

### Key Decisions

- Rules stored as structured JSON and evaluated by a pure engine, not Kentico macros.
- Pins and hides are chosen by searching the index.
- Synonyms are query-time variants weighted at 0.8.
- Preview is a compare mode with contexts that are the strings the site would send.
- Analytics are identity-free KPI data. A session cookie is used only at the Visitor/All cookie level. Every setting is configurable except histogram edges and the UTC day.
- Admin UI: native Kentico listing/edit pages, with custom React only for structured fields. Page context reaches component commands via `GetFormItems`, never the client.
- Widgets:
  - Server-rendered, with one search per request.
  - Page size and contact-group contexts are site-wide options.
  - `sr=` only on same-site links.
  - Pages that run a search are uncacheable.
- Package code never mutates host-wide configuration.
- Human-verify checkpoints are deferred to `.paul/APPROVAL-CHECKS.md` and reviewed at the end of the milestone (both reviews approved 2026-10-12).

### Archive

`.paul/milestones/v0.1.0-ROADMAP.md`

---
