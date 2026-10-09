---
phase: 02-event-capture
plan: 03
subsystem: analytics
tags: [options-binding, validate-on-start, module-class, installer-v3, rollup-engine, attribution, test-vectors, determinism]

requires: [02-01 SearchEvent capture, 02-02 SearchInteraction + contract + 17 vectors + reference minter]
provides:
  - SuiteSearchOptions.SectionName = "LuceneSuite"; nested analytics groups Merge / Attribution / Retention / Surfacing / Job with defaults; bound from configuration, code delegate applied after; SuiteSearchOptionsValidator + ValidateOnStart
  - Class LuceneSuite.SearchDaily (object type lucenesuite.searchdaily), installer v3 (additive, under the existing sp_getapplock), unique IX_LuceneSuite_SearchDaily_Day_IndexName_QueryText
  - Internal pure engine Analytics/Rollup: RollupEngine.Roll(DateOnly, raw searches, raw interactions, RollupSettings) and Segment(...); DailyKpi with fixed latency histogram
  - docs/analytics-test-vectors.json v2 (20 cases, top-level settings + per-case overrides), contract settings table
affects: [02-04 job & retention (maps Info rows to the engine input contract, persists DailyKpi to SearchDaily, consumes Retention/Job settings), Phase 6 Widgets JS client (must use server settings), v0.2 reports (read SearchDaily)]

tech-stack:
  added: []
  patterns: [Configure<IServiceProvider> optional-IConfiguration binding, IValidateOptions with full-key messages, pure engine with explicit input contract, settings-carrying shared vector file, seeded shuffle determinism tests]

key-files:
  created: [src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptionsValidator.cs, src/XperienceCommunity.Lucene.Suite/Analytics/SearchDailyInfo.cs, src/XperienceCommunity.Lucene.Suite/Analytics/Rollup/{RollupEngine.cs, RawSearchRow.cs, RawInteractionRow.cs, DailyKpi.cs, LogicalSearch.cs}, tests/XperienceCommunity.Lucene.Suite.Tests/{AnalyticsOptionsTests.cs, RollupEngineTests.cs}]
  modified: [src/XperienceCommunity.Lucene.Suite/Search/SuiteSearchOptions.cs, src/XperienceCommunity.Lucene.Suite/LuceneSuiteServiceCollectionExtensions.cs, src/XperienceCommunity.Lucene.Suite/Analytics/AnalyticsModuleInstaller.cs, docs/{analytics-client-contract.md, analytics-test-vectors.json}, tests/XperienceCommunity.Lucene.Suite.Tests/{TestVectorTests.cs, AnalyticsInstallerDatabaseTests.cs}]

key-decisions:
  - "Binding via Configure<IServiceProvider> + optional IConfiguration instead of BindConfiguration: bare ServiceCollection callers keep working; no live-reload change token"
  - "Logical search capped at AttributionWindow from its first row; engine input contract documented in XML remarks for 02-04"
  - "Duplicate (SearchId, Sequence): earliest ReceivedUtc, then lowest row id"
  - "Mint-rule conflicts from 02-02 resolved by configuration: defaults keep the literal rules; vectors prove TypoOneEditMinLength 5 and DeepBackspaceRatio 0.4 flip the two cases"

duration: APPLY → commit d5261ec at 2026-10-09 10:50 -04:00
completed: 2026-10-09
---

# Phase 2 Plan 03: Merge Engine Summary

**Every analytics setting is now configurable from appsettings "LuceneSuite" and validated at startup. A pure, deterministic rollup engine turns one UTC day of raw rows into daily KPIs, and the shared vectors drive it under default and non-default settings. The SearchDaily class ships as installer v3. All 7 ACs pass. Nothing is persisted or scheduled yet: that, plus the unmeasured whole-day-in-memory cost, is 02-04.**

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Every setting configurable and validated | Pass | Capture settings stay on SuiteAnalyticsOptions (Enabled true, QueueCapacity 10000, FlushInterval 2 s, BatchSize 500); nested Merge (30 s, 4, 8, 0.5), Attribution (1 h), Retention (48 h, 762 days ≈ 25 months, 30 days), Surfacing (5, 2), Job (15 min); FacetValuesPerDimension and SearchFields bind too. Config first, then delegate. Validator messages start with the full key; RawRetention ≥ 1 day + AttributionWindow + RollupInterval + 1 h (boundary 1.03:30:00 passes, 1.03:29:59 fails). Histogram edges + UTC day are not options. AnalyticsOptionsTests: 21 |
| AC-2: Daily KPI class (installer v3) | Pass | LuceneSuite.SearchDaily: SearchDailyID, Day date, IndexName(200), QueryText(256) NULL ('' browse), Searches, ZeroResultSearches, SubmittedSearches, ClickedSearches, Clicks, ConvertedSearches, Conversions, SessionCount, LatencyBucket0..9, ClickPositionSum bigint; unique ascending index; TypeInfo hygiene as the other classes. On a disposable copy: 5/5 DB tests passed twice (fresh concurrent + rerun; v1 → current; v2 → v3 leaves SearchEvent/Interaction definitions and rows untouched; newer-version no-op; duplicate (Day, Index, Query) rejected). Mutation non-unique index → 4 DB tests fail; reverted. Copy dropped, .bak deleted (sys.databases 0) |
| AC-3: Merge rule (pure engine) | Pass | Dedupe per (SearchId, Sequence) with earliest ReceivedUtc then lowest row id; split on text change after gap > window or beyond the duration cap; counted text = earliest clicked row else last non-fragment; zero-result/latency/submitted per rule; '' browse bucket; day of first row; input contract in XML remarks. Midnight tests 23:59:50→00:40 (earlier day), paging to 01:10 (split at cap), 00:00:05 (new day) |
| AC-4: Attribution | Pass | Interactions in (ReceivedUtc, Id) order, EventId once, |Δt| ≤ window, page window (Page−1)×PageSize < Position ≤ Page×PageSize, once per (logical search, ItemGuid); conversions with/without position; 00:20 click counts for the earlier day |
| AC-5: Shared vectors drive the engine | Pass | Vectors v2: 20 cases, top-level settings + per-case overrides; 3 non-default (TypoOneEditMinLength 5 → ipad/ipod two; DeepBackspaceRatio 0.4 → coffee grinder one; TextChangeTimeoutMs 45000). Minter reads settings; adapter feeds vectors into the engine (same boundaries + attributed clicks); FileDefaults_MatchTheServerDefaults. Contract doc has the settings table, fixed exceptions and "use the server's values". TestVectorTests 22 → 46 |
| AC-6: Multi-node and determinism | Pass | 3 fixed seeds, rows split across simulated nodes, identical output, non-triviality assert; engine takes day + settings, no I/O, no clock |
| AC-7: No regressions | Pass | Release build 0/0 and `dotnet restore --locked-mode` (re-run at UNIFY); tests 223 passed, 5 DB-gated skipped, 228 total (orchestrator); xbk scratch builds normal + SeparatedAdmin 0 errors / 279 warnings; Suite.Admin.dll absent in SeparatedAdmin; xbk lock file restored byte-identical |

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet restore --locked-mode` | Pass (UNIFY re-run) |
| `dotnet build -c Release` | 0 warnings, 0 errors (UNIFY re-run) |
| `dotnet test -c Release` | 223 passed, 5 skipped (DB-gated), 228 total |
| DB-gated tests on disposable copy | 5/5 twice; copy and .bak removed |
| Mutations (each failed tests, reverted, clean diff) | text-changed split condition; row-id tie-break; duration cap; JSON default edits ×4; non-unique index |
| xbk scratch builds (normal + SeparatedAdmin) | 0 errors, 279 warnings each |
| Writes to `xperience` | None: only SELECT and the COPY_ONLY BACKUP |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 1: Options, binding, validation; SearchDaily + installer v3 | Done (deviations 1, 2) | `d5261ec` |
| 2: Pure rollup engine | Done (RollupEngineTests 45) | `d5261ec` |
| 3: Parameterized vectors + contract; engine runs the vectors | Done (TestVectorTests 46) | `d5261ec` |

## Tests Added

| File | Covers |
|------|--------|
| AnalyticsOptionsTests.cs (21) | Binding from in-memory config, delegate-after-config precedence, each validation rule naming the key, retention boundary |
| RollupEngineTests.cs (45) | Each merge/attribution rule, XpSearch bug examples, midnight cases, tie order ×4, page window, dedupe, conversions, 3-seed multi-node determinism |
| TestVectorTests.cs (22 → 46) | Minter with file settings + overrides, engine agreement, file defaults = server defaults |
| AnalyticsInstallerDatabaseTests.cs (extended) | v2 → v3, v1 → current, concurrency, newer-version no-op, unique index |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | Binding via `Configure<IServiceProvider>((o, sp) => sp.GetService<IConfiguration>()?.GetSection("LuceneSuite").Bind(o))`, not `BindConfiguration` | BindConfiguration throws without IConfiguration and would break bare ServiceCollection callers and a test | No live-reload change token (KNOWN-LIMITATIONS) |
| 2 | New file Search/SuiteSearchOptionsValidator.cs | Plan named an IValidateOptions implementation but no file | One file |
| 3 | Test renames (Version1Database_UpgradesToCurrent_…, FileHas…_AndTwoWithNonDefaultSettings); DB harness generalised | Tests now span v1–v3 and 3 override cases | Test-only |
| 4 | Rule interpretations: earliest clicked = lowest-Sequence row with a counted click; conversions don't decide counted text; symmetric attribution window; position check on clicks only; index = first row's; earliest duplicate click supplies position; engine re-applies only server time rules | Spec silent or ambiguous | Recorded in KNOWN-LIMITATIONS; contract doc notes the duration cap |
| 5 | DailyRetention default 762 days | "25 months" has no fixed day count | Approximation recorded in KNOWN-LIMITATIONS |

## Key Patterns / Decisions

- Optional-IConfiguration binding keeps AddLuceneSuite usable without a host; validation fails startup with the full configuration key.
- The engine owns which logical searches belong to a day; callers only supply rows per the input contract (searches of every SearchId touching [day − window, day end + window], interactions in [day, day end + 2 × window]).
- Vector file carries its own settings, so client and server share one executable definition of the merge rule under any configuration.
- KNOWN-LIMITATIONS appended (project-sidecar/docs/KNOWN-LIMITATIONS.md): O(D²·L) fragment check; whole day in memory; segmentation can differ after raw purge; rule interpretations; DailyRetention approximation and no config change tracking; minting entry updated (both values now settings).

## Skill Audit

Skill audit: All required skills invoked ✓
- `/dotnet:csharp-refactoring`: invoked.
- `/kentico-web-development:content-retrieval`: invoked.

## Deferred / Carry-forward

| Item | Revisit |
|------|---------|
| Whole day in memory; engine volume unmeasured (audit deferral 1) | 02-04: measure with synthetic volume; stream per SearchId if needed |
| No configuration change tracking: changing merge settings re-segments only days recomputed afterwards; no live reload | 02-04 / reports (v0.2) |
| Segmentation can differ when a day is recomputed after its raw rows were partly purged | 02-04 watermark + retention ordering |
| Admin settings page (audit deferral 2) | With the permissions concern (Phase 3) |
| Resolved from 02-02: ipad/ipod and coffee-grinder mint conflicts (now settings, proven by vectors) | Closed |

## Environment Note

An xbk.exe (PID 13160, started outside this loop) is listening on port 55709, running pre-02-03 code against `xperience` at installer version 2. It was left alone; on its next start with current code the installer will run the additive v2 → v3 step.

## Next

Phase 2 progress: 3/4 plans. Next: `/paul:plan` 02-04 Job & retention.

---
*Completed: 2026-10-09*
