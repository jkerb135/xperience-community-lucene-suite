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

export interface PinRow {
  readonly itemGuid: string;
  readonly position: string;
}

/** An indexed item as the Search and Describe commands return it. */
export interface RuleItem {
  readonly itemGuid: string;
  readonly title: string;
  readonly contentType: string | null;
}

export const sameGuid = (guid: string) => guid.toLowerCase();

export const parsePins = (json: string | undefined): PinRow[] =>
  parseArray(json).map((item) => {
    const pin = record(item);
    return { itemGuid: text(pin.itemGuid), position: pin.position === undefined ? "" : String(pin.position) };
  });

// Like boosts, a non-integer position is sent as typed so the server reports it on the position control.
export const serializePins = (rows: readonly PinRow[]): string =>
  JSON.stringify(
    rows.map((row) => ({
      itemGuid: row.itemGuid,
      position: /^\d+$/.test(row.position.trim()) ? Number(row.position.trim()) : row.position,
    })),
  );

export const parseHides = (json: string | undefined): string[] => parseArray(json).map(text);
export const serializeHides = (guids: readonly string[]): string => JSON.stringify(guids);

/** The `count` lowest positions from 1 that are not taken. */
export const nextFreePositions = (taken: readonly number[], count: number): number[] => {
  const used = new Set(taken);
  const free: number[] = [];
  for (let position = 1; free.length < count; position++) {
    if (!used.has(position)) {
      free.push(position);
    }
  }
  return free;
};

/** Appends pins for items not pinned yet, at the next free positions, in the order chosen. */
export const addPins = (rows: readonly PinRow[], guids: readonly string[]): PinRow[] => {
  const pinned = new Set(rows.map((row) => sameGuid(row.itemGuid)));
  const fresh = guids.filter((guid, i) => !pinned.has(sameGuid(guid)) && guids.findIndex((g) => sameGuid(g) === sameGuid(guid)) === i);
  const taken = rows.map((row) => Number(row.position.trim())).filter((position) => Number.isInteger(position));
  const positions = nextFreePositions(taken, fresh.length);
  return [...rows, ...fresh.map((itemGuid, i) => ({ itemGuid, position: String(positions[i]) }))];
};

/** Appends hides for items not hidden yet, in the order chosen. */
export const addHides = (hides: readonly string[], guids: readonly string[]): string[] => {
  const result = [...hides];
  for (const guid of guids) {
    if (!result.some((existing) => sameGuid(existing) === sameGuid(guid))) {
      result.push(guid);
    }
  }
  return result;
};

/** How the picker shows a result: already in this list (checked), in the other list, or free to choose. */
export const pickerRow = (guid: string, selected: ReadonlySet<string>, added: ReadonlySet<string>, excluded: ReadonlySet<string>, excludedNote: string) => {
  const key = sameGuid(guid);
  if (added.has(key)) {
    return { checked: true, disabled: true, note: "Already added" };
  }
  if (excluded.has(key)) {
    return { checked: false, disabled: true, note: excludedNote };
  }
  return { checked: selected.has(key), disabled: false, note: undefined };
};

export const toggleItem = (selected: ReadonlySet<string>, guid: string, checked: boolean): Set<string> => {
  const next = new Set(selected);
  if (checked) {
    next.add(sameGuid(guid));
  } else {
    next.delete(sameGuid(guid));
  }
  return next;
};

/** Every path a pins control shows errors for; a row-level error shows on the item. */
export const pinControlPaths = (rows: readonly PinRow[]): string[] =>
  rows.flatMap((_, i) => [`actions.pins[${i}].position`, `actions.pins[${i}].itemGuid`]);

export const pinAlias = (path: string) => (/^actions\.pins\[\d+\]$/.test(path) ? `${path}.itemGuid` : path);

export const hideControlPaths = (hides: readonly string[]): string[] => hides.map((_, i) => `actions.hides[${i}]`);

/** A form field's value by name, ignoring case (the form runtime's key casing is the server's property name). */
export const fieldValue = (values: Readonly<Record<string, unknown>> | undefined, name: string): string | undefined => {
  const key = Object.keys(values ?? {}).find((k) => k.toLowerCase() === name.toLowerCase());
  const value = key === undefined ? undefined : values?.[key];
  return typeof value === "string" ? value : undefined;
};
