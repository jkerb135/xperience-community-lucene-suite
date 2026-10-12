// Report days are UTC calendar days kept as "yyyy-MM-dd" strings end to end: never parsed with the Date
// constructor and never produced with toISOString on a local Date, so no time zone can shift them.

export type Day = string;

export interface DayRange {
  readonly from: Day;
  readonly to: Day;
}

export const PRESETS = [
  { id: "7", label: "7 days", days: 7 },
  { id: "30", label: "30 days", days: 30 },
  { id: "90", label: "90 days", days: 90 },
  { id: "365", label: "12 months", days: 365 },
] as const;

export const CUSTOM = "custom";

const DAY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const MS_PER_DAY = 86_400_000;

const utcMs = (day: Day): number | null => {
  const match = DAY_PATTERN.exec(day);
  if (!match) {
    return null;
  }
  const [y, m, d] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const ms = Date.UTC(y, m - 1, d);
  const check = new Date(ms);
  return check.getUTCFullYear() === y && check.getUTCMonth() === m - 1 && check.getUTCDate() === d ? ms : null;
};

const fromUtcMs = (ms: number): Day => {
  const date = new Date(ms);
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
};

const pad = (n: number) => String(n).padStart(2, "0");

export const isDay = (value: string | null | undefined): value is Day => value != null && utcMs(value) !== null;

export const addDays = (day: Day, days: number): Day => fromUtcMs(utcMs(day)! + days * MS_PER_DAY);

/** Inclusive number of days in the range. */
export const lengthOf = ({ from, to }: DayRange): number => Math.round((utcMs(to)! - utcMs(from)!) / MS_PER_DAY) + 1;

/** A preset's range: the last N complete days ending yesterday, capped at maxDays. */
export const presetRange = (days: number, today: Day, maxDays: number): DayRange => {
  const to = addDays(today, -1);
  return { from: addDays(to, 1 - Math.min(days, maxDays)), to };
};

export const defaultRange = (today: Day, maxDays: number): DayRange => presetRange(30, today, maxDays);

export const previousRange = (range: DayRange): DayRange => {
  const days = lengthOf(range);
  return { from: addDays(range.from, -days), to: addDays(range.from, -1) };
};

/** A range the server accepts: valid days, from ≤ to ≤ today, at most maxDays long. */
export const isAllowed = (range: DayRange, today: Day, maxDays: number): boolean =>
  isDay(range.from) && isDay(range.to) && range.from <= range.to && range.to <= today && lengthOf(range) <= maxDays;

/** The preset a range equals, or CUSTOM. */
export const presetOf = (range: DayRange, today: Day, maxDays: number): string =>
  PRESETS.find((p) => {
    const candidate = presetRange(p.days, today, maxDays);
    return candidate.from === range.from && candidate.to === range.to;
  })?.id ?? CUSTOM;

/** The calendar day a picker shows for a Date (local fields: the picker works in local time). */
export const toDay = (date: Date): Day => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

/** Local midnight of a day, for the picker's value. */
export const toPickerDate = (day: Day): Date => {
  const ms = utcMs(day)!;
  const utc = new Date(ms);
  return new Date(utc.getUTCFullYear(), utc.getUTCMonth(), utc.getUTCDate());
};

export const dayLabel = (day: Day, locale?: string, short = false): string =>
  new Intl.DateTimeFormat(locale, short ? { timeZone: "UTC", day: "numeric", month: "short" } : { timeZone: "UTC", day: "numeric", month: "short", year: "numeric" }).format(
    utcMs(day)!,
  );

export const rangeLabel = (range: DayRange, locale?: string): string => `${dayLabel(range.from, locale)} – ${dayLabel(range.to, locale)}`;

export const DASH = "—";

const MINUS = "−";

const signed = (value: number, text: string) => (value > 0 ? `+${text}` : value < 0 ? `${MINUS}${text}` : `±${text}`);

export const formatCount = (value: number, locale?: string): string => new Intl.NumberFormat(locale).format(value);

export const formatRate = (value: number | null | undefined, locale?: string): string =>
  value == null ? DASH : new Intl.NumberFormat(locale, { style: "percent", minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);

export const formatPosition = (value: number | null | undefined, locale?: string): string =>
  value == null ? DASH : new Intl.NumberFormat(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);

const oneDecimal = (value: number, locale?: string) =>
  new Intl.NumberFormat(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(Math.abs(value));

export const NO_COMPARISON = "No comparison";
export const NO_PREVIOUS = "No previous data";
const VS = "vs previous period";

/** Counts: relative change from the server ("+15.0 % vs previous period"). */
export const countDelta = (delta: number | null | undefined, partial: boolean, locale?: string): string =>
  partial ? NO_COMPARISON : delta == null ? NO_PREVIOUS : `${signed(round1(delta * 100), `${oneDecimal(delta * 100, locale)} %`)} ${VS}`;

/** Rates: change in percentage points ("+3.0 pp"), never a relative change of a rate. */
export const rateDelta = (current: number | null | undefined, previous: number | null | undefined, partial: boolean, locale?: string): string => {
  if (partial) {
    return NO_COMPARISON;
  }
  if (current == null || previous == null) {
    return NO_PREVIOUS;
  }
  const points = (current - previous) * 100;
  return `${signed(round1(points), `${oneDecimal(points, locale)} pp`)} ${VS}`;
};

/** Average click position: absolute change in positions. */
export const positionDelta = (current: number | null | undefined, previous: number | null | undefined, partial: boolean, locale?: string): string => {
  if (partial) {
    return NO_COMPARISON;
  }
  if (current == null || previous == null) {
    return NO_PREVIOUS;
  }
  const change = current - previous;
  return `${signed(round1(change), oneDecimal(change, locale))} ${VS}`;
};

const round1 = (value: number) => Math.round(value * 10) / 10;

export interface Percentile {
  readonly ms: number | null;
  readonly isOverflow: boolean;
}

export const formatMs = (ms: number, locale?: string): string =>
  ms < 1000 ? `${formatCount(ms, locale)} ms` : `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(ms / 1000)} s`;

export const percentileLabel = (percentile: Percentile | null | undefined, bounds: readonly number[], locale?: string): string =>
  percentile == null
    ? DASH
    : percentile.isOverflow || percentile.ms == null
      ? `over ${formatMs(bounds[bounds.length - 1], locale)}`
      : `≤ ${formatMs(percentile.ms, locale)}`;

export const bucketLabels = (bounds: readonly number[], locale?: string): string[] => [
  ...bounds.map((bound) => `≤ ${formatMs(bound, locale)}`),
  `> ${formatMs(bounds[bounds.length - 1], locale)}`,
];

/** 3–5 evenly spaced integer ticks from 0 that cover max. */
export const niceTicks = (max: number): number[] => {
  if (max <= 1) {
    return [0, 1];
  }
  const rough = max / 4;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const normalized = rough / magnitude;
  const step = Math.max(1, (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * magnitude);
  const top = Math.ceil(max / step) * step;
  return Array.from({ length: top / step + 1 }, (_, i) => i * step);
};

/** Label every n-th category so at most maxLabels appear. */
export const labelEvery = (count: number, maxLabels = 8): number => Math.max(1, Math.ceil(count / maxLabels));

/** One report day with every daily KPI count (mirrors the server's SearchReportsDay). */
export interface KpiDay {
  readonly day: Day;
  readonly searches: number;
  readonly zeroResultSearches: number;
  readonly clickedSearches: number;
  readonly convertedSearches: number;
  readonly clicks: number;
  readonly clickPositionSum: number;
  readonly sessionDays: number;
  readonly isComplete: boolean;
}

/** Range totals of the same counts (the overview's current or previous period). */
export type KpiTotals = Omit<KpiDay, "day" | "isComplete">;

export type MetricKind = "count" | "rate" | "position";

export interface Metric {
  readonly id: string;
  readonly caption: string;
  readonly definition: string;
  readonly kind: MetricKind;
  /** Whether a higher value is the good direction. */
  readonly higherIsBetter: boolean;
  readonly numerator: (t: KpiTotals) => number;
  /** Absent for counts; a rate is null when its denominator is 0. */
  readonly denominator?: (t: KpiTotals) => number;
}

export const METRICS: readonly Metric[] = [
  { id: "searches", caption: "Searches", definition: "Logical searches in the period.", kind: "count", higherIsBetter: true, numerator: (t) => t.searches },
  {
    id: "noResultRate",
    caption: "No-result rate",
    definition: "Share of searches that returned nothing.",
    kind: "rate",
    higherIsBetter: false,
    numerator: (t) => t.zeroResultSearches,
    denominator: (t) => t.searches,
  },
  {
    id: "ctr",
    caption: "Click-through rate",
    definition: "Share of searches with at least one result click.",
    kind: "rate",
    higherIsBetter: true,
    numerator: (t) => t.clickedSearches,
    denominator: (t) => t.searches,
  },
  {
    id: "conversionRate",
    caption: "Conversion rate",
    definition: "Share of searches followed by a conversion.",
    kind: "rate",
    higherIsBetter: true,
    numerator: (t) => t.convertedSearches,
    denominator: (t) => t.searches,
  },
  {
    id: "avgClickPosition",
    caption: "Avg. click position",
    definition: "Average position of clicked results (1 is first).",
    kind: "position",
    higherIsBetter: false,
    numerator: (t) => t.clickPositionSum,
    denominator: (t) => t.clicks,
  },
  { id: "sessionDays", caption: "Session-days", definition: "Distinct search sessions per day, summed.", kind: "count", higherIsBetter: true, numerator: (t) => t.sessionDays },
];

export const DEFAULT_METRIC = METRICS[0];

/** The metric with this id, else Searches (stored state may be stale or tampered with). */
export const metricById = (id: string | null | undefined): Metric => METRICS.find((m) => m.id === id) ?? DEFAULT_METRIC;

/** The metric's value for a day or a range; null when a rate's denominator is 0. */
export const valueOf = (metric: Metric, totals: KpiTotals): number | null => {
  if (!metric.denominator) {
    return metric.numerator(totals);
  }
  const denominator = metric.denominator(totals);
  return denominator === 0 ? null : metric.numerator(totals) / denominator;
};

export const series = (days: readonly KpiDay[], metric: Metric): (number | null)[] => days.map((day) => valueOf(metric, day));

/** Trailing average over up to `window` values, skipping nulls; null until a value exists in the window. */
export const rollingAverage = (values: readonly (number | null)[], window = 7): (number | null)[] =>
  values.map((_, i) => {
    const present = values.slice(Math.max(0, i - window + 1), i + 1).filter((v): v is number => v != null);
    return present.length === 0 ? null : present.reduce((a, b) => a + b, 0) / present.length;
  });

/** Change from previous to current: relative for counts, absolute for rates and positions; null without a comparison. */
export const deltaOf = (metric: Metric, current: number | null, previous: number | null, partial: boolean): number | null => {
  if (partial || current == null || previous == null) {
    return null;
  }
  if (metric.kind === "count") {
    return previous === 0 ? null : (current - previous) / previous;
  }
  return current - previous;
};

export type Tone = "good" | "bad" | "neutral";

/** Good/bad by the metric's direction, not by sign: a falling no-result rate is good. A change that rounds to 0 is neutral. */
export const deltaTone = (metric: Metric, delta: number | null): Tone => {
  if (delta == null || round1(delta * (metric.kind === "position" ? 1 : 100)) === 0) {
    return "neutral";
  }
  return delta > 0 === metric.higherIsBetter ? "good" : "bad";
};

/** The metric's value formatted for display. */
export const formatMetric = (metric: Metric, value: number | null, locale?: string): string =>
  value == null
    ? DASH
    : metric.kind === "count"
      ? formatCount(Math.round(value), locale)
      : metric.kind === "rate"
        ? formatRate(value, locale)
        : formatPosition(value, locale);

/** The delta text in the metric's units: relative % for counts, pp for rates, positions for position. */
export const formatMetricDelta = (metric: Metric, current: number | null, previous: number | null, partial: boolean, delta: number | null, locale?: string): string =>
  metric.kind === "count"
    ? countDelta(delta, partial, locale)
    : metric.kind === "rate"
      ? rateDelta(current, previous, partial, locale)
      : positionDelta(current, previous, partial, locale);

export interface DayValue {
  readonly day: Day;
  readonly value: number;
}

export interface Insights {
  readonly value: number | null;
  readonly previous: number | null;
  readonly delta: number | null;
  readonly tone: Tone;
  readonly dailyAverage: number | null;
  /** Complete days only; null when no complete day has a value. */
  readonly peak: DayValue | null;
  readonly lowest: DayValue | null;
  readonly activeDays: number;
  readonly totalDays: number;
  /** The latest complete day against the 7-day rolling average ending on it. */
  readonly latest: (DayValue & { readonly average: number }) | null;
  readonly noResultShare: number | null;
}

export const insights = (days: readonly KpiDay[], metric: Metric, current: KpiTotals, previous: KpiTotals, partial: boolean): Insights => {
  const values = series(days, metric);
  const rolling = rollingAverage(values);
  const value = valueOf(metric, current);
  const before = valueOf(metric, previous);
  const delta = deltaOf(metric, value, before, partial);
  const present = values.filter((v): v is number => v != null);
  const sum = present.reduce((a, b) => a + b, 0);
  const complete = days.flatMap((day, i) => (day.isComplete && values[i] != null ? [{ day: day.day, value: values[i] as number, i }] : []));
  const pick = (better: (a: number, b: number) => boolean) =>
    complete.reduce<(typeof complete)[number] | null>((best, x) => (best === null || better(x.value, best.value) ? x : best), null);
  const peak = pick((a, b) => a > b);
  const lowest = pick((a, b) => a < b);
  const last = complete.length > 0 ? complete[complete.length - 1] : null;
  const lastAverage = last ? rolling[last.i] : null;
  return {
    value,
    previous: before,
    delta,
    tone: deltaTone(metric, delta),
    // Counts average over every day of the range; rates over the days that have a value.
    dailyAverage: metric.kind === "count" ? (days.length > 0 ? sum / days.length : null) : present.length > 0 ? sum / present.length : null,
    peak: peak && { day: peak.day, value: peak.value },
    lowest: lowest && { day: lowest.day, value: lowest.value },
    activeDays: days.filter((day) => day.searches > 0).length,
    totalDays: days.length,
    latest: last && lastAverage != null ? { day: last.day, value: last.value, average: lastAverage } : null,
    noResultShare: current.searches === 0 ? null : current.zeroResultSearches / current.searches,
  };
};

/** 3–5 evenly spaced ticks from 0 that cover max, allowing fractional steps (rates, positions). */
export const fractionTicks = (max: number): number[] => {
  if (!(max > 0)) {
    return [0, 1];
  }
  const rough = max / 4;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const normalized = rough / magnitude;
  const step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * magnitude;
  const count = Math.ceil(max / step - 1e-9);
  // toPrecision drops float noise such as 0.15000000000000002.
  return Array.from({ length: count + 1 }, (_, i) => Number((i * step).toPrecision(12)));
};

/**
 * The same-length range one period earlier (-1) or later (+1), or null when it would leave what the server keeps
 * (after today, or starting before the oldest retained day today − maxDays + 1).
 */
export const shiftRange = (range: DayRange, direction: -1 | 1, today: Day, maxDays: number): DayRange | null => {
  const days = lengthOf(range) * direction;
  const shifted = { from: addDays(range.from, days), to: addDays(range.to, days) };
  return isAllowed(shifted, today, maxDays) && shifted.from >= addDays(today, 1 - maxDays) ? shifted : null;
};

/** The preset a stepped range still belongs to: its length matches the preset's (stepping keeps "7 days" selected). */
export const presetForLength = (range: DayRange, preset: string | undefined, maxDays: number): string | null => {
  const match = PRESETS.find((p) => p.id === preset);
  return match && Math.min(match.days, maxDays) === lengthOf(range) ? match.id : null;
};
