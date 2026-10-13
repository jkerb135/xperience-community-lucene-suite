import { DASH, barRatio, formatCount, formatPosition, formatRate, percentileLabel, type Percentile, type Tone } from "./report.ts";

export type InsightKind = "ContentGap" | "RelevanceMiss" | "BuriedAnswer" | "SlowQuery" | "Regression";

/** A row action of the query table; RowActions shows a subset of them. */
export type RowAction = "tester" | "rule" | "synonym" | "block";

export interface InsightRow {
  readonly kind: InsightKind;
  readonly queryText: string;
  readonly hasPlaceholder: boolean;
  readonly searches: number;
  readonly affected: number;
  readonly noResultRate: number | null;
  readonly ctr: number | null;
  readonly avgClickPosition: number | null;
  readonly latencyP95: Percentile | null;
  readonly previousSearches: number;
  readonly previousNoResultRate: number | null;
  readonly previousCtr: number | null;
  readonly appliedSynonyms: readonly { readonly id: number; readonly editUrl: string }[];
  readonly firedRules: readonly { readonly id: number; readonly name: string; readonly editUrl: string }[];
}

export interface InsightsResult {
  readonly error?: string | null;
  readonly findings: InsightRow[];
  readonly omitted: number;
  readonly considered: number;
  readonly truncated: boolean;
  readonly previousIsPartial: boolean;
  readonly annotationsFailed: boolean;
}

export interface KindInfo {
  readonly label: string;
  readonly explanation: string;
  readonly actions: readonly RowAction[];
}

/** Display order of the kind groups (the server's tie-break order). */
export const KINDS: readonly InsightKind[] = ["ContentGap", "RelevanceMiss", "BuriedAnswer", "SlowQuery", "Regression"];

const INFO: Record<InsightKind, KindInfo> = {
  ContentGap: {
    label: "No results",
    explanation: "Searches for these queries often found nothing. Add a synonym or a rule, or create the missing content.",
    actions: ["synonym", "rule", "tester"],
  },
  RelevanceMiss: {
    label: "Results not clicked",
    explanation: "These queries found results, but people rarely clicked any. Check the results and promote better ones with a rule.",
    actions: ["rule", "tester", "block"],
  },
  BuriedAnswer: {
    label: "Answer buried",
    explanation: "People clicked results far down the list. Move the answer up with a rule.",
    actions: ["rule", "tester"],
  },
  SlowQuery: {
    label: "Slow",
    explanation: "These queries took long to answer. Check them in the query tester.",
    actions: ["tester"],
  },
  Regression: {
    label: "Got worse",
    explanation: "These queries do worse than in the previous period of the same length.",
    actions: ["tester", "rule", "synonym"],
  },
};

export const kindInfo = (kind: InsightKind): KindInfo => INFO[kind];

const change = (name: string, previous: number | null, current: number | null, worse: boolean, locale?: string) =>
  previous != null && current != null && worse ? `${name} ${formatRate(previous, locale)} → ${formatRate(current, locale)}` : null;

/** One line of evidence for a finding, from the figures the server sent. */
export const evidence = (row: InsightRow, bounds: readonly number[], locale?: string): string => {
  const searches = formatCount(row.searches, locale);
  const affected = formatCount(row.affected, locale);
  switch (row.kind) {
    case "ContentGap":
      return `${affected} of ${searches} searches had no results (${formatRate(row.noResultRate, locale)})`;
    case "RelevanceMiss":
      return `${affected} of ${searches} searches found results but got no click (CTR ${formatRate(row.ctr, locale)})`;
    case "BuriedAnswer":
      return `Clicked on average at position ${formatPosition(row.avgClickPosition, locale)} in ${affected} searches`;
    case "SlowQuery":
      return `${affected} of ${searches} searches were slow; p95 ${percentileLabel(row.latencyP95, bounds, locale)}`;
    case "Regression": {
      const worse = [
        change("No-result rate", row.previousNoResultRate, row.noResultRate, (row.noResultRate ?? 0) > (row.previousNoResultRate ?? 0), locale),
        change("CTR", row.previousCtr, row.ctr, (row.ctr ?? 0) < (row.previousCtr ?? 0), locale),
      ].filter(Boolean);
      return worse.length > 0 ? worse.join(" · ") : "Worse than in the previous period";
    }
  }
};

/** Findings grouped in the fixed kind order; kinds without findings are left out. Server order is kept within a kind. */
export const groupByKind = (rows: readonly InsightRow[]) =>
  KINDS.map((kind) => ({ kind, rows: rows.filter((row) => row.kind === kind) })).filter((group) => group.rows.length > 0);

/** The first findings in server order (most affected searches first). */
export const topFindings = (rows: readonly InsightRow[], count: number) => rows.slice(0, count);

/** Edit links of the synonyms and rules that already apply; paths are admin-root relative (use adminHref). */
export const fixLinks = (row: InsightRow) => [
  ...row.appliedSynonyms.map((synonym, i) => ({
    key: `synonym-${synonym.id}`,
    label: row.appliedSynonyms.length > 1 ? `Synonym ${i + 1}` : "Synonym",
    editUrl: synonym.editUrl,
  })),
  ...row.firedRules.map((rule) => ({ key: `rule-${rule.id}`, label: `Rule "${rule.name}"`, editUrl: rule.editUrl })),
];

/** "12 findings · 40 queries checked", noting the caps when they applied. */
export const summary = (result: InsightsResult, locale?: string): string => {
  const total = result.findings.length + result.omitted;
  return [
    `${formatCount(total, locale)} ${total === 1 ? "finding" : "findings"}`,
    `${formatCount(result.considered, locale)} ${result.truncated ? "most searched " : ""}${result.considered === 1 ? "query" : "queries"} checked`,
    result.omitted > 0 ? `showing the top ${formatCount(result.findings.length, locale)}` : null,
  ]
    .filter(Boolean)
    .join(" · ");
};

export interface FindingBar {
  readonly ratio: number;
  readonly tone: Tone;
  readonly label: string;
}

const rateBar = (rate: number | null, locale?: string): FindingBar => ({ ratio: barRatio(rate), tone: "bad", label: formatRate(rate, locale) });

/** Worsening of a rate in points (positive = worse), or null without both values. */
const worsening = (previous: number | null, current: number | null, higherIsWorse: boolean) =>
  previous == null || current == null ? null : higherIsWorse ? current - previous : previous - current;

/** The data bar of a finding: the figure its kind is about, as a 0–1 share (average position as a share of 10). */
export const findingBar = (row: InsightRow, locale?: string): FindingBar => {
  switch (row.kind) {
    case "ContentGap":
      return rateBar(row.noResultRate, locale);
    case "RelevanceMiss":
      return rateBar(row.ctr, locale);
    case "BuriedAnswer":
      return { ratio: barRatio(row.avgClickPosition, 10), tone: "neutral", label: formatPosition(row.avgClickPosition, locale) };
    case "SlowQuery":
      return rateBar(row.searches > 0 ? row.affected / row.searches : null, locale);
    case "Regression": {
      // The rate that got worse by more points, as the server's affected count does.
      const rise = worsening(row.previousNoResultRate, row.noResultRate, true) ?? 0;
      const fall = worsening(row.previousCtr, row.ctr, false) ?? 0;
      return rise <= 0 && fall <= 0 ? { ratio: 0, tone: "neutral", label: DASH } : rateBar(rise >= fall ? row.noResultRate : row.ctr, locale);
    }
  }
};
