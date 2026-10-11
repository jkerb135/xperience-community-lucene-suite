import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import {
  createSearchTracker, formatResultReference, newGuid, osa, parseResultReference, sendClick, sendConversion,
} from "../wwwroot/lucene-suite.js";

const vectors = JSON.parse(readFileSync(new URL("../../../docs/analytics-test-vectors.json", import.meta.url), "utf8"));

for (const testCase of vectors.cases) {
  test(`vector: ${testCase.name}`, () => {
    const tracker = createSearchTracker({ ...vectors.settings, ...testCase.settings });
    const ordinals = new Map();
    let latest = 0;
    const sent = new Set();
    testCase.steps.forEach((step, i) => {
      if (step.click) {
        assert.ok(sent.has(step.click.seq ?? latest), `step ${i + 1}: clicked response was sent`);
        return;
      }
      const { searchId, sequence, isSubmit } = tracker.beforeSend(step.q, step.submit ?? false, step.t);
      if (!ordinals.has(searchId)) {
        ordinals.set(searchId, ordinals.size + 1);
        sent.clear();
      }
      latest = sequence;
      sent.add(sequence);
      assert.equal(isSubmit, step.submit ?? false);
      assert.deepEqual({ search: ordinals.get(searchId), seq: sequence }, step.expect, `step ${i + 1} (${JSON.stringify(step.q)})`);
    });
  });
}

test("osa counts a transposition as one edit", () => {
  assert.deepEqual([osa("gridner", "grinder"), osa("ipad", "imac"), osa("", "abc"), osa("kitten", "sitting")], [1, 2, 3, 3]);
});

test("newGuid is a random version 4 GUID", () => {
  const a = newGuid();
  assert.match(a, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  assert.notEqual(a, newGuid());
});

test("result reference round trips and matches the server format", () => {
  const id = "8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b";
  const value = formatResultReference(id, 4, 1000);
  assert.equal(value, "8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.4.1000");
  assert.deepEqual(parseResultReference(value), { searchId: id, sequence: 4, position: 1000 });
});

test("result reference rejects what the server rejects", () => {
  const g = "8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b";
  for (const value of [null, undefined, "", `${g}.4`, `${g}.4.1.1`, "8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b.4.1", `${"z".repeat(32)}.4.1`,
    `${"0".repeat(32)}.4.1`, `${g}.0.1`, `${g}.-1.1`, `${g}.+1.1`, `${g}.1.0`, `${g}.1.1001`, `${g}.1. 1`, `${g}.1.99999999999`,
    `${g}.99999999999.1`, `${g}.1.${"0".repeat(29)}1`]) {
    assert.equal(parseResultReference(value), null, String(value));
  }
});

test("events use sendBeacon, fall back to fetch, and keep a given eventId", (t) => {
  const beacons = [], fetches = [];
  let accept = true;
  const original = Object.getOwnPropertyDescriptor(globalThis, "navigator");
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: { sendBeacon: (url, body) => (beacons.push([url, JSON.parse(body)]), accept) } });
  t.after(() => Object.defineProperty(globalThis, "navigator", original));
  t.mock.method(globalThis, "fetch", (url, init) => (fetches.push([url, init]), Promise.resolve(new Response(null, { status: 202 }))));
  const e = { searchId: newGuid(), sequence: 2, itemGuid: newGuid(), position: 3 };

  const clickId = sendClick("/lucenesuite/events", e);
  accept = false;
  const conversionId = sendConversion("/lucenesuite/events", { ...e, position: undefined, eventId: clickId });

  assert.deepEqual(beacons[0], ["/lucenesuite/events", { ...e, type: "click", eventId: clickId }]);
  assert.equal(conversionId, clickId);
  assert.equal(fetches.length, 1);
  const [url, init] = fetches[0];
  assert.equal(url, "/lucenesuite/events");
  assert.equal(init.keepalive, true);
  assert.deepEqual(JSON.parse(init.body), { searchId: e.searchId, sequence: 2, itemGuid: e.itemGuid, type: "conversion", eventId: clickId });
});
