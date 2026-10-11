import assert from "node:assert/strict";
import test from "node:test";

import { clickPayload } from "../wwwroot/lucene-suite-results.js";

const searchId = "8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b";
const itemGuid = "1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b";

/** A result link inside a results list, with only the DOM members clickPayload uses. */
function link(listAttributes, linkAttributes) {
  const list = { getAttribute: (name) => listAttributes[name] ?? null };
  return {
    getAttribute: (name) => linkAttributes[name] ?? null,
    closest: (selector) => (selector === "[data-lucene-suite-results]" && listAttributes ? list : null),
  };
}

const validList = { "data-lucene-suite-search": searchId, "data-lucene-suite-sequence": "3" };
const validLink = { "data-lucene-suite-item": itemGuid, "data-lucene-suite-position": "12" };

test("clickPayload reads the list and link attributes", () => {
  assert.deepEqual(clickPayload(link(validList, validLink)), { searchId, sequence: 3, itemGuid, position: 12 });
});

test("clickPayload rejects missing or invalid attributes", () => {
  const cases = [
    [null, validLink],
    [{ ...validList, "data-lucene-suite-search": "nope" }, validLink],
    [{ ...validList, "data-lucene-suite-sequence": "0" }, validLink],
    [{ ...validList, "data-lucene-suite-sequence": "1.5" }, validLink],
    [validList, { ...validLink, "data-lucene-suite-item": undefined }],
    [validList, { ...validLink, "data-lucene-suite-position": "0" }],
    [validList, { ...validLink, "data-lucene-suite-position": "1001" }],
    [validList, { ...validLink, "data-lucene-suite-position": "-1" }],
    [validList, { ...validLink, "data-lucene-suite-position": "3e2" }],
  ];
  for (const [list, attributes] of cases) {
    assert.equal(clickPayload(link(list, attributes)), null, JSON.stringify([list, attributes]));
  }
});
