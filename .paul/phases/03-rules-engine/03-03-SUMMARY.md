---
phase: 03-rules-engine
plan: 03
subsystem: rules
tags: [system.text.json, kentico-module-class, pure-engine, installer]

requires:
  - phase: 02-event-capture
    provides: installer applock/version/downgrade-guard pattern, DatabaseFact disposable-DB harness, pure-engine style (RollupEngine)
  - phase: 03-rules-engine (03-01)
    provides: Site Search app + LuceneSuite.ManageRules permission (used by 03-05)
provides:
  - Rule model (RuleDefinition/RuleCondition/RuleActions…) + RuleLimits
  - RuleSerializer — strict JSON parse/validate with path-addressed errors; Serialize* for 03-05
  - RuleEngine — pure matching + conflict resolution (internal)
  - LuceneSuite.Rule module class (RuleInfo) via installer v4
affects: [03-04 search integration, 03-05 rules admin UI, Phase 5 preview sandbox]

tech-stack:
  added: []
  patterns:
    - "Rule JSON validated by walking the model and collecting every error with a JSON path; never throws"
    - "Whole-word matching on normalized strings (space/edge-bounded substring) instead of token arrays"

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite/Rules/RuleDefinition.cs
    - src/XperienceCommunity.Lucene.Suite/Rules/RuleSerializer.cs
    - src/XperienceCommunity.Lucene.Suite/Rules/RuleEngine.cs
    - src/XperienceCommunity.Lucene.Suite/Rules/RuleInfo.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleSerializerTests.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/RuleEngineTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite/Analytics/AnalyticsModuleInstaller.cs
    - tests/XperienceCommunity.Lucene.Suite.Tests/AnalyticsInstallerDatabaseTests.cs

key-decisions:
  - "Malformed JSON → one error at the root path; unknown members / bad enum values keep their precise path"
  - "Null or blank stored JSON reads as an empty array / object"
  - "RuleEngine and its input/outcome types stay internal until 03-04 needs a public surface"

patterns-established:
  - "Disposable DB comm_projects_test (restored backup + --kxp-update) for LUCENESUITE_TEST_CONNECTIONSTRING"

duration: ~1h
started: 2026-10-10T19:30:00Z
completed: 2026-10-10T21:00:00Z
description: "Strict JSON rule model with path-addressed validation, a pure sub-millisecond rule engine, and the LuceneSuite.Rule module class via installer v4"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 3 Plan 03: Rule Model + Pure Engine Summary

**Rules are now a validated JSON model stored in `LuceneSuite.Rule` (installer v4), and a pure engine resolves which rules fire and their single outcome (pins, hides, boosts, user data) in about 0.5 ms for 500 rules.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1 h |
| Tasks | 3 completed (3 PASS, 0 GAP/DRIFT left) |
| Files | 6 created, 2 modified (+ KNOWN-LIMITATIONS) |
| Tests | 349 pass with DB tests on (was 244 + 21 skipped); 2 opt-in volume tests skipped as before |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Strict parse + path-addressed validation | Pass | 54 RuleSerializerTests: one row per invalid case incl. audit additions (metadata, within-rule duplicates, limits, no actions); round trip lossless; userData cloned |
| AC-2: Matching semantics | Pass | Anchorings, whole words ("grinder" ≠ "grinders"), browse, facet AND / context OR, validity edges, disabled, other index, zero conditions, priority/Id order; input normalized without PII placeholders (10-digit run matches) |
| AC-3: Deterministic conflict resolution | Pass | Hide beats pin, earlier rule keeps placement, collision shift, drop past 300, boost sum/clamp/zero-drop/order, userData order, shuffle × 50 identical; 500-rule median < 1 ms (≈0.5 ms) |
| AC-4: LuceneSuite.Rule via installer v4 | Pass | DB tests on comm_projects_test: v3→v4 keeps v3 objects, concurrent + rerun idempotent, version 5 untouched, RuleInfo round trip, invalid stored JSON → errors; host on comm_projects logged "Create Class 'Lucene Suite search rule'", version 4, index present |

## Verification Results

- `dotnet build XperienceCommunity.Lucene.Suite.slnx` → 0 warnings, 0 errors.
- `dotnet test` (no DB) → 327 passed, 24 skipped; with `LUCENESUITE_TEST_CONNECTIONSTRING` → comm_projects_test: 349 passed, 2 skipped (RollupVolumeTests, opt-in).
- CommunityProjects host built (6 warnings, all host/other-library code) and started on http://localhost:27340; no `fail:`/`crit:` lines; stopped afterwards by agreement.
- KNOWN-LIMITATIONS: 4 entries added (single validity window; priority-over-position duplicate pins + silent drop past 300; whole-word matching only; case-sensitive facets/contexts).

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `Rules/RuleDefinition.cs` (126) | Created | Public records, RuleAnchoring, RuleLimits |
| `Rules/RuleSerializer.cs` (332) | Created | Parse/validate/serialize; NormalizeText (no PII placeholders) |
| `Rules/RuleEngine.cs` (168) | Created | Pure Evaluate: matching, ordering, pins/hides/boosts/userData |
| `Rules/RuleInfo.cs` (159) | Created | Module class: CI/CD off, LogEvents, cache touch, web-farm invalidation; ToDefinition |
| `Analytics/AnalyticsModuleInstaller.cs` | Modified | Version 4: Rule class + IX_LuceneSuite_Rule_IndexName; resource description |
| `RuleSerializerTests.cs` (199), `RuleEngineTests.cs` (249) | Created | Pure table tests |
| `AnalyticsInstallerDatabaseTests.cs` | Modified | Rule class in cleanup/asserts; v3→v4, round trip, invalid stored row |

## Skill Audit

All required skills invoked ✓ — /dotnet:csharp-refactoring and /kentico-web-development:content-retrieval loaded before Task 1. Admin design skills not applicable (no admin UI).

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 3 | Essential (correctness, performance target) |
| Approach changes | 3 | Same behaviour as specified |
| Deferred | 0 new | Audit deferrals stand |

### Auto-fixed Issues

1. **Context regex accepted a trailing newline** (Task 1): `$` matches before a final `\n` in .NET; changed to `\z`. Covered by the `"vip\n"` test row.
2. **Engine missed the 1 ms target (2.0 ms median)** (Task 2): with ~500 fired rules, slot probing in a SortedDictionary and per-evaluation pattern splitting dominated; replaced by a slot array and allocation-free boundary matching → ≈0.5 ms.
3. **Truncated JSON reported a nested path** (Task 1): System.Text.Json points at the deepest position; syntax errors (inner reader exception) now map to the root path, as AC-1 specifies.

### Approach Changes

- Task 2 said "tokenize the normalized query once per Evaluate"; matching is space/edge-bounded substring search on the normalized strings — identical results on normalized input, no allocations.
- Null/blank conditions or actions JSON read as `[]` / `{}` (not specified); empty actions still fail the "no actions" rule.
- Test DB updated with `dotnet run --no-build --kxp-update` because the user's host held the bin folder; the old binaries took the test DB to Suite v3, which became the v3→v4 starting point.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Host build locked by a running CommunityProjects instance | Update run with --no-build; user stopped the host for the final build/start check |
| `IInfoProvider<RuleInfo>.Get(int)` not available | Query by `WhereEquals(RuleID)` |

## Next Phase Readiness

**Ready:**
- 03-04 can load `RuleInfo` rows, call `ToDefinition()`, skip rows with errors, and feed `RuleEngine.Evaluate`; RuleInfo touches cache dependencies and supports web-farm invalidation.
- 03-05 has `RuleSerializer.Parse` (inline errors by path) and `Serialize*`.

**Concerns:**
- 03-04 must log skipped (invalid) rule ids — fail-safe here means silent otherwise.
- Engine types are internal; 03-04 decides the public surface.
- Dropped pins (past 300) are not reported in `RuleOutcome`.

**Blockers:** None.

---
*Phase: 03-rules-engine, Plan: 03 · Completed: 2026-10-10*
