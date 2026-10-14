import { test } from "node:test";
import assert from "node:assert/strict";
import {
  addDays,
  blockSuggestionReason,
  bucketLabels,
  countDelta,
  dayLabel,
  defaultRange,
  isAllowed,
  itemActionReason,
  itemAriaSort,
  itemSortOf,
  labelEvery,
  lengthOf,
  niceTicks,
  percentileLabel,
  positionDelta,
  presetOf,
  presetRange,
  previousRange,
  rateDelta,
  toDay,
  toPickerDate,
} from "./report.ts";

// Run under several time zones (TZ=America/New_York, TZ=Asia/Tokyo): the results must not change.
const TODAY = "2030-03-01";
const BOUNDS = [10, 25, 50, 100, 250, 500, 1000, 2500, 5000];

test("presets end yesterday and are capped at the retention", () => {
  assert.deepEqual(presetRange(7, TODAY, 762), { from: "2030-02-22", to: "2030-02-28" });
  assert.deepEqual(presetRange(365, TODAY, 762), { from: "2029-03-01", to: "2030-02-28" });
  assert.deepEqual(presetRange(90, TODAY, 30), { from: "2030-01-30", to: "2030-02-28" });
  assert.deepEqual(defaultRange(TODAY, 762), { from: "2030-01-30", to: "2030-02-28" });
});

test("day arithmetic crosses months, leap days and years", () => {
  assert.equal(addDays("2028-02-28", 1), "2028-02-29");
  assert.equal(addDays("2030-01-01", -1), "2029-12-31");
  assert.equal(lengthOf({ from: "2029-12-31", to: "2030-01-01" }), 2);
  assert.deepEqual(previousRange({ from: "2030-03-01", to: "2030-03-31" }), { from: "2030-01-29", to: "2030-02-28" });
  assert.deepEqual(previousRange({ from: "2030-01-01", to: "2030-01-10" }), { from: "2029-12-22", to: "2029-12-31" });
});

test("only ranges the server accepts are allowed", () => {
  assert.equal(isAllowed({ from: "2030-02-01", to: "2030-02-10" }, TODAY, 762), true);
  assert.equal(isAllowed({ from: "2030-03-01", to: "2030-03-01" }, TODAY, 762), true);
  for (const [from, to] of [["2030-02-10", "2030-02-01"], ["2030-02-01", "2030-03-02"], ["2030-02-30", "2030-03-01"], ["1/2/2030", "2030-02-01"], ["2027-01-01", "2030-02-01"]]) {
    assert.equal(isAllowed({ from, to }, TODAY, 762), false, `${from}..${to}`);
  }
});

test("a range is recognized as its preset, otherwise custom", () => {
  assert.equal(presetOf(presetRange(90, TODAY, 762), TODAY, 762), "90");
  assert.equal(presetOf({ from: "2030-02-01", to: "2030-02-10" }, TODAY, 762), "custom");
});

test("picked dates keep their calendar day in any time zone", () => {
  assert.equal(toDay(new Date(2030, 9, 1)), "2030-10-01");
  assert.equal(toDay(new Date(2030, 9, 1, 23, 59)), "2030-10-01");
  assert.equal(toDay(new Date(2030, 9, 1, 0, 1)), "2030-10-01");
  assert.equal(toDay(toPickerDate("2030-10-01")), "2030-10-01");
  assert.equal(dayLabel("2030-10-01", "en-US"), "Oct 1, 2030");
  assert.equal(dayLabel("2030-01-01", "en-US", true), "Jan 1");
});

test("count deltas are relative, rate deltas are percentage points, positions are absolute", () => {
  assert.equal(countDelta(0.15, false, "en-US"), "+15.0 % vs previous period");
  assert.equal(countDelta(-0.5, false, "en-US"), "−50.0 % vs previous period");
  assert.equal(countDelta(0, false, "en-US"), "±0.0 % vs previous period");
  assert.equal(countDelta(null, false), "No previous data");
  assert.equal(countDelta(0.15, true), "No comparison");
  assert.equal(rateDelta(0.33, 0.3, false, "en-US"), "+3.0 pp vs previous period");
  assert.equal(rateDelta(0.3, null, false), "No previous data");
  assert.equal(rateDelta(0.33, 0.3, true), "No comparison");
  assert.equal(positionDelta(2.1, 2.5, false, "en-US"), "−0.4 vs previous period");
});

test("percentiles read as bucket bounds or overflow", () => {
  assert.equal(percentileLabel({ ms: 100, isOverflow: false }, BOUNDS, "en-US"), "≤ 100 ms");
  assert.equal(percentileLabel({ ms: 2500, isOverflow: false }, BOUNDS, "en-US"), "≤ 2.5 s");
  assert.equal(percentileLabel({ ms: null, isOverflow: true }, BOUNDS, "en-US"), "over 5 s");
  assert.equal(percentileLabel(null, BOUNDS), "—");
  const labels = bucketLabels(BOUNDS, "en-US");
  assert.equal(labels.length, 10);
  assert.deepEqual([labels[0], labels[8], labels[9]], ["≤ 10 ms", "≤ 5 s", "> 5 s"]);
});

test("ticks cover the maximum with a few round steps", () => {
  assert.deepEqual(niceTicks(0), [0, 1]);
  assert.deepEqual(niceTicks(1), [0, 1]);
  assert.deepEqual(niceTicks(7), [0, 2, 4, 6, 8]);
  assert.deepEqual(niceTicks(1234), [0, 500, 1000, 1500]);
  assert.equal(labelEvery(365), 46);
  assert.equal(labelEvery(7), 1);
});

test("block suggestion is unavailable for the browse row, placeholder rows and without Manage synonyms", () => {
  const row = { isBrowse: false, hasPlaceholder: false };
  assert.equal(blockSuggestionReason(row, true), null);
  assert.equal(blockSuggestionReason(row, false), "Requires Site Search → Manage synonyms");
  assert.equal(blockSuggestionReason({ ...row, isBrowse: true }, true), "The browse row has no query text");
  assert.equal(blockSuggestionReason({ ...row, hasPlaceholder: true }, true), "Contains a removed value; it is never suggested");
  // The row's own reason wins over the permission.
  assert.equal(blockSuggestionReason({ ...row, hasPlaceholder: true }, false), "Contains a removed value; it is never suggested");
});

test("clicked-item headers map to the server sorts, with aria-sort only on sortable columns", () => {
  assert.equal(itemSortOf("clicked"), "ClickedSearches");
  assert.equal(itemSortOf("conversions"), "Conversions");
  assert.equal(itemSortOf("position"), "AvgPosition");
  assert.equal(itemSortOf("title"), null);
  assert.equal(itemSortOf("actions"), null);

  assert.equal(itemAriaSort("clicked", "ClickedSearches"), "descending");
  assert.equal(itemAriaSort("position", "AvgPosition"), "ascending");
  assert.equal(itemAriaSort("conversions", "AvgPosition"), "none");
  assert.equal(itemAriaSort("title", "ClickedSearches"), null);
});

test("pin and hide are unavailable for an item no longer indexed or without Manage rules", () => {
  assert.equal(itemActionReason("Coffee", true), null);
  assert.equal(itemActionReason("Coffee", false), "Requires Site Search → Manage rules");
  // The item's own reason wins over the permission.
  assert.equal(itemActionReason(null, false), "The item is no longer in this index");
  assert.equal(itemActionReason(null, true), "The item is no longer in this index");
});
