import { test } from "node:test";
import assert from "node:assert/strict";
import { humanizeFieldName, latestRequest, mergeSelectedIntoFacets, toggleSelection, visibleValues } from "./state.ts";

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
