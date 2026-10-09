export interface FacetValue {
  readonly label: string;
  readonly count: number;
}

export interface Facet {
  readonly dimension: string;
  readonly values: readonly FacetValue[];
}

export type FacetSelection = Readonly<Record<string, readonly string[]>>;

/** Returned facets plus every selected value the server left out (top-N cut, zero hits, error), with count 0. */
export const mergeSelectedIntoFacets = (returned: readonly Facet[], selected: FacetSelection): Facet[] => {
  const zero = (labels: readonly string[]) => labels.map((label) => ({ label, count: 0 }));
  const merged = returned.map((facet) => {
    const missing = (selected[facet.dimension] ?? []).filter((label) => !facet.values.some((v) => v.label === label));
    return missing.length > 0 ? { ...facet, values: [...facet.values, ...zero(missing)] } : facet;
  });
  for (const [dimension, labels] of Object.entries(selected)) {
    if (labels.length > 0 && !returned.some((facet) => facet.dimension === dimension)) {
      merged.push({ dimension, values: zero(labels) });
    }
  }
  return merged;
};

export const toggleSelection = (selection: FacetSelection, dimension: string, label: string, checked: boolean): FacetSelection => {
  const values = (selection[dimension] ?? []).filter((v) => v !== label);
  const next: Record<string, readonly string[]> = { ...selection, [dimension]: checked ? [...values, label] : values };
  if (next[dimension].length === 0) {
    delete next[dimension];
  }
  return next;
};

/** The first `limit` values, plus any selected value beyond them so a ticked box never hides behind "Show more". */
export const visibleValues = (values: readonly FacetValue[], selected: readonly string[], expanded: boolean, limit: number) =>
  expanded ? values : values.filter((value, i) => i < limit || selected.includes(value.label));

/** "CoffeeProcessing" → "Coffee processing"; all-caps words such as "SKU" keep their case. */
export const humanizeFieldName = (name: string) => {
  const words = name
    .replace(/[_-]+/g, " ")
    .replace(/([a-z\d])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2")
    .trim()
    .split(/\s+/)
    .map((word) => (word.length > 1 && word === word.toUpperCase() ? word : word.toLowerCase()))
    .join(" ");
  return words.charAt(0).toUpperCase() + words.slice(1);
};

/** Each call aborts the previous request; `latest` is false once a newer call has started. A throw resolves as `value: undefined`. */
export const latestRequest = () => {
  let current: AbortController | undefined;
  return async <T>(send: (abort: AbortController) => Promise<T | undefined>): Promise<{ readonly latest: boolean; readonly value: T | undefined }> => {
    current?.abort();
    const mine = new AbortController();
    current = mine;
    let value: T | undefined;
    try {
      value = await send(mine);
    } catch {
      value = undefined;
    }
    return { latest: mine === current, value };
  };
};
