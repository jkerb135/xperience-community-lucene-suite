import { test } from "node:test";
import assert from "node:assert/strict";
import { HANDOFF_LIFETIME_MS, give, indexFromPath, loadReportState, saveReportState, take, type StorageLike } from "./handoff.ts";

const memory = (): StorageLike & { readonly map: Map<string, string> } => {
  const map = new Map<string, string>();
  return { map, getItem: (k) => map.get(k) ?? null, setItem: (k, v) => void map.set(k, v), removeItem: (k) => void map.delete(k) };
};

const CREATE = "/site-search/indexes/4/rules/create";
const AT_CREATE = `/admin${CREATE}`;
const NOW = 1_000_000;

test("a handoff reaches its target page once", () => {
  const storage = memory();
  give("ruleCreate", CREATE, "pizza delivery", NOW, storage);

  assert.equal(take("ruleCreate", AT_CREATE, NOW + 1000, storage), "pizza delivery");
  assert.equal(take("ruleCreate", AT_CREATE, NOW + 1000, storage), null);
});

test("another target, another index or an edit page never takes it, and it stays for its own target", () => {
  const storage = memory();
  give("ruleCreate", CREATE, "pizza", NOW, storage);

  assert.equal(take("synonymCreate", "/admin/site-search/indexes/4/synonyms/create", NOW, storage), null);
  assert.equal(take("ruleCreate", "/admin/site-search/indexes/5/rules/create", NOW, storage), null);
  // An existing rule's edit page, even one whose conditions are empty.
  assert.equal(take("ruleCreate", "/admin/site-search/indexes/4/rules/12", NOW, storage), null);
  assert.equal(take("ruleCreate", AT_CREATE, NOW, storage), "pizza");
});

test("an item reaches the pin or hide consumer of its create page once", () => {
  const storage = memory();
  const guid = "0A1B2C3D-0000-4000-8000-00000000000A";
  give("rulePin", CREATE, guid, NOW, storage);

  assert.equal(take("ruleHide", AT_CREATE, NOW, storage), null);
  assert.equal(take("ruleCreate", AT_CREATE, NOW, storage), null);
  assert.equal(take("rulePin", "/admin/site-search/indexes/4/rules/12", NOW, storage), null);
  assert.equal(take("rulePin", AT_CREATE, NOW, storage), guid);
  assert.equal(take("rulePin", AT_CREATE, NOW, storage), null);

  give("ruleHide", CREATE, guid, NOW, storage);
  assert.equal(take("ruleHide", AT_CREATE, NOW, storage), guid);
});

test("an item handoff without a GUID is refused on read", () => {
  const storage = memory();
  for (const payload of ["pizza", "", "0a1b2c3d-0000-4000-8000-00000000000a; drop", "{0a1b2c3d-0000-4000-8000-00000000000a}"]) {
    give("rulePin", CREATE, payload, NOW, storage);
    assert.equal(take("rulePin", AT_CREATE, NOW, storage), null, payload);
    give("ruleHide", CREATE, payload, NOW, storage);
    assert.equal(take("ruleHide", AT_CREATE, NOW, storage), null, payload);
  }
});

test("an expired handoff is discarded unused", () => {
  const storage = memory();
  give("queryTester", "/site-search/indexes/4/query-tester", "tea", NOW, storage);

  assert.equal(take("queryTester", "/admin/site-search/indexes/4/query-tester", NOW + HANDOFF_LIFETIME_MS + 1, storage), null);
  assert.equal(storage.map.size, 0);
});

test("garbage or a throwing storage never breaks the page", () => {
  const storage = memory();
  storage.setItem("lucene-suite:handoff", "{not json");
  assert.equal(take("ruleCreate", AT_CREATE, NOW, storage), null);

  const throwing: StorageLike = {
    getItem: () => {
      throw new Error("blocked");
    },
    setItem: () => {
      throw new Error("blocked");
    },
    removeItem: () => {
      throw new Error("blocked");
    },
  };
  assert.doesNotThrow(() => give("ruleCreate", CREATE, "x", NOW, throwing));
  assert.equal(take("ruleCreate", AT_CREATE, NOW, throwing), null);
  assert.equal(take("ruleCreate", AT_CREATE, NOW, null), null);
  assert.equal(loadReportState("4", throwing), null);
});

test("the index comes from the admin path", () => {
  assert.equal(indexFromPath("/admin/site-search/indexes/4/reports"), "4");
  assert.equal(indexFromPath("/admin/site-search/indexes/12"), "12");
  assert.equal(indexFromPath("/admin/site-search/indexes"), null);
  assert.equal(indexFromPath("/admin/lucene/indexes/4/edit"), null);
});

test("report state is kept per index and rejected when malformed", () => {
  const storage = memory();
  saveReportState("4", { from: "2030-02-01", to: "2030-02-10", preset: "custom", sort: "NoResults", page: 2 }, storage);

  assert.deepEqual(loadReportState("4", storage), { from: "2030-02-01", to: "2030-02-10", preset: "custom", sort: "NoResults", page: 2 });
  assert.equal(loadReportState("5", storage), null);
  storage.setItem("lucene-suite:reports:4", JSON.stringify({ from: "2030-02-01", to: "2030-02-10", preset: "custom", sort: "Searches", page: 0 }));
  assert.equal(loadReportState("4", storage), null);
  assert.equal(loadReportState(null, storage), null);
});

test("a saved page size is kept only when the query table offers it", () => {
  const storage = memory();
  const state = { from: "2030-02-01", to: "2030-02-10", preset: "custom", sort: "Searches", page: 1 };
  saveReportState("4", { ...state, pageSize: 50 }, storage);
  assert.deepEqual(loadReportState("4", storage), { ...state, pageSize: 50 });

  for (const pageSize of [7, "25", 0]) {
    storage.setItem("lucene-suite:reports:4", JSON.stringify({ ...state, pageSize }));
    assert.deepEqual(loadReportState("4", storage), state);
  }
});
