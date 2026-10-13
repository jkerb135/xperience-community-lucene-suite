import type { AppliedSynonym, Facet, FiredRule } from "./state";

export interface Hit {
  readonly itemGuid: string | null;
  readonly score: number;
  readonly fields: Record<string, string[]>;
  readonly pinnedByRuleId?: number | null;
}

export interface SearchResult {
  readonly hits: Hit[];
  readonly totalHits: number;
  readonly facets: Facet[];
  readonly elapsedMilliseconds: number;
  readonly error: string | null;
  readonly rulesFailed: boolean;
  readonly firedRules: FiredRule[];
  readonly synonymsFailed: boolean;
  readonly appliedSynonyms: AppliedSynonym[];
}

/** One column's search settings. */
export interface SideSettings {
  readonly applyRules: boolean;
  readonly applySynonyms: boolean;
  readonly contexts: readonly string[];
}

export interface ComparableHit {
  readonly itemGuid: string | null;
}

export type RankChange =
  | { readonly kind: "same" }
  | { readonly kind: "new" }
  | { readonly kind: "up" | "down"; readonly by: number };

export interface ContactGroup {
  readonly name: string;
  readonly displayName: string;
}

export const defaultSides = (): { baseline: SideSettings; tuned: SideSettings } => ({
  baseline: { applyRules: false, applySynonyms: false, contexts: [] },
  tuned: { applyRules: true, applySynonyms: true, contexts: [] },
});

/**
 * Rank of each ItemGuid: the position of its first hit, counting distinct ItemGuids only,
 * so a multilingual index's extra language documents never shift ranks.
 */
const ranks = (hits: readonly ComparableHit[]) => {
  const rank = new Map<string, number>();
  for (const hit of hits) {
    const key = hit.itemGuid?.toLowerCase();
    if (key && !rank.has(key)) {
      rank.set(key, rank.size);
    }
  }
  return rank;
};

/** How each Tuned hit moved against Baseline; later duplicates and hits without an ItemGuid get "same" (no marker). */
export const rankChanges = (baseline: readonly ComparableHit[], tuned: readonly ComparableHit[]): RankChange[] => {
  const before = ranks(baseline);
  const after = ranks(tuned);
  const seen = new Set<string>();
  return tuned.map((hit) => {
    const key = hit.itemGuid?.toLowerCase();
    if (!key || seen.has(key)) {
      return { kind: "same" };
    }
    seen.add(key);
    const was = before.get(key);
    if (was === undefined) {
      return { kind: "new" };
    }
    const delta = was - after.get(key)!;
    return delta === 0 ? { kind: "same" } : { kind: delta > 0 ? "up" : "down", by: Math.abs(delta) };
  });
};

/** Baseline hits whose ItemGuid Tuned did not return (first hit per ItemGuid). */
export const missingFrom = <T extends ComparableHit>(baseline: readonly T[], tuned: readonly ComparableHit[]): T[] => {
  const present = new Set(tuned.map((hit) => hit.itemGuid?.toLowerCase()).filter(Boolean));
  const listed = new Set<string>();
  return baseline.filter((hit) => {
    const key = hit.itemGuid?.toLowerCase();
    if (!key || present.has(key) || listed.has(key)) {
      return false;
    }
    listed.add(key);
    return true;
  });
};

/** Adds a free-text context; contexts match exactly on the server, so duplicates are compared as typed. */
export const addContext = (contexts: readonly string[], text: string, max: number, maxLength: number): { contexts: string[] } | { error: string } => {
  const context = text.trim();
  if (!context) {
    return { error: "Type a context to add." };
  }
  if (context.length > maxLength) {
    return { error: `At most ${maxLength} characters.` };
  }
  if (contexts.includes(context)) {
    return { error: `"${context}" is already a context.` };
  }
  if (contexts.length >= max) {
    return { error: `At most ${max} contexts.` };
  }
  return { contexts: [...contexts, context] };
};

/** Free-text contexts plus the chosen contact groups (by code name), without duplicates, in a stable order. */
export const withGroups = (contexts: readonly string[], groups: readonly ContactGroup[], chosen: readonly string[]): string[] => {
  const groupNames = new Set(groups.map((group) => group.name));
  return [...contexts.filter((context) => !groupNames.has(context)), ...chosen];
};

/** The chip label: a contact group's display name, else the context as typed. */
export const contextLabel = (context: string, groups: readonly ContactGroup[]) =>
  groups.find((group) => group.name === context)?.displayName ?? context;

/** The Moved cell: visible text plus the hidden text a screen reader gets instead of the arrow or dash. */
export const markerLabel = (change: RankChange): { text: string; hidden: string } => {
  switch (change.kind) {
    case "up":
    case "down":
      return { text: String(change.by), hidden: `${change.by} ${change.by === 1 ? "place" : "places"} ${change.kind}` };
    case "new":
      return { text: "New", hidden: "Not in Baseline" };
    default:
      return { text: "—", hidden: "Unchanged" };
  }
};
