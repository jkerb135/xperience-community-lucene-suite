// A one-time hand-over of a report query to the page a row action opens. It lives in sessionStorage (this tab
// only) because admin URLs cannot carry state, and it is bound to the exact target page so it can never fill an
// existing rule or synonym.

export type HandoffTarget = "queryTester" | "ruleCreate" | "synonymCreate";

export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

interface Handoff {
  readonly target: HandoffTarget;
  readonly path: string;
  readonly query: string;
  readonly expires: number;
}

const KEY = "lucene-suite:handoff";
export const HANDOFF_LIFETIME_MS = 2 * 60 * 1000;

const session = (): StorageLike | null => {
  try {
    return globalThis.sessionStorage ?? null;
  } catch {
    return null;
  }
};

/** Leaves a query for the page at targetPath (admin-root-relative, e.g. "/site-search/indexes/4/rules/create"). */
export const give = (target: HandoffTarget, targetPath: string, query: string, now = Date.now(), storage = session()): void => {
  try {
    storage?.setItem(KEY, JSON.stringify({ target, path: targetPath, query, expires: now + HANDOFF_LIFETIME_MS } satisfies Handoff));
  } catch {
    // Storage full or blocked: the target simply opens empty.
  }
};

/** The query left for this target on this exact page, if not expired; reads only, so it is safe in render. */
export const peek = (target: HandoffTarget, currentPath: string, now = Date.now(), storage = session()): string | null => {
  try {
    const raw = storage?.getItem(KEY);
    if (!raw) {
      return null;
    }
    const entry = JSON.parse(raw) as Partial<Handoff>;
    return entry.target === target &&
      typeof entry.path === "string" &&
      entry.path.length > 0 &&
      currentPath.endsWith(entry.path) &&
      typeof entry.query === "string" &&
      typeof entry.expires === "number" &&
      entry.expires >= now
      ? entry.query
      : null;
  } catch {
    return null;
  }
};

/** Removes the handoff if it was left for this target on this page (used or expired); idempotent. */
export const clear = (target: HandoffTarget, currentPath: string, storage = session()): void => {
  try {
    const raw = storage?.getItem(KEY);
    const entry = raw ? (JSON.parse(raw) as Partial<Handoff>) : null;
    if (entry?.target === target && typeof entry.path === "string" && entry.path.length > 0 && currentPath.endsWith(entry.path)) {
      storage?.removeItem(KEY);
    }
  } catch {
    // Nothing to clear.
  }
};

/** Takes the query once when it was left for this target on this exact page and has not expired; otherwise null. */
export const take = (target: HandoffTarget, currentPath: string, now = Date.now(), storage = session()): string | null => {
  const query = peek(target, currentPath, now, storage);
  clear(target, currentPath, storage);
  return query;
};

/** The index identifier in an admin path such as "/admin/site-search/indexes/4/reports". */
export const indexFromPath = (pathname: string): string | null => /\/site-search\/indexes\/(\d+)(?:\/|$)/.exec(pathname)?.[1] ?? null;

export interface SavedReportState {
  readonly from: string;
  readonly to: string;
  readonly preset: string;
  readonly sort: string;
  readonly page: number;
}

const stateKey = (index: string) => `lucene-suite:reports:${index}`;

export const saveReportState = (index: string | null, state: SavedReportState, storage = session()): void => {
  if (index === null) {
    return;
  }
  try {
    storage?.setItem(stateKey(index), JSON.stringify(state));
  } catch {
    // Not saved: the page opens with its defaults next time.
  }
};

/** The saved state of this index's report, or null when missing or malformed; the caller validates the range. */
export const loadReportState = (index: string | null, storage = session()): SavedReportState | null => {
  if (index === null) {
    return null;
  }
  try {
    const raw = storage?.getItem(stateKey(index));
    const state = raw ? (JSON.parse(raw) as Partial<SavedReportState>) : null;
    return state && typeof state.from === "string" && typeof state.to === "string" && typeof state.preset === "string" && typeof state.sort === "string" && Number.isInteger(state.page) && state.page! >= 1
      ? (state as SavedReportState)
      : null;
  } catch {
    return null;
  }
};
