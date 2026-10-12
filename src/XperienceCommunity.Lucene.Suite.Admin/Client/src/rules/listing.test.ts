import { test } from "node:test";
import assert from "node:assert/strict";
import type { StorageLike } from "../reports/handoff.ts";
import {
  bulkTarget,
  deleteDialogNames,
  initialView,
  loadView,
  pageRange,
  parsePriority,
  partialFailureText,
  rangeText,
  saveView,
  selectionFrom,
  statusLabel,
  viewKey,
  viewReducer,
  type RuleListItem,
  type ViewState,
} from "./listing.ts";

const memory = (): StorageLike & { readonly map: Map<string, string> } => {
  const map = new Map<string, string>();
  return { map, getItem: (k) => map.get(k) ?? null, setItem: (k, v) => void map.set(k, v), removeItem: (k) => void map.delete(k) };
};

const row = (id: number, name = `Rule ${id}`): RuleListItem => ({
  id,
  name,
  priority: id,
  status: "Active",
  validFromUtc: "",
  validUntilUtc: "",
  conditions: "",
  actions: "",
  editPath: `/site-search/indexes/4/rules/${id}`,
  version: `v${id}`,
});

const selectedOnPage3: ViewState = {
  ...initialView,
  page: 3,
  selected: [{ id: 1, name: "a", version: "v1" }],
  allMatching: true,
};

test("search, filter and page size go back to page 1 and clear the selection", () => {
  for (const action of [
    { type: "query", query: "coffee" },
    { type: "status", status: "Active" },
    { type: "pageSize", pageSize: 50 },
    { type: "clearFilters" },
  ] as const) {
    const next = viewReducer(selectedOnPage3, action);
    assert.equal(next.page, 1, action.type);
    assert.deepEqual([next.selected, next.allMatching], [[], false], action.type);
  }
});

test("paging and sorting keep the selection; picking rows leaves select-all-matching", () => {
  assert.deepEqual(viewReducer(selectedOnPage3, { type: "page", page: 4 }).selected, selectedOnPage3.selected);
  const sorted = viewReducer(selectedOnPage3, { type: "sort", sort: "name", descending: true });
  assert.deepEqual([sorted.page, sorted.sort, sorted.descending, sorted.allMatching], [1, "name", true, true]);
  assert.equal(viewReducer(selectedOnPage3, { type: "select", selected: [] }).allMatching, false);
});

test("the selection keeps off-page rules and takes page rules from the page", () => {
  const previous = [{ id: 9, name: "off page", version: "v9" }];
  assert.deepEqual(selectionFrom([9, 2], [row(1), row(2)], previous), [previous[0], { id: 2, name: "Rule 2", version: "v2" }]);
});

test("range text and labels use thousands separators", () => {
  const { from, to } = pageRange(41, 25, 1234);
  assert.equal(rangeText(from, to, 1234, "rules"), "1,001–1,025 of 1,234 rules");
  assert.deepEqual(pageRange(1, 25, 0), { from: 0, to: 0 });
  assert.equal(statusLabel("Active", 1412), "Active 1,412");
  assert.equal(statusLabel("All", undefined), "All");
});

test("the delete dialog lists three names, then the rest as a count", () => {
  assert.deepEqual(deleteDialogNames(["a", "b"], 2), ["a", "b"]);
  assert.deepEqual(deleteDialogNames(["a", "b", "c", "d"], 1200), ["a", "b", "c", "and 1,197 more"]);
  // Select all matching: names come from the page, the count from the server.
  assert.deepEqual(deleteDialogNames(["a"], 120), ["a", "and 119 more"]);
});

test("the target is the rules with their versions, or the filter with the expected count", () => {
  const state = { ...initialView, query: "coffee", status: "Active" as const, selected: [{ id: 3, name: "x", version: "v3" }] };
  assert.deepEqual(bulkTarget(state, 120), { items: [{ id: 3, version: "v3" }] });
  assert.deepEqual(bulkTarget({ ...state, allMatching: true }, 120), { filter: { query: "coffee", status: "Active", expectedCount: 120 } });
});

test("priority must be a whole int, and Set to at least 1", () => {
  assert.equal(parsePriority(" -5 ", "shift"), -5);
  assert.equal(parsePriority("1", "set"), 1);
  for (const text of ["", "1.5", "1e3", "abc", "2147483648", "-2147483649"]) {
    assert.equal(parsePriority(text, "shift"), null, text);
  }
  assert.equal(parsePriority("0", "set"), null);
  assert.equal(parsePriority("2147483647", "shift"), 2147483647);
});

test("a partial failure names the first reason and the count", () => {
  assert.equal(
    partialFailureText("Disable", [{ id: 2, name: "B", reason: "it was changed by someone else" }]),
    "1 rule could not be disabled: it was changed by someone else. Reload and try again.",
  );
  assert.match(partialFailureText("Delete", [{ id: 1, name: "", reason: "not in this index" }, { id: 2, name: "", reason: "x" }])!, /^2 rules could not be deleted: not in this index\./);
  assert.equal(partialFailureText("Enable", []), null);
});

test("the view is saved per index without the selection, and bad state falls back to the defaults", () => {
  const storage = memory();
  saveView("4", { ...selectedOnPage3, query: "coffee", status: "Expired", sort: "name", descending: true, pageSize: 50 }, storage);

  assert.deepEqual(loadView("4", storage), { ...initialView, query: "coffee", status: "Expired", sort: "name", descending: true, page: 3, pageSize: 50 });
  assert.deepEqual(loadView("5", storage), initialView);

  storage.setItem(viewKey("6"), JSON.stringify({ query: "", status: "Live", sort: "name", descending: false, page: 1, pageSize: 25 }));
  storage.setItem(viewKey("7"), JSON.stringify({ query: "", status: "All", sort: "name", descending: false, page: 1, pageSize: 10 }));
  storage.setItem(viewKey("8"), "{not json");
  for (const index of ["6", "7", "8"]) {
    assert.deepEqual(loadView(index, storage), initialView, index);
  }
});
