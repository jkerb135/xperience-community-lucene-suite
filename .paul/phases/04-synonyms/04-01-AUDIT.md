# Enterprise Plan Audit Report

**Plan:** .paul/phases/04-synonyms/04-01-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. The architecture is sound:
- Query-time variants go through the existing query factory, with no analyzer change and no rebuild.
- The plan reuses the proven rule patterns: strict serializer, cached provider, fail-open behaviour and an add-only installer step.

Two defects would have shipped wrong behaviour:
1. **Fail-open only covered loading and expansion, not query building.** A custom `ISearchQueryFactory` that throws on a variant text would fail the whole search. Rules already have this rerun, and synonyms need it too.
2. **"Boost = weight" overwrites a boost the factory set.** That silently changes the scoring of custom factories.

With these fixed I would approve it.

## 2. What Is Solid

- **Variants go through `ISearchQueryFactory`.** Synonyms keep working with any factory, AND or OR semantics, and analysis and stemming apply to the variant. There is no second query-construction path to maintain.
- **DisjunctionMaxQuery with tie-breaker 0.** A document matching both the original and a variant is not double-scored, and the original keeps priority through the weight.
- **Single substitution with no cascading, capped at 20.** Bounded work, and the semantics match Algolia and Azure.
- **Prebuilt term → alternatives lookup.** Search cost depends on query length, not on the number of synonyms.
- **Rules and analytics read the original text.** No change to established Phase 2/3 contracts.
- **Plain-text terms, analyzed through QueryBuilder, no query parser.** Synonyms can't inject query syntax with the default factory.
- **Infrastructure carried over from RuleInfo:** CI/CD off, event log on and cache invalidation work across a web farm.

## 3. Enterprise Gaps Identified

1. **Query-building and execution failures with variants are not fail-open.** Only load and expand were covered.
2. **The variant boost overwrites a factory-set boost.**
3. **Key normalization asymmetry.** If entry terms and query words are normalized differently ("T-Shirt." in the query vs "t-shirt" in the entry), matches silently never happen.
4. **Punctuation-only terms pass validation but can never match.** They are silent dead data.
5. **The ranking promise is overstated.** The weight cannot guarantee exact-above-synonym: IDF and field differences can outweigh 0.8. Marketers need to know this.
6. **The request could be mutated.** That would leak the expanded text into capture or rules; it was not asserted.
7. **The checkpoint silently moves comm_projects to schema v5.** This build can't reverse that.
8. **No accent folding of inputs** ("cafe" vs "café").
9. **Synonyms of a deleted or renamed index stay orphaned.**
10. **A query-parser-based custom factory could interpret syntax characters in synonym terms.**

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Fail-open on query building/execution (gap 1) | AC-3, Task 2 action + tests | A non-ArgumentException with rules or variants applied → rerun once with neither. Flags are set per what was applied, with one throttled warning. Test: a factory throwing only for a variant. |
| 2 | Boost overwrite (gap 2) | AC-3, Task 2 action + tests | `variant.Boost *= weight`. Test: a factory boost of 2 stays 2 × weight. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | One MatchKey function for entries and queries (gap 3) | Task 2 action | The same normalize/split/trim/join function builds both, with a test. |
| 2 | Terms need a letter or digit (gap 4) | AC-1 | Validation rule added. |
| 3 | Weight is a preference (gap 5) | AC-3, verification | Stated in the AC and in KNOWN-LIMITATIONS. |
| 4 | Request never mutated (gap 6) | AC-3, Task 2 tests | Assertion added. |
| 5 | Schema move disclosed (gap 7) | Checkpoint step 1 | The note states that the host and test DBs move to v5, add-only. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Accent folding (gap 8) | Same behaviour as rule patterns; recorded in KNOWN-LIMITATIONS. |
| 2 | Orphaned synonyms after index deletion or rename (gap 9) | Same as rules; harmless (never loaded); cleanup belongs with index lifecycle work. |
| 3 | Query-parser custom factories (gap 10) | Same exposure as the visitor's own query text, which also goes through the factory. Terms come from users with Manage synonyms. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - Unit tests for every validation path, the expansion semantics and the fail-open paths.
  - Database tests for the installer and provider.
  - An opt-in volume test.
  - Live host evidence at the checkpoint.
- **Silent failures:** load, expand and run failures each set a response flag and log a throttled warning. Invalid stored rows are logged by id.
- **Reconstruction:** the Kentico event log records synonym changes. `AppliedSynonyms` on the response shows exactly which substitutions shaped a result.
- **Ownership:** write access comes in 04-02 behind Manage synonyms. No write path exists in this plan beyond the API.

## 6. Final Release Bar

- **Must be true before shipping:**
  - Fail-open covers query building.
  - Boosts are multiplied, not overwritten.
  - The same MatchKey function is used on both sides.
  - Exact matches rank first for identical documents.
  - The live check passes and the temporary rows are deleted.
- **Remaining risks:**
  - Ranking is preferential, not guaranteed.
  - No plural matching and no accent folding.
  - One substitution per variant.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 2 must-have + 5 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
