import assert from "node:assert/strict";
import test from "node:test";

import { clickPayload, clip } from "../wwwroot/lucene-suite-results.js";
import { sendClick } from "../wwwroot/lucene-suite.js";

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

/** A link with text content, in a list with a query attribute. */
function textLink(query, text) {
  const l = link(query == null ? validList : { ...validList, "data-lucene-suite-query": query }, validLink);
  return Object.assign(l, { textContent: text });
}

test("clickPayload adds the list query and the trimmed, collapsed link text", () => {
  assert.deepEqual(clickPayload(textLink("coffee grinder", "\n  Burr\t grinder  ")), { searchId, sequence: 3, itemGuid, position: 12, query: "coffee grinder", title: "Burr grinder" });
});

test("clickPayload omits empty or missing query and title", () => {
  assert.deepEqual(clickPayload(textLink(null, null)), { searchId, sequence: 3, itemGuid, position: 12 });
  assert.deepEqual(clickPayload(textLink("", " \n ")), { searchId, sequence: 3, itemGuid, position: 12 });
});

test("clip cuts at 100 UTF-16 units without splitting a pair and drops control characters and lone surrogates", () => {
  assert.equal(clip("x".repeat(150)), "x".repeat(100));
  assert.equal(clip("x".repeat(99) + "😀"), "x".repeat(99));
  assert.equal(clip("😀".repeat(60)), "😀".repeat(50));
  assert.equal(clip("a\u0001b\u007fc\ud800d\udc00e"), "abcde");
});

test("a click with maximal query and title stays within the 1 KB body limit", (t) => {
  const bodies = [];
  const original = Object.getOwnPropertyDescriptor(globalThis, "navigator");
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: { sendBeacon: (_, body) => (bodies.push(body), true) } });
  t.after(() => Object.defineProperty(globalThis, "navigator", original));
  // Worst cases per UTF-16 unit: 3-byte BMP text, 2-byte JSON escapes, 4-byte pairs (2 units).
  for (const unit of ["語", "\"", "\\", "😀", "é"]) {
    const text = unit.repeat(200);
    const payload = clickPayload(Object.assign(link({ ...validList, "data-lucene-suite-sequence": "2147483647" }, { ...validLink, "data-lucene-suite-position": "1000" }), { textContent: text }));
    payload.query = clip(text);
    sendClick("/lucenesuite/events", payload);
  }
  for (const body of bodies) {
    assert.ok(new TextEncoder().encode(body).length <= 1024, `${new TextEncoder().encode(body).length} bytes`);
    assert.ok(JSON.parse(body).title.length > 0);
  }
});
