import { test } from "node:test";
import assert from "node:assert/strict";
import {
  adminHref,
  appliedSynonymRows,
  firedRuleRows,
  humanizeFieldName,
  latestRequest,
  mergeSelectedIntoFacets,
  chipIcon,
  pinningRuleName,
  position,
  scoreRatio,
  toggleSelection,
  visibleValues,
} from "./state.ts";

test("selected values missing from the response stay listed with count 0", () => {
  const merged = mergeSelectedIntoFacets(
    [{ dimension: "Category", values: [{ label: "Coffees", count: 8 }] }],
    { Category: ["Coffees", "Grinders"], Processing: ["Washed"] },
  );

  assert.deepEqual(merged, [
    { dimension: "Category", values: [{ label: "Coffees", count: 8 }, { label: "Grinders", count: 0 }] },
    { dimension: "Processing", values: [{ label: "Washed", count: 0 }] },
  ]);
});

test("an errored search (no facets) still lists every selection", () => {
  assert.deepEqual(mergeSelectedIntoFacets([], { A: ["x"] }), [{ dimension: "A", values: [{ label: "x", count: 0 }] }]);
});

test("toggling the last value of a dimension removes the dimension", () => {
  const on = toggleSelection({}, "A", "x", true);
  assert.deepEqual(on, { A: ["x"] });
  assert.deepEqual(toggleSelection(on, "A", "x", false), {});
});

test("collapsed facets show the first values plus selected ones beyond the limit", () => {
  const values = ["a", "b", "c", "d"].map((label) => ({ label, count: 1 }));
  assert.deepEqual(visibleValues(values, ["d"], false, 2).map((v) => v.label), ["a", "b", "d"]);
  assert.equal(visibleValues(values, [], true, 2).length, 4);
});

test("field names are humanized", () => {
  assert.equal(humanizeFieldName("CoffeeProcessing"), "Coffee processing");
  assert.equal(humanizeFieldName("ProductFieldCategory"), "Product field category");
  assert.equal(humanizeFieldName("SKUName"), "SKU name");
  assert.equal(humanizeFieldName("content_type"), "Content type");
});

test("only the latest request is applied, and older ones are aborted", async () => {
  const request = latestRequest();
  const resolvers: ((value: string) => void)[] = [];
  const signals: AbortController[] = [];
  const send = (abort: AbortController) => {
    signals.push(abort);
    return new Promise<string>((resolve) => resolvers.push(resolve));
  };

  const first = request(send);
  const second = request(send);
  resolvers[1]("second");
  resolvers[0]("first");

  assert.deepEqual(await second, { latest: true, value: "second" });
  assert.equal((await first).latest, false);
  assert.equal(signals[0].signal.aborted, true);
  assert.equal(signals[1].signal.aborted, false);
});

test("a failed request resolves as undefined instead of throwing", async () => {
  const result = await latestRequest()(() => Promise.reject(new Error("network")));
  assert.deepEqual(result, { latest: true, value: undefined });
});

test("fired rules link inside the admin root; a deleted rule shows its id without a link", () => {
  const rules = [
    { ruleId: 3, name: "Coffee", editUrl: "/site-search/indexes/4/rules/3", userData: { banner: "x" }, actions: "2 pins, 1 boost", primaryAction: "pin" as const },
    { ruleId: 9, name: null, editUrl: null, userData: null, actions: null, primaryAction: null },
  ];
  assert.deepEqual(firedRuleRows(rules, "/admin/site-search/indexes/4/query-tester"), [
    { ruleId: 3, label: "Coffee", href: "/admin/site-search/indexes/4/rules/3", userData: JSON.stringify({ banner: "x" }, null, 2), summary: "2 pins, 1 boost", icon: "xp-pin" },
    { ruleId: 9, label: "Rule 9", href: null, userData: null, summary: null, icon: "xp-database" },
  ]);
  assert.equal(adminHref("/site-search/indexes/4/rules/3", "/site-search/indexes/4/query-tester"), "/site-search/indexes/4/rules/3");
});

test("pinning rule name: the rule's name, a fallback for a deleted rule, null for an organic hit", () => {
  const rules = [{ ruleId: 3, name: "Coffee", editUrl: null, userData: null }];
  assert.equal(pinningRuleName({ pinnedByRuleId: 3 }, rules), "Coffee");
  assert.equal(pinningRuleName({ pinnedByRuleId: 7 }, rules), "Rule 7");
  assert.equal(pinningRuleName({ pinnedByRuleId: null }, rules), null);
});

test("chip icon follows the rule's primary action", () => {
  assert.equal(chipIcon("pin"), "xp-pin");
  assert.equal(chipIcon("boost"), "xp-arrow-up");
  assert.equal(chipIcon("hide"), "xp-eye-slash");
  assert.equal(chipIcon("data"), "xp-database");
  assert.equal(chipIcon(null), "xp-database");
});

test("positions continue across pages of any size", () => {
  assert.equal(position(1, 10, 0), 1);
  assert.equal(position(3, 25, 4), 55);
  assert.equal(position(2, 50, 49), 100);
});

test("score bars are relative to the top score and never NaN", () => {
  assert.equal(scoreRatio(5, 10), 0.5);
  assert.equal(scoreRatio(10, 10), 1);
  assert.equal(scoreRatio(12, 10), 1);
  assert.equal(scoreRatio(0, 10), 0);
  assert.equal(scoreRatio(-1, 10), 0);
  // A page of pinned hits scored 0 has no top score to compare with.
  assert.equal(scoreRatio(0, 0), 0);
  assert.equal(scoreRatio(5, 0), 0);
  assert.equal(scoreRatio(Number.NaN, 10), 0);
  assert.equal(scoreRatio(5, Number.NaN), 0);
  assert.equal(scoreRatio(Number.POSITIVE_INFINITY, 10), 0);
  assert.equal(scoreRatio(5, Number.POSITIVE_INFINITY), 0);
});

test("applied synonyms link inside the admin root; a deleted synonym has no link", () => {
  const rows = appliedSynonymRows(
    [
      { synonymId: 4, term: "mill", replacement: "grinder", editUrl: "/site-search/indexes/4/synonyms/4" },
      { synonymId: 9, term: "tee", replacement: "t-shirt", editUrl: null },
    ],
    "/admin/site-search/indexes/4/query-tester",
  );
  assert.deepEqual(rows, [
    { key: "4-0", label: "mill → grinder", href: "/admin/site-search/indexes/4/synonyms/4" },
    { key: "9-1", label: "tee → t-shirt", href: null },
  ]);
});
