import { test } from "node:test";
import assert from "node:assert/strict";
import { addContext, contextLabel, defaultSides, markerLabel, missingFrom, rankChanges, withGroups } from "./compare.ts";

const hits = (...guids: Array<string | null>) => guids.map((itemGuid) => ({ itemGuid }));

test("defaults compare an untuned baseline with a tuned column, no contexts", () => {
  assert.deepEqual(defaultSides(), {
    baseline: { applyRules: false, applySynonyms: false, contexts: [] },
    tuned: { applyRules: true, applySynonyms: true, contexts: [] },
  });
});

test("rank changes: up, down, new and unchanged", () => {
  assert.deepEqual(rankChanges(hits("a", "b", "c"), hits("c", "a", "d", "b")), [
    { kind: "up", by: 2 },
    { kind: "down", by: 1 },
    { kind: "new" },
    { kind: "down", by: 2 },
  ]);
  assert.deepEqual(rankChanges(hits("a", "b"), hits("a", "b")), [{ kind: "same" }, { kind: "same" }]);
});

test("multilingual duplicates neither shift ranks nor count as new; hits without an ItemGuid get no marker", () => {
  // Baseline returns a's two language documents; Tuned returns them too, plus a pinned b first.
  assert.deepEqual(rankChanges(hits("a", "A", "c"), hits("b", "a", "A", "c", null)), [
    { kind: "new" },
    { kind: "down", by: 1 },
    { kind: "same" },
    { kind: "down", by: 1 },
    { kind: "same" },
  ]);
});

test("missing lists Baseline items Tuned did not return, once each", () => {
  assert.deepEqual(missingFrom(hits("a", "x", "X", "b", null), hits("b", "a")), hits("x"));
});

test("addContext trims, refuses empty, too long, duplicate and over-limit contexts", () => {
  assert.deepEqual(addContext(["vip"], "  mobile ", 50, 100), { contexts: ["vip", "mobile"] });
  assert.ok("error" in addContext([], "  ", 50, 100));
  assert.ok("error" in addContext([], "x".repeat(101), 50, 100));
  assert.deepEqual(addContext(["vip"], "vip", 50, 100), { error: '"vip" is already a context.' });
  assert.deepEqual(addContext(["vip"], "VIP", 50, 100), { contexts: ["vip", "VIP"] });
  assert.deepEqual(addContext(["a"], "b", 1, 100), { error: "At most 1 contexts." });
});

test("contact groups merge with free text by code name; chips show display names", () => {
  const groups = [{ name: "Coffee_lovers", displayName: "Coffee lovers" }, { name: "VIP", displayName: "VIP customers" }];
  assert.deepEqual(withGroups(["mobile", "VIP"], groups, ["Coffee_lovers"]), ["mobile", "Coffee_lovers"]);
  assert.equal(contextLabel("Coffee_lovers", groups), "Coffee lovers");
  assert.equal(contextLabel("mobile", groups), "mobile");
});

test("marker labels are readable and announced", () => {
  assert.deepEqual(markerLabel({ kind: "up", by: 1 }), { text: "1", hidden: "1 place up" });
  assert.deepEqual(markerLabel({ kind: "down", by: 3 }), { text: "3", hidden: "3 places down" });
  assert.deepEqual(markerLabel({ kind: "new" }), { text: "New", hidden: "Not in Baseline" });
  assert.deepEqual(markerLabel({ kind: "same" }), { text: "—", hidden: "Unchanged" });
});
