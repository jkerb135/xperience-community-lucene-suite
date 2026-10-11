import { test } from "node:test";
import assert from "node:assert/strict";
import { addTerm, commitPending, focusAfterRemove, matchKey, parseTerms, serializeTerms, termErrors } from "./state.ts";

// Shared with SynonymEngineTests.MatchKey_SharedVectors: the duplicate check must agree with the server.
test("matchKey agrees with the server on the shared vectors", () => {
  const vectors: Array<[string, string]> = [
    ["  Coffee   GRINDER ", "coffee grinder"],
    ['"T-Shirt,"', "t-shirt"],
    ["...!!!", ""],
    ["V60-01 filters.", "v60-01 filters"],
    ["Café CRÈME", "café crème"],
    ["a - b", "a b"],
  ];
  for (const [text, expected] of vectors) {
    assert.equal(matchKey(text), expected, text);
  }
});

test("terms round-trip; malformed JSON reads as empty", () => {
  assert.deepEqual(parseTerms('["grinder","mill"]'), ["grinder", "mill"]);
  assert.deepEqual(parseTerms("["), []);
  assert.deepEqual(parseTerms(undefined), []);
  assert.equal(serializeTerms(["café"]), '["café"]');
  assert.equal(serializeTerms([]), "");
});

test("addTerm refuses empty, punctuation-only, too long, duplicate and over-limit terms", () => {
  assert.deepEqual(addTerm(["grinder"], "  Burr   mill ", 20, 100), { terms: ["grinder", "Burr mill"] });
  assert.ok("error" in addTerm([], "   ", 20, 100));
  assert.ok("error" in addTerm([], "!!!", 20, 100));
  assert.ok("error" in addTerm([], "x".repeat(101), 20, 100));
  assert.deepEqual(addTerm(["grinder"], "Grinder.", 20, 100), { error: '"Grinder." is already a term.' });
  assert.deepEqual(addTerm(["a", "b"], "c", 2, 100), { error: "At most 2 terms." });
});

test("commitPending adds a valid typed term, keeps an invalid one with its error, ignores empty", () => {
  assert.deepEqual(commitPending(["grinder"], "mill", 20, 100), { terms: ["grinder", "mill"], text: "", error: undefined });
  assert.deepEqual(commitPending(["grinder"], "GRINDER", 20, 100), { terms: ["grinder"], text: "GRINDER", error: '"GRINDER" is already a term.' });
  assert.deepEqual(commitPending(["grinder"], "  ", 20, 100), { terms: ["grinder"], text: "", error: undefined });
});

test("focus after removing a chip goes to the next, else the previous, else the input", () => {
  assert.equal(focusAfterRemove(0, 3), 0);
  assert.equal(focusAfterRemove(2, 3), 1);
  assert.equal(focusAfterRemove(0, 1), "input");
});

test("server errors map to their term; list-level errors stay unmatched", () => {
  const { byIndex, unmatched } = termErrors([
    { path: "terms[1]", message: "Duplicate term." },
    { path: "terms", message: "Must have 2–20 terms." },
  ]);
  assert.equal(byIndex.get(1), "Duplicate term.");
  assert.deepEqual(unmatched.map((error) => error.path), ["terms"]);
});
