export type Anchoring = "any" | "is" | "startsWith" | "endsWith" | "contains";

export interface FacetRow {
  readonly dimension: string;
  readonly value: string;
}

export interface ConditionRow {
  readonly anchoring: Anchoring;
  readonly pattern: string;
  readonly facets: readonly FacetRow[];
  readonly contexts: readonly string[];
}

export interface BoostRow {
  readonly field: string;
  readonly value: string;
  readonly strength: string;
}

export interface RuleError {
  readonly path: string;
  readonly message: string;
}

export const anchoringOptions: ReadonlyArray<{ readonly value: Anchoring; readonly label: string }> = [
  { value: "any", label: "Any query" },
  { value: "is", label: "Is" },
  { value: "startsWith", label: "Starts with" },
  { value: "endsWith", label: "Ends with" },
  { value: "contains", label: "Contains" },
];

export const emptyCondition: ConditionRow = { anchoring: "contains", pattern: "", facets: [], contexts: [] };
export const emptyBoost: BoostRow = { field: "", value: "", strength: "10" };

const text = (value: unknown) => (typeof value === "string" ? value : "");
const parseArray = (json: string | undefined): unknown[] => {
  try {
    const parsed: unknown = JSON.parse(json?.trim() ? json : "[]");
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};
const record = (value: unknown): Record<string, unknown> => (value && typeof value === "object" ? (value as Record<string, unknown>) : {});

export const parseConditions = (json: string | undefined): ConditionRow[] =>
  parseArray(json).map((item) => {
    const condition = record(item);
    const anchoring = anchoringOptions.some((o) => o.value === condition.anchoring) ? (condition.anchoring as Anchoring) : "any";
    return {
      anchoring: typeof condition.pattern === "string" ? anchoring : "any",
      pattern: text(condition.pattern),
      facets: (Array.isArray(condition.facets) ? condition.facets : []).map((f) => ({ dimension: text(record(f).dimension), value: text(record(f).value) })),
      contexts: (Array.isArray(condition.contexts) ? condition.contexts : []).map(text),
    };
  });

// Row i serializes to conditions[i], so server error paths index the rows directly.
export const serializeConditions = (rows: readonly ConditionRow[]): string =>
  JSON.stringify(
    rows.map((row) => ({
      ...(row.anchoring === "any" ? {} : { anchoring: row.anchoring, pattern: row.pattern }),
      ...(row.facets.length ? { facets: row.facets } : {}),
      ...(row.contexts.length ? { contexts: row.contexts } : {}),
    })),
  );

export const parseBoosts = (json: string | undefined): BoostRow[] =>
  parseArray(json).map((item) => {
    const boost = record(item);
    return { field: text(boost.field), value: text(boost.value), strength: boost.strength === undefined ? "" : String(boost.strength) };
  });

// A non-integer strength is sent as typed, so the server reports it on the strength control.
export const serializeBoosts = (rows: readonly BoostRow[]): string =>
  JSON.stringify(
    rows.map((row) => ({
      field: row.field,
      value: row.value,
      strength: /^-?\d+$/.test(row.strength.trim()) ? Number(row.strength.trim()) : row.strength,
    })),
  );

/** Every path a conditions control shows errors for. */
export const conditionControlPaths = (rows: readonly ConditionRow[]): string[] =>
  rows.flatMap((row, i) => [
    `conditions[${i}].anchoring`,
    `conditions[${i}].pattern`,
    `conditions[${i}].facets`,
    `conditions[${i}].contexts`,
    ...row.facets.flatMap((_, j) => [`conditions[${i}].facets[${j}].dimension`, `conditions[${i}].facets[${j}].value`]),
    ...row.contexts.map((_, j) => `conditions[${i}].contexts[${j}]`),
  ]);

/** Every path a boosts control shows errors for; a row-level error (duplicate) shows on its value. */
export const boostControlPaths = (rows: readonly BoostRow[]): string[] =>
  rows.flatMap((_, i) => [`actions.boosts[${i}].field`, `actions.boosts[${i}].value`, `actions.boosts[${i}].strength`]);

export const boostAlias = (path: string) => (/^actions\.boosts\[\d+\]$/.test(path) ? `${path}.value` : path);

/** Splits errors into per-control messages and the rest, which the component lists in a notification bar. */
export const partitionErrors = (errors: readonly RuleError[], controlPaths: readonly string[], alias: (path: string) => string = (p) => p) => {
  const known = new Set(controlPaths);
  const byPath = new Map<string, string>();
  const unmatched: RuleError[] = [];
  for (const error of errors) {
    const path = alias(error.path);
    if (known.has(path)) {
      byPath.set(path, byPath.has(path) ? `${byPath.get(path)} ${error.message}` : error.message);
    } else {
      unmatched.push(error);
    }
  }
  return { byPath, unmatched };
};

export const replaceAt = <T>(items: readonly T[], index: number, item: T): T[] => items.map((existing, i) => (i === index ? item : existing));
export const removeAt = <T>(items: readonly T[], index: number): T[] => items.filter((_, i) => i !== index);
export const canAdd = (count: number, limit: number) => count < limit;

/** Calls `run` after `delayMs` of quiet; a newer call cancels the pending one. */
export const debounce = <A extends unknown[]>(run: (...args: A) => void, delayMs: number) => {
  let timer: ReturnType<typeof setTimeout> | undefined;
  return (...args: A) => {
    if (timer !== undefined) {
      clearTimeout(timer);
    }
    timer = setTimeout(() => run(...args), delayMs);
  };
};
