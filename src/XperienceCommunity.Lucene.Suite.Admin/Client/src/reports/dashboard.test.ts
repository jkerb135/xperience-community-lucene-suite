import { test } from "node:test";
import assert from "node:assert/strict";
import { deltaTone, fractionTicks, insights, presetForLength, shiftRange, metricById, rollingAverage, series, valueOf, type KpiDay, type KpiTotals } from "./report.ts";

const day = (d: string, searches: number, extra: Partial<KpiDay> = {}): KpiDay => ({
  day: d,
  searches,
  zeroResultSearches: 0,
  clickedSearches: 0,
  convertedSearches: 0,
  clicks: 0,
  clickPositionSum: 0,
  sessionDays: 0,
  isComplete: true,
  ...extra,
});

const totals = (days: readonly KpiDay[]): KpiTotals => {
  const sum = (f: (d: KpiDay) => number) => days.reduce((a, d) => a + f(d), 0);
  return {
    searches: sum((d) => d.searches),
    zeroResultSearches: sum((d) => d.zeroResultSearches),
    clickedSearches: sum((d) => d.clickedSearches),
    convertedSearches: sum((d) => d.convertedSearches),
    clicks: sum((d) => d.clicks),
    clickPositionSum: sum((d) => d.clickPositionSum),
    sessionDays: sum((d) => d.sessionDays),
  };
};

const SEARCHES = metricById("searches");
const NO_RESULTS = metricById("noResultRate");
const POSITION = metricById("avgClickPosition");

test("counts are plain values; rates are null on days without a denominator (a gap, not 0)", () => {
  const days = [day("2030-01-01", 10, { zeroResultSearches: 2 }), day("2030-01-02", 0)];
  assert.deepEqual(series(days, SEARCHES), [10, 0]);
  assert.deepEqual(series(days, NO_RESULTS), [0.2, null]);
  assert.equal(valueOf(POSITION, totals([day("x", 5, { clicks: 4, clickPositionSum: 10 })])), 2.5);
});

test("the rolling average trails up to 7 values and skips gaps", () => {
  assert.deepEqual(rollingAverage([null, 2, null, 4]), [null, 2, 2, 3]);
  const nine = [1, 2, 3, 4, 5, 6, 7, 8, 9];
  assert.equal(rollingAverage(nine)[8], (3 + 4 + 5 + 6 + 7 + 8 + 9) / 7);
  assert.deepEqual(rollingAverage([]), []);
});

test("tone follows the metric's direction, not the sign", () => {
  assert.equal(deltaTone(SEARCHES, 0.1), "good");
  assert.equal(deltaTone(NO_RESULTS, -0.02), "good");
  assert.equal(deltaTone(NO_RESULTS, 0.02), "bad");
  assert.equal(deltaTone(POSITION, -0.5), "good");
  assert.equal(deltaTone(POSITION, 0.5), "bad");
  assert.equal(deltaTone(SEARCHES, null), "neutral");
  assert.equal(deltaTone(SEARCHES, 0.0001), "neutral");
});

test("peak, lowest and latest use complete days only", () => {
  const days = [day("2030-01-01", 5), day("2030-01-02", 9), day("2030-01-03", 1), day("2030-01-04", 50, { isComplete: false })];
  const result = insights(days, SEARCHES, totals(days), totals([day("p", 10)]), false);
  assert.deepEqual(result.peak, { day: "2030-01-02", value: 9 });
  assert.deepEqual(result.lowest, { day: "2030-01-03", value: 1 });
  assert.deepEqual(result.latest, { day: "2030-01-03", value: 1, average: 5 });
  assert.equal(result.activeDays, 4);
  assert.equal(result.totalDays, 4);
  assert.equal(result.dailyAverage, 65 / 4);
  assert.equal(result.delta, 5.5);
  assert.equal(result.tone, "good");
});

test("no complete day yet: peak, lowest and latest are empty", () => {
  const days = [day("2030-01-01", 5, { isComplete: false })];
  const result = insights(days, SEARCHES, totals(days), totals([]), false);
  assert.equal(result.peak, null);
  assert.equal(result.lowest, null);
  assert.equal(result.latest, null);
});

test("a partial previous period has no delta and a neutral tone", () => {
  const days = [day("2030-01-01", 5)];
  const result = insights(days, SEARCHES, totals(days), totals([day("p", 3)]), true);
  assert.equal(result.delta, null);
  assert.equal(result.tone, "neutral");
});

test("empty and all-null ranges stay null instead of dividing by zero", () => {
  const empty = insights([], NO_RESULTS, totals([]), totals([]), false);
  assert.equal(empty.value, null);
  assert.equal(empty.dailyAverage, null);
  assert.equal(empty.noResultShare, null);
  const silent = [day("2030-01-01", 0), day("2030-01-02", 0)];
  const rates = insights(silent, NO_RESULTS, totals(silent), totals([]), false);
  assert.equal(rates.dailyAverage, null);
  assert.equal(rates.peak, null);
  assert.equal(rates.activeDays, 0);
});

test("a single day works", () => {
  const days = [day("2030-01-01", 4, { zeroResultSearches: 1 })];
  const result = insights(days, NO_RESULTS, totals(days), totals([day("p", 4, { zeroResultSearches: 2 })]), false);
  assert.equal(result.value, 0.25);
  assert.equal(result.delta, -0.25);
  assert.equal(result.tone, "good");
  assert.deepEqual(result.latest, { day: "2030-01-01", value: 0.25, average: 0.25 });
});

test("an unknown or missing stored metric falls back to Searches", () => {
  assert.equal(metricById("nope"), SEARCHES);
  assert.equal(metricById(undefined), SEARCHES);
  assert.equal(metricById("ctr").id, "ctr");
});

test("fractional ticks cover rates and positions without float noise", () => {
  assert.deepEqual(fractionTicks(0.135), [0, 0.05, 0.1, 0.15]);
  assert.deepEqual(fractionTicks(2.4), [0, 1, 2, 3]);
  assert.deepEqual(fractionTicks(0.2), [0, 0.05, 0.1, 0.15, 0.2]);
  assert.deepEqual(fractionTicks(0), [0, 1]);
});

test("a range steps by its own length, never past today or before retention", () => {
  const week = { from: "2026-10-05", to: "2026-10-11" };
  assert.deepEqual(shiftRange(week, -1, "2026-10-12", 730), { from: "2026-09-28", to: "2026-10-04" });
  assert.equal(shiftRange(week, 1, "2026-10-12", 730), null);
  assert.deepEqual(shiftRange({ from: "2026-09-28", to: "2026-10-04" }, 1, "2026-10-12", 730), week);
  assert.equal(shiftRange({ from: "2026-10-01", to: "2026-10-07" }, -1, "2026-10-12", 14), null);
});

test("a stepped range keeps its preset only while the length matches", () => {
  assert.equal(presetForLength({ from: "2026-09-28", to: "2026-10-04" }, "7", 730), "7");
  assert.equal(presetForLength({ from: "2026-09-28", to: "2026-10-05" }, "7", 730), null);
  assert.equal(presetForLength({ from: "2026-09-28", to: "2026-10-04" }, "custom", 730), null);
});
