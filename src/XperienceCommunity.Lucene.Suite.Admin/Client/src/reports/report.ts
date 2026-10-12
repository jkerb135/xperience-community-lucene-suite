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
