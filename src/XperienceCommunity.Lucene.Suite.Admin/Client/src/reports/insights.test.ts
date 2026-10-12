import { test } from "node:test";
import assert from "node:assert/strict";
import { KINDS, evidence, fixLinks, groupByKind, kindInfo, summary, topFindings, type InsightRow, type InsightsResult } from "./insights.ts";

const BOUNDS = [50, 100, 200, 300, 500, 750, 1000, 1500, 2000];

const row = (overrides: Partial<InsightRow>): InsightRow => ({
  kind: "ContentGap",
  queryText: "q",
  hasPlaceholder: false,
  searches: 120,
  affected: 41,
  noResultRate: 41 / 120,
  ctr: 0.2,
  avgClickPosition: 7.2,
  latencyP95: { ms: 2000, isOverflow: false },
  previousSearches: 100,
  previousNoResultRate: 0.12,
  previousCtr: 0.4,
  appliedSynonyms: [],
  firedRules: [],
  ...overrides,
});

test("every kind has a label, an explanation and the planned action set", () => {
  assert.deepEqual(
    KINDS.map((kind) => [kind, kindInfo(kind).label, kindInfo(kind).actions]),
    [
      ["ContentGap", "No results", ["synonym", "rule", "tester"]],
      ["RelevanceMiss", "Results not clicked", ["rule", "tester", "block"]],
      ["BuriedAnswer", "Answer buried", ["rule", "tester"]],
      ["SlowQuery", "Slow", ["tester"]],
      ["Regression", "Got worse", ["tester", "rule", "synonym"]],
    ],
  );
  for (const kind of KINDS) {
    assert.match(kindInfo(kind).explanation, /\.$/);
  }
});

test("evidence is one line per kind", () => {
  assert.equal(evidence(row({ kind: "ContentGap" }), BOUNDS, "en-US"), "41 of 120 searches had no results (34.2%)");
  assert.equal(evidence(row({ kind: "RelevanceMiss", affected: 90 }), BOUNDS, "en-US"), "90 of 120 searches found results but got no click (CTR 20.0%)");
  assert.equal(evidence(row({ kind: "BuriedAnswer", affected: 30 }), BOUNDS, "en-US"), "Clicked on average at position 7.2 in 30 searches");
  assert.equal(evidence(row({ kind: "SlowQuery", affected: 120 }), BOUNDS, "en-US"), "120 of 120 searches were slow; p95 ≤ 2 s");
  assert.equal(evidence(row({ kind: "SlowQuery", latencyP95: { ms: null, isOverflow: true } }), BOUNDS, "en-US"), "41 of 120 searches were slow; p95 over 2 s");
  assert.equal(evidence(row({ kind: "Regression", noResultRate: 0.31 }), BOUNDS, "en-US"), "No-result rate 12.0% → 31.0% · CTR 40.0% → 20.0%");
  assert.equal(evidence(row({ kind: "Regression", noResultRate: 0.1, ctr: 0.2 }), BOUNDS, "en-US"), "CTR 40.0% → 20.0%");
  assert.equal(evidence(row({ kind: "Regression", noResultRate: 0.31, ctr: 0.5 }), BOUNDS, "en-US"), "No-result rate 12.0% → 31.0%");
});

test("evidence with null rates shows a dash or skips the comparison", () => {
  assert.equal(evidence(row({ kind: "ContentGap", noResultRate: null }), BOUNDS, "en-US"), "41 of 120 searches had no results (—)");
  assert.equal(evidence(row({ kind: "BuriedAnswer", avgClickPosition: null }), BOUNDS, "en-US"), "Clicked on average at position — in 41 searches");
  assert.equal(evidence(row({ kind: "SlowQuery", latencyP95: null }), BOUNDS, "en-US"), "41 of 120 searches were slow; p95 —");
  assert.equal(
    evidence(row({ kind: "Regression", previousNoResultRate: null, previousCtr: null }), BOUNDS, "en-US"),
    "Worse than in the previous period",
  );
});

test("groupByKind keeps the fixed kind order, server order within a kind, and drops empty kinds", () => {
  const rows = [
    row({ kind: "Regression", queryText: "a" }),
    row({ kind: "SlowQuery", queryText: "b" }),
    row({ kind: "ContentGap", queryText: "c" }),
    row({ kind: "Regression", queryText: "d" }),
  ];
  assert.deepEqual(
    groupByKind(rows).map((group) => [group.kind, group.rows.map((r) => r.queryText)]),
    [
      ["ContentGap", ["c"]],
      ["SlowQuery", ["b"]],
      ["Regression", ["a", "d"]],
    ],
  );
  assert.deepEqual(groupByKind([]), []);
});

test("topFindings keeps the first five in server order", () => {
  const rows = Array.from({ length: 7 }, (_, i) => row({ queryText: `q${i}` }));
  assert.deepEqual(topFindings(rows, 5).map((r) => r.queryText), ["q0", "q1", "q2", "q3", "q4"]);
  assert.equal(topFindings(rows.slice(0, 2), 5).length, 2);
});

test("fixLinks lists synonyms, numbered when several, then rules by name", () => {
  assert.deepEqual(fixLinks(row({})), []);
  assert.deepEqual(
    fixLinks(row({ appliedSynonyms: [{ id: 3, editUrl: "s/3" }], firedRules: [{ id: 9, name: "Grinders", editUrl: "r/9" }] })).map((l) => [l.key, l.label, l.editUrl]),
    [
      ["synonym-3", "Synonym", "s/3"],
      ["rule-9", 'Rule "Grinders"', "r/9"],
    ],
  );
  assert.deepEqual(
    fixLinks(row({ appliedSynonyms: [{ id: 3, editUrl: "s/3" }, { id: 4, editUrl: "s/4" }] })).map((l) => l.label),
    ["Synonym 1", "Synonym 2"],
  );
});

test("summary counts all findings and notes the caps", () => {
  const result = (overrides: Partial<InsightsResult>): InsightsResult => ({
    findings: [row({})],
    omitted: 0,
    considered: 40,
    truncated: false,
    previousIsPartial: false,
    annotationsFailed: false,
    ...overrides,
  });
  assert.equal(summary(result({}), "en-US"), "1 finding · 40 queries checked");
  assert.equal(summary(result({ omitted: 1200, considered: 1, truncated: true }), "en-US"), "1,201 findings · 1 most searched query checked · showing the top 1");
});
