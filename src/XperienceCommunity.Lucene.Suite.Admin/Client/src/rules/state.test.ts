import { test } from "node:test";
import assert from "node:assert/strict";
import {
  boostAlias,
  boostControlPaths,
  canAdd,
  conditionControlPaths,
  conditionsWarning,
  debounce,
  isShown,
  parseBoosts,
  parseConditions,
  partitionErrors,
  removeAt,
  replaceAt,
  serializeBoosts,
  serializeConditions,
  addHides,
  addPins,
  fieldValue,
  hideControlPaths,
  nextFreePositions,
  parseHides,
  parsePins,
  pickerRow,
  pinAlias,
  pinControlPaths,
  serializeHides,
  serializePins,
  toggleItem,
  TYPED,
  contextChoice,
  contextFromSelect,
  contextOptions,
  contextSelectValue,
  dimensionOptions,
  facetChoice,
  facetFromSelect,
  facetSelectValue,
  facetValueOptions,
  statusArguments,
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

test("next free positions fill gaps first", () => {
  assert.deepEqual(nextFreePositions([1, 3], 2), [2, 4]);
  assert.deepEqual(nextFreePositions([], 3), [1, 2, 3]);
  assert.deepEqual(nextFreePositions([2], 0), []);
});

test("addPins appends new items at free positions and skips pinned or repeated ones", () => {
  const rows = parsePins(JSON.stringify([{ itemGuid: "AAA", position: 1 }, { itemGuid: "bbb", position: 3 }]));
  const next = addPins(rows, ["aaa", "ccc", "ddd", "CCC"]);
  assert.deepEqual(next.slice(2), [
    { itemGuid: "ccc", position: "2" },
    { itemGuid: "ddd", position: "4" },
  ]);
  assert.equal(serializePins(next), '[{"itemGuid":"AAA","position":1},{"itemGuid":"bbb","position":3},{"itemGuid":"ccc","position":2},{"itemGuid":"ddd","position":4}]');
});

test("pins keep a non-integer position as typed; hides round-trip", () => {
  assert.equal(serializePins([{ itemGuid: "a", position: "x" }]), '[{"itemGuid":"a","position":"x"}]');
  assert.deepEqual(addHides(parseHides('["a"]'), ["A", "b", "b"]), ["a", "b"]);
  assert.equal(serializeHides(["a", "b"]), '["a","b"]');
});

test("picker rows: added items are checked and disabled, the other list's items are disabled", () => {
  const added = new Set(["a"]);
  const excluded = new Set(["b"]);
  const selected = toggleItem(new Set(), "C", true);
  assert.deepEqual(pickerRow("A", selected, added, excluded, "Pinned"), { checked: true, disabled: true, note: "Already added" });
  assert.deepEqual(pickerRow("b", selected, added, excluded, "Pinned"), { checked: false, disabled: true, note: "Pinned" });
  assert.deepEqual(pickerRow("c", selected, added, excluded, "Pinned"), { checked: true, disabled: false, note: undefined });
  assert.equal(toggleItem(selected, "c", false).size, 0);
});

test("pin and hide error paths map to their controls", () => {
  const rows = parsePins('[{"itemGuid":"a","position":1}]');
  const { byPath, unmatched } = partitionErrors(
    [
      { path: "actions.pins[0]", message: "Must be an object." },
      { path: "actions.pins[0].position", message: "Must be 1–300." },
      { path: "actions.pins", message: "At most 300 pins." },
    ],
    pinControlPaths(rows),
    pinAlias,
  );
  assert.equal(byPath.get("actions.pins[0].itemGuid"), "Must be an object.");
  assert.equal(byPath.get("actions.pins[0].position"), "Must be 1–300.");
  assert.equal(unmatched.length, 1);
  assert.deepEqual(hideControlPaths(["a", "b"]), ["actions.hides[0]", "actions.hides[1]"]);
});

test("fieldValue reads another field regardless of key casing", () => {
  assert.equal(fieldValue({ pins: "[]" }, "Pins"), "[]");
  assert.equal(fieldValue({ Pins: 1 }, "Pins"), undefined);
  assert.equal(fieldValue(undefined, "Pins"), undefined);
});

test("errors stay hidden until the control or its group was left, or a save failed", () => {
  const none = new Set<string>();
  assert.equal(isShown("conditions[0].pattern", none, false), false);
  assert.equal(isShown("conditions[0].pattern", new Set(["conditions[0].pattern"]), false), true);
  assert.equal(isShown("conditions[0].facets", new Set(["conditions[0]"]), false), true);
  assert.equal(isShown("conditions[1].facets", new Set(["conditions[0]"]), false), false);
  assert.equal(isShown("conditions[10].pattern", new Set(["conditions[1]"]), false), false);
  assert.equal(isShown("anything", new Set([""]), false), true);
  assert.equal(isShown("anything", none, true), true);
});

const groups = [
  { name: "VIPs", displayName: "VIP customers" },
  { name: "Gold", displayName: "Gold members" },
];
const facets = [{ dimension: "ContentType", values: [{ label: "Article", count: 12 }, { label: "Cafe", count: 3 }] }];

test("stored contexts show as their group or as a custom context, and map back to the identical string", () => {
  assert.equal(contextChoice("VIPs", groups, false), "group");
  assert.equal(contextChoice("mobile", groups, false), "custom");
  assert.equal(contextChoice("", groups, false), "none");
  assert.equal(contextChoice("", groups, true), "custom");
  assert.equal(contextChoice("vips", groups, false), "custom", "code names match exactly, as at runtime");

  for (const stored of ["VIPs", "mobile", "Gold"]) {
    const shown = contextSelectValue(stored, groups, false);
    assert.deepEqual(contextFromSelect(shown, stored, groups).value, stored, stored);
  }
  // Before the groups arrive every stored value is custom, and still maps back unchanged.
  assert.equal(contextFromSelect(contextSelectValue("VIPs", [], false), "VIPs", []).value, "VIPs");
});

test("choosing a context: a group stores its code name, Custom context keeps a typed value and starts empty from a group", () => {
  assert.deepEqual(contextFromSelect("Gold", "VIPs", groups), { value: "Gold", typed: false });
  assert.deepEqual(contextFromSelect(TYPED, "VIPs", groups), { value: "", typed: true });
  assert.deepEqual(contextFromSelect(TYPED, "mobile", groups), { value: "mobile", typed: true });
  assert.deepEqual(
    contextOptions(groups).map((o) => [o.value, o.primary, o.secondary]),
    [
      ["VIPs", "VIP customers", "Contact group"],
      ["Gold", "Gold members", "Contact group"],
      [TYPED, "Custom context…", undefined],
    ],
  );
});

test("stored facet values show as listed or stale, and map back to the identical string", () => {
  assert.equal(facetChoice("ContentType", "Article", facets, false), "listed");
  assert.equal(facetChoice("ContentType", "Podcast", facets, false), "stale");
  assert.equal(facetChoice("Gone", "Article", facets, false), "stale");
  assert.equal(facetChoice("ContentType", "", facets, false), "none");
  assert.equal(facetChoice("ContentType", "Article", facets, true), "other");

  for (const [dimension, value] of [["ContentType", "Article"], ["ContentType", "Podcast"], ["Gone", "x"]]) {
    const shown = facetSelectValue(dimension, value, facets, false);
    assert.equal(facetFromSelect(shown, dimension, value, facets).value, value, `${dimension}=${value}`);
  }
});

test("facet options: values with counts, a stale value labelled, Other value last; nothing is stale while loading", () => {
  assert.deepEqual(
    facetValueOptions("ContentType", "Podcast", facets, false).map((o) => [o.value, o.secondary]),
    [
      ["Article", "12"],
      ["Cafe", "3"],
      ["Podcast", "not in the index"],
      [TYPED, undefined],
    ],
  );
  assert.deepEqual(facetValueOptions("ContentType", "Article", facets, false).map((o) => o.value), ["Article", "Cafe", TYPED]);
  assert.deepEqual(facetValueOptions("ContentType", "Podcast", undefined, false).map((o) => [o.value, o.secondary]), [["Podcast", undefined], [TYPED, undefined]]);
  assert.deepEqual(dimensionOptions("Gone", facets).map((o) => [o.value, o.secondary]), [["ContentType", undefined], ["Gone", "not in the index"]]);
  assert.deepEqual(dimensionOptions("Gone", undefined).map((o) => [o.value, o.secondary]), [["Gone", undefined]]);
  assert.deepEqual(facetFromSelect(TYPED, "ContentType", "Article", facets), { value: "", typed: true });
  assert.deepEqual(facetFromSelect(TYPED, "ContentType", "Podcast", facets), { value: "Podcast", typed: true });
});

test("the option sentinel can never be a stored context or facet value (the server rejects whitespace-only)", () => {
  assert.equal(TYPED.trim(), "");
});

test("status arguments carry picked dates as UTC instants and server strings untouched, whatever the time zone", () => {
  // Run under TZ=America/New_York too: neither value may shift by the browser's offset.
  const picked = new Date(Date.UTC(2026, 9, 13, 0, 0));
  assert.deepEqual(statusArguments({ enabled: true, validFromUtc: picked, ValidUntilUtc: "2026-10-20T00:00:00" }), {
    enabled: true,
    validFromUtc: "2026-10-13T00:00:00.000Z",
    validUntilUtc: "2026-10-20T00:00:00",
  });
  assert.deepEqual(statusArguments({ Enabled: false, ValidFromUtc: "", ValidUntilUtc: null }), { enabled: false, validFromUtc: null, validUntilUtc: null });
  assert.deepEqual(statusArguments(undefined), { enabled: false, validFromUtc: null, validUntilUtc: null });
  assert.equal(statusArguments({ validFromUtc: new Date(NaN) }).validFromUtc, null);
});

test("the conditions section warns exactly while the rule has no condition", () => {
  assert.equal(conditionsWarning([]), "No conditions: this rule applies to every search.");
  assert.equal(conditionsWarning(parseConditions(undefined)), "No conditions: this rule applies to every search.");
  assert.equal(conditionsWarning(parseConditions("[]")), "No conditions: this rule applies to every search.");
  assert.equal(conditionsWarning(parseConditions('[{"anchoring":"any"}]')), null);
  assert.equal(conditionsWarning(parseConditions('[{"anchoring":"is","pattern":"tea"}]')), null);
});
