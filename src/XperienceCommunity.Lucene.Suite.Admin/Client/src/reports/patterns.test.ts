import { test } from "node:test";
import assert from "node:assert/strict";
import { barRatio, sparkPoints } from "./report.ts";
import { pageRange, rangeText } from "../rules/listing.ts";

test("a data bar's share is clamped to 0–1 and empty for missing values", () => {
  assert.equal(barRatio(0.25), 0.25);
  assert.equal(barRatio(30, 120), 0.25);
  assert.equal(barRatio(120, 120), 1);
  assert.equal(barRatio(1.5), 1);
  assert.equal(barRatio(-3, 10), 0);
  assert.equal(barRatio(null), 0);
  assert.equal(barRatio(undefined), 0);
  assert.equal(barRatio(Number.NaN), 0);
  // A page whose largest value is 0 (or no rows) draws no bars rather than dividing by zero.
  assert.equal(barRatio(0, 0), 0);
  assert.equal(barRatio(5, 0), 0);
});

test("Searches bars are scaled to the largest value on the page", () => {
  const searches = [400, 100, 0];
  const most = Math.max(0, ...searches);
  assert.deepEqual(searches.map((s) => barRatio(s, most)), [1, 0.25, 0]);
});

test("a sparkline spans its box, top is the highest value, and nulls leave gaps", () => {
  assert.deepEqual(sparkPoints([0, 10], 64, 22), ["1,20 63,2"]);
  assert.deepEqual(sparkPoints([0, null, 10, 5], 64, 22), ["1,20", "42.33,2 63,11"]);
  assert.deepEqual(sparkPoints([null, null], 64, 22), []);
  assert.deepEqual(sparkPoints([], 64, 22), []);
});

test("a single value or a flat series sits in the middle", () => {
  assert.deepEqual(sparkPoints([7], 64, 22), ["32,11"]);
  assert.deepEqual(sparkPoints([3, 3], 64, 22), ["1,11 63,11"]);
});

test("the range footer reads like the rules listing", () => {
  const { from, to } = pageRange(1, 25, 1240);
  assert.equal(rangeText(from, to, 1240, "queries"), "1–25 of 1,240 queries");
  const last = pageRange(25, 50, 1240);
  assert.equal(rangeText(last.from, last.to, 1240, "queries"), "1,201–1,240 of 1,240 queries");
});
