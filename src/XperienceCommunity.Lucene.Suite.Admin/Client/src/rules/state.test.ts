import { test } from "node:test";
import assert from "node:assert/strict";
import {
  boostAlias,
  boostControlPaths,
  canAdd,
  conditionControlPaths,
  debounce,
  parseBoosts,
  parseConditions,
  partitionErrors,
  removeAt,
  replaceAt,
  serializeBoosts,
  serializeConditions,
} from "./state.ts";

test("conditions round-trip, with Any query omitting anchoring and pattern", () => {
  const json = JSON.stringify([
    { anchoring: "contains", pattern: "coffee", facets: [{ dimension: "Type", value: "Article" }] },
    { contexts: ["vip"] },
    { anchoring: "is", pattern: "" },
  ]);

  const rows = parseConditions(json);

  assert.deepEqual(rows.map((r) => r.anchoring), ["contains", "any", "is"]);
  assert.equal(serializeConditions(rows), json);
});

test("invalid or empty conditions JSON starts an empty editor", () => {
  assert.deepEqual(parseConditions("[{"), []);
  assert.deepEqual(parseConditions(undefined), []);
  assert.deepEqual(parseConditions("{}"), []);
});

test("boosts round-trip; a non-integer strength is sent as typed for the server to report", () => {
  const rows = parseBoosts(JSON.stringify([{ field: "Type", value: "Article", strength: -40 }]));

  assert.deepEqual(rows, [{ field: "Type", value: "Article", strength: "-40" }]);
  assert.equal(serializeBoosts(rows), '[{"field":"Type","value":"Article","strength":-40}]');
  assert.equal(serializeBoosts([{ field: "a", value: "b", strength: "1.5" }]), '[{"field":"a","value":"b","strength":"1.5"}]');
});

test("server errors land on their controls; the rest are listed", () => {
  const rows = parseConditions(JSON.stringify([{ anchoring: "contains", pattern: "", facets: [{ dimension: "d", value: "" }] }]));

  const { byPath, unmatched } = partitionErrors(
    [
      { path: "conditions[0].pattern", message: "Empty." },
      { path: "conditions[0].facets[0].value", message: "Required." },
      { path: "conditions", message: "At most 25 conditions." },
    ],
    conditionControlPaths(rows),
  );

  assert.equal(byPath.get("conditions[0].pattern"), "Empty.");
  assert.equal(byPath.get("conditions[0].facets[0].value"), "Required.");
  assert.deepEqual(unmatched.map((e) => e.path), ["conditions"]);
});

test("a duplicate-boost row error shows on that row's value", () => {
  const rows = parseBoosts('[{"field":"a","value":"b","strength":1},{"field":"a","value":"b","strength":2}]');

  const { byPath, unmatched } = partitionErrors([{ path: "actions.boosts[1]", message: "Already boosted." }], boostControlPaths(rows), boostAlias);

  assert.equal(byPath.get("actions.boosts[1].value"), "Already boosted.");
  assert.equal(unmatched.length, 0);
});

test("list helpers and limits", () => {
  assert.deepEqual(replaceAt(["a", "b"], 1, "c"), ["a", "c"]);
  assert.deepEqual(removeAt(["a", "b", "c"], 1), ["a", "c"]);
  assert.equal(canAdd(24, 25), true);
  assert.equal(canAdd(25, 25), false);
});

test("debounce runs only the last call", async () => {
  const calls: number[] = [];
  const run = debounce((n: number) => calls.push(n), 10);

  run(1);
  run(2);
  await new Promise((resolve) => setTimeout(resolve, 30));

  assert.deepEqual(calls, [2]);
});
