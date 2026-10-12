import type { StorageLike } from "../reports/handoff.ts";

export const STATUSES = ["All", "Active", "Scheduled", "Expired", "Disabled"] as const;
export type StatusFilter = (typeof STATUSES)[number];
export type RuleStatus = Exclude<StatusFilter, "All">;
export const PAGE_SIZES = [25, 50, 100] as const;
export const SORTS = ["name", "priority"] as const;
export type RuleSort = (typeof SORTS)[number];
export type BulkAction = "Enable" | "Disable" | "Delete" | "ChangePriority";
export type PriorityMode = "shift" | "set";

export interface RuleListItem {
  readonly id: number;
  readonly name: string;
  readonly priority: number;
  readonly status: RuleStatus;
  readonly validFromUtc: string;
  readonly validUntilUtc: string;
  readonly conditions: string;
  readonly actions: string;
  readonly editPath: string;
  readonly version: string;
}

export type StatusCounts = Readonly<Record<StatusFilter, number>>;

export interface RuleListResult {
  readonly error?: string | null;
  readonly rows: readonly RuleListItem[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly counts: { readonly all: number; readonly active: number; readonly scheduled: number; readonly expired: number; readonly disabled: number };
}

export interface BulkFailure {
  readonly id: number;
  readonly name: string;
  readonly reason: string;
}

export interface BulkResult {
  readonly error?: string | null;
  readonly succeeded: number;
  readonly failed: readonly BulkFailure[];
}

/** A selected rule: the version is the concurrency token sent back, the name feeds the Delete dialog. */
export interface SelectedRule {
  readonly id: number;
  readonly name: string;
  readonly version: string;
}

export interface ViewState {
  readonly query: string;
  readonly status: StatusFilter;
  readonly sort: RuleSort;
  readonly descending: boolean;
  readonly page: number;
  readonly pageSize: number;
  readonly selected: readonly SelectedRule[];
  readonly allMatching: boolean;
}

export const initialView: ViewState = { query: "", status: "All", sort: "priority", descending: false, page: 1, pageSize: 25, selected: [], allMatching: false };

export type ViewAction =
  | { readonly type: "query"; readonly query: string }
  | { readonly type: "status"; readonly status: StatusFilter }
  | { readonly type: "pageSize"; readonly pageSize: number }
  | { readonly type: "sort"; readonly sort: RuleSort; readonly descending: boolean }
  | { readonly type: "page"; readonly page: number }
  | { readonly type: "select"; readonly selected: readonly SelectedRule[] }
  | { readonly type: "selectAllMatching" }
  | { readonly type: "clearSelection" }
  | { readonly type: "clearFilters" };

const unselected = { selected: [], allMatching: false } as const;

/** Any search, filter or page-size change starts again at page 1 with nothing selected. */
export const viewReducer = (state: ViewState, action: ViewAction): ViewState => {
  switch (action.type) {
    case "query":
      return action.query === state.query ? state : { ...state, ...unselected, query: action.query, page: 1 };
    case "status":
      return action.status === state.status ? state : { ...state, ...unselected, status: action.status, page: 1 };
    case "pageSize":
      return action.pageSize === state.pageSize ? state : { ...state, ...unselected, pageSize: action.pageSize, page: 1 };
    case "clearFilters":
      return { ...state, ...unselected, query: "", status: "All", page: 1 };
    case "sort":
      return { ...state, sort: action.sort, descending: action.descending, page: 1 };
    case "page":
      return action.page === state.page ? state : { ...state, page: action.page };
    case "select":
      return { ...state, selected: action.selected, allMatching: false };
    case "selectAllMatching":
      return { ...state, allMatching: true };
    case "clearSelection":
      return { ...state, ...unselected };
  }
};

/** The new selection from the table's identifiers: off-page picks are kept, page rows come from the page. */
export const selectionFrom = (ids: readonly unknown[], rows: readonly RuleListItem[], previous: readonly SelectedRule[]): SelectedRule[] =>
  ids.flatMap((id) => {
    const row = rows.find((r) => r.id === id);
    const kept = previous.find((r) => r.id === id);
    return row ? [{ id: row.id, name: row.name, version: row.version }] : kept ? [kept] : [];
  });

const format = (n: number) => n.toLocaleString("en-US");

export const rulesText = (n: number) => (n === 1 ? "1 rule" : `${format(n)} rules`);

/** "1–25 of 500 rules" */
export const rangeText = (from: number, to: number, total: number, noun: string) => `${format(from)}–${format(to)} of ${format(total)} ${noun}`;

/** First and last row number of a page. */
export const pageRange = (page: number, pageSize: number, total: number) => ({
  from: total === 0 ? 0 : (page - 1) * pageSize + 1,
  to: Math.min(page * pageSize, total),
});

export const statusLabel = (status: StatusFilter, count: number | undefined) => (count === undefined ? status : `${status} ${format(count)}`);

export const statusCounts = (counts: RuleListResult["counts"] | undefined): StatusCounts | undefined =>
  counts && { All: counts.all, Active: counts.active, Scheduled: counts.scheduled, Expired: counts.expired, Disabled: counts.disabled };

/** Up to three names, then "and {n} more" for the rest of `total`. */
export const deleteDialogNames = (names: readonly string[], total: number): string[] => {
  const shown = names.slice(0, Math.min(3, total));
  return total > shown.length ? [...shown, `and ${format(total - shown.length)} more`] : shown;
};

/** The command target: the filter itself when every matching rule is selected (the server checks the count), else the rules. */
export const bulkTarget = (state: ViewState, total: number) =>
  state.allMatching
    ? { filter: { query: state.query, status: state.status, expectedCount: total } }
    : { items: state.selected.map(({ id, version }) => ({ id, version })) };

/** Rules a bulk command acts on. */
export const selectedCount = (state: ViewState, total: number) => (state.allMatching ? total : state.selected.length);

const INT_MIN = -2_147_483_648;
const INT_MAX = 2_147_483_647;

/** A whole number within the server's int range (Set to also at least 1), or null. */
export const parsePriority = (text: string, mode: PriorityMode): number | null => {
  const trimmed = text.trim();
  if (!/^[+-]?\d+$/.test(trimmed)) {
    return null;
  }
  const value = Number(trimmed);
  return value < INT_MIN || value > INT_MAX || (mode === "set" && value < 1) ? null : value;
};

const VERBS: Record<BulkAction, string> = { Enable: "enabled", Disable: "disabled", Delete: "deleted", ChangePriority: "changed" };

/** "1 rule could not be disabled: it was changed by someone else. Reload and try again." */
export const partialFailureText = (action: BulkAction, failed: readonly BulkFailure[]) =>
  failed.length === 0 ? null : `${rulesText(failed.length)} could not be ${VERBS[action]}: ${failed[0].reason}. Reload and try again.`;

export const transportFailureText = (action: BulkAction) => `The rules could not be ${VERBS[action]}. Check the connection and try again.`;

// The view (never the selection) per index, in sessionStorage: admin URLs cannot carry state.
type SavedView = Pick<ViewState, "query" | "status" | "sort" | "descending" | "page" | "pageSize">;

export const viewKey = (index: string) => `lucene-suite:rules:${index}`;

const session = (): StorageLike | null => {
  try {
    return globalThis.sessionStorage ?? null;
  } catch {
    return null;
  }
};

export const saveView = (index: string | null, state: ViewState, storage = session()): void => {
  if (index === null) {
    return;
  }
  const { query, status, sort, descending, page, pageSize } = state;
  try {
    storage?.setItem(viewKey(index), JSON.stringify({ query, status, sort, descending, page, pageSize } satisfies SavedView));
  } catch {
    // Not saved: the page opens with its defaults next time.
  }
};

/** The saved view of this index, or the defaults when missing or malformed. */
export const loadView = (index: string | null, storage = session()): ViewState => {
  if (index === null) {
    return initialView;
  }
  try {
    const raw = storage?.getItem(viewKey(index));
    const v = raw ? (JSON.parse(raw) as Partial<SavedView>) : null;
    return v &&
      typeof v.query === "string" &&
      STATUSES.includes(v.status as StatusFilter) &&
      SORTS.includes(v.sort as RuleSort) &&
      typeof v.descending === "boolean" &&
      Number.isInteger(v.page) &&
      v.page! >= 1 &&
      PAGE_SIZES.includes(v.pageSize as 25)
      ? { ...initialView, ...(v as SavedView) }
      : initialView;
  } catch {
    return initialView;
  }
};
