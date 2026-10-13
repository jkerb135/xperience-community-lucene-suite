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

export type PrimaryAction = "pin" | "boost" | "hide" | "data";

export interface FiredRule {
  readonly ruleId: number;
  readonly name: string | null;
  readonly editUrl: string | null;
  readonly userData: unknown;
  /** Action summary, e.g. "2 pins, 1 boost"; null for a rule deleted since. */
  readonly actions?: string | null;
  readonly primaryAction?: PrimaryAction | null;
}

export interface PinnableHit {
  readonly pinnedByRuleId?: number | null;
}

const ruleName = (rule: Pick<FiredRule, "ruleId" | "name"> | undefined, ruleId: number) => rule?.name ?? `Rule ${ruleId}`;

/**
 * The rule's edit link inside the admin: the server's path is relative to the admin root, so the root is taken
 * from the current page's path (everything before the link's first segment).
 */
export const adminHref = (editUrl: string, currentPath: string) => {
  const firstSegment = editUrl.split("/").slice(0, 2).join("/") + "/";
  const at = currentPath.indexOf(firstSegment);
  return (at > 0 ? currentPath.slice(0, at) : "") + editUrl;
};

const CHIP_ICON = { pin: "xp-pin", boost: "xp-arrow-up", hide: "xp-eye-slash", data: "xp-database" } as const;

/** Icon of a fired-rule chip: what the rule mainly does. */
export const chipIcon = (primaryAction: PrimaryAction | null | undefined) => CHIP_ICON[primaryAction ?? "data"];

/** Fired rules as the query tester lists them; a rule deleted since has no name, link or summary. */
export const firedRuleRows = (rules: readonly FiredRule[], currentPath: string) =>
  rules.map((rule) => ({
    ruleId: rule.ruleId,
    label: ruleName(rule, rule.ruleId),
    href: rule.name !== null && rule.editUrl ? adminHref(rule.editUrl, currentPath) : null,
    userData: rule.userData === null || rule.userData === undefined ? null : JSON.stringify(rule.userData, null, 2),
    summary: rule.actions ?? null,
    icon: chipIcon(rule.primaryAction),
  }));

export interface AppliedSynonym {
  readonly synonymId: number;
  readonly term: string;
  readonly replacement: string;
  readonly editUrl: string | null;
}

/** Applied synonyms as the query tester lists them; a synonym deleted since has no link. */
export const appliedSynonymRows = (synonyms: readonly AppliedSynonym[], currentPath: string) =>
  synonyms.map((synonym, index) => ({
    key: `${synonym.synonymId}-${index}`,
    label: `${synonym.term} → ${synonym.replacement}`,
    href: synonym.editUrl ? adminHref(synonym.editUrl, currentPath) : null,
  }));

/** Name of the rule that pinned the hit, or null for an organic hit. */
export const pinningRuleName = (hit: PinnableHit, rules: readonly FiredRule[]) =>
  hit.pinnedByRuleId === null || hit.pinnedByRuleId === undefined
    ? null
    : ruleName(rules.find((rule) => rule.ruleId === hit.pinnedByRuleId), hit.pinnedByRuleId);

/** 1-based position of the index-th hit on a page. */
export const position = (page: number, pageSize: number, index: number) => (page - 1) * pageSize + index + 1;

/** A score bar's share of the page's top score; 0 (empty bar) for non-finite or non-positive values. */
export const scoreRatio = (score: number, top: number) =>
  Number.isFinite(score) && Number.isFinite(top) && score > 0 && top > 0 ? Math.min(1, score / top) : 0;
