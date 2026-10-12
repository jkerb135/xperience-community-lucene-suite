import { useCallback, useEffect, useMemo, useState } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  ButtonSize,
  Card,
  CellType,
  ColumnContentType,
  LabelWithTooltip,
  NameToggleButtons,
  NotificationBarAlert,
  Pagination,
  Spacing,
  Spinner,
  Stack,
  Table,
  type ComponentCell,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";
import { adminHref, latestRequest } from "../query-tester/state.ts";
import { give, type HandoffTarget } from "./handoff.ts";
import type { RowAction } from "./insights.ts";
import { cardHeadline, visuallyHidden } from "./dashboard.tsx";
import { DASH, blockSuggestionReason, formatCount, formatPosition, formatRate, percentileLabel, type DayRange, type Percentile } from "./report.ts";

export const SORTS = [
  { id: "Searches", label: "Top", title: "Most searched queries" },
  { id: "NoResults", label: "No results", title: "Queries without results" },
  { id: "LowestCtr", label: "Lowest CTR", title: "Queries with the lowest click-through" },
  { id: "Slowest", label: "Slowest", title: "Slowest queries" },
] as const;

export interface QueryRow {
  readonly queryText: string | null;
  readonly isBrowse: boolean;
  readonly hasPlaceholder: boolean;
  readonly searches: number;
  readonly ctr: number | null;
  readonly noResultRate: number | null;
  readonly avgClickPosition: number | null;
  readonly latencyP95: Percentile | null;
}

export interface QueriesResult {
  readonly error?: string | null;
  readonly rows: QueryRow[];
  readonly other: QueryRow | null;
  readonly totalCount: number;
  readonly pageSize: number;
}

export interface QueryTableProps {
  readonly range: DayRange;
  readonly sort: string;
  readonly page: number;
  readonly onSortChange: (sort: string) => void;
  readonly onPageChange: (page: number) => void;
  readonly bucketBounds: readonly number[];
  readonly canManageRules: boolean;
  readonly canManageSynonyms: boolean;
  readonly queryTesterPath: string;
  readonly ruleCreatePath: string;
  readonly synonymCreatePath: string;
  readonly onBlock: (query: string) => void;
}

const column = (name: string, caption: string, maxWidth = 16, minWidth = 8): TableColumn => ({
  name,
  caption,
  visible: true,
  minWidth,
  maxWidth,
  contentType: ColumnContentType.Text,
  sortable: false,
  searchable: false,
});

const cell = (value: string, columnName: string): StringCell => ({ type: CellType.String, value, columnName });

const COLUMNS = [
  column("query", "Query", 40),
  column("searches", "Searches"),
  column("noResults", "No results"),
  column("ctr", "CTR"),
  column("position", "Avg. pos."),
  column("p95", "p95"),
  // Four icon buttons in one line, as in native listings.
  column("actions", "Actions", 15, 15),
];

export const BROWSE = "(browse – no query text)";
const PLACEHOLDER = "Contains a removed value; a rule or synonym would never match";

/** Why Create rule / Add synonym are unavailable for this row, or null. */
export const blockedReason = (row: ActionRow): string | null =>
  row.isBrowse ? "The browse row has no query text" : row.hasPlaceholder ? PLACEHOLDER : null;

/** Hands the query to the target page (one-time sessionStorage handoff) and navigates there. */
export const openTarget = (target: HandoffTarget, path: string, query: string) => {
  give(target, path, query);
  window.location.assign(adminHref(path, window.location.pathname));
};

/** What the row actions read of a row. */
export type ActionRow = Pick<QueryRow, "queryText" | "isBrowse" | "hasPlaceholder">;

export interface RowActionsProps {
  readonly row: ActionRow;
  readonly canManageRules: boolean;
  readonly canManageSynonyms: boolean;
  readonly queryTesterPath?: string;
  readonly ruleCreatePath: string;
  readonly synonymCreatePath: string;
  /** Shows the Block suggestion button (query table only). */
  readonly onBlock?: (query: string) => void;
  /** The actions to show, always in the table's order; all of them by default. Never derive it from request state. */
  readonly actions?: readonly RowAction[];
}

/**
 * Listing-style row actions: icon-only buttons in one line. Each has a full accessible name (the npm 31.9.1 ActionCell
 * announces every icon as "button"). A disabled button cannot take focus, so its reason is also given as hidden text.
 */
export const RowActions = ({ row, canManageRules, canManageSynonyms, queryTesterPath, ruleCreatePath, synonymCreatePath, onBlock, actions }: RowActionsProps) => {
  const query = row.queryText ?? "";
  const shows = (action: RowAction) => actions === undefined || actions.includes(action);
  const blocked = blockedReason(row);
  const ruleReason = shows("rule") ? (blocked ?? (canManageRules ? null : "Requires Site Search → Manage rules")) : null;
  const synonymReason = shows("synonym") ? (blocked ?? (canManageSynonyms ? null : "Requires Site Search → Manage synonyms")) : null;
  const showBlock = onBlock !== undefined && shows("block");
  const blockReason = showBlock ? blockSuggestionReason(row, canManageSynonyms) : null;
  const name = row.isBrowse ? "the browse row" : `"${query}"`;
  return (
    <div style={{ display: "flex", flexWrap: "nowrap", alignItems: "center", gap: Spacing.XXS }}>
      {queryTesterPath !== undefined && shows("tester") && (
        <Button
          icon="xp-magnifier"
          size={ButtonSize.S}
          color={ButtonColor.Quinary}
          borderless
          aria-label={`Open ${name} in the query tester`}
          title="Open in query tester"
          onClick={() => openTarget("queryTester", queryTesterPath, query)}
        />
      )}
      {shows("rule") && (
        <Button
          icon="xp-plus-circle"
          size={ButtonSize.S}
          color={ButtonColor.Quinary}
          borderless
          aria-label={`Create rule for ${name}`}
          title={ruleReason ?? "Create rule"}
          disabled={ruleReason !== null}
          onClick={() => openTarget("ruleCreate", ruleCreatePath, query)}
        />
      )}
      {shows("synonym") && (
        <Button
          icon="xp-arrows-h"
          size={ButtonSize.S}
          color={ButtonColor.Quinary}
          borderless
          aria-label={`Add synonym for ${name}`}
          title={synonymReason ?? "Add synonym"}
          disabled={synonymReason !== null}
          onClick={() => openTarget("synonymCreate", synonymCreatePath, query)}
        />
      )}
      {showBlock && (
        <Button
          icon="xp-ban-sign"
          size={ButtonSize.S}
          color={ButtonColor.Quinary}
          borderless
          aria-label={`Block ${name} from suggestions`}
          title={blockReason ?? "Block suggestion"}
          disabled={blockReason !== null}
          onClick={() => onBlock?.(query)}
        />
      )}
      {(ruleReason || synonymReason || blockReason) && (
        <span style={visuallyHidden}>
          {[
            ruleReason && `Create rule unavailable: ${ruleReason}.`,
            synonymReason && `Add synonym unavailable: ${synonymReason}.`,
            blockReason && `Block suggestion unavailable: ${blockReason}.`,
          ]
            .filter(Boolean)
            .join(" ")}
        </span>
      )}
    </div>
  );
};

export const QueryTable = (props: QueryTableProps) => {
  const { range, sort, page, onSortChange, onPageChange, bucketBounds } = props;
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [result, setResult] = useState<QueriesResult>();
  const [error, setError] = useState<string>();
  const [failed, setFailed] = useState(false);
  const [inProgress, setInProgress] = useState(false);

  const load = useCallback(async () => {
    setInProgress(true);
    const { latest, value } = await request((abort) =>
      executeCommand<QueriesResult, DayRange & { sort: string; page: number }>("Queries", { ...range, sort, page }, undefined, abort),
    );
    if (!latest) {
      return;
    }
    setInProgress(false);
    setFailed(value === undefined);
    setError(value?.error ?? undefined);
    if (value !== undefined && !value.error) {
      setResult(value);
    }
  }, [executeCommand, request, range, sort, page]);

  useEffect(() => {
    void load();
  }, [load]);

  // The 31.9.1 ComponentCell renders `component` as a component type, not an element.
  const actionsFor = (row: QueryRow): ComponentCell => ({
    type: CellType.Component,
    columnName: "actions",
    component: () => (
      <RowActions
        row={row}
        canManageRules={props.canManageRules}
        canManageSynonyms={props.canManageSynonyms}
        queryTesterPath={props.queryTesterPath}
        ruleCreatePath={props.ruleCreatePath}
        synonymCreatePath={props.synonymCreatePath}
        onBlock={props.onBlock}
      />
    ),
  });

  const rows: TableRow[] | undefined = useMemo(() => result?.rows.map((row, i) => ({
    identifier: i,
    disabled: false,
    cells: [
      cell(row.isBrowse ? BROWSE : (row.queryText ?? DASH), "query"),
      cell(formatCount(row.searches), "searches"),
      cell(formatRate(row.noResultRate), "noResults"),
      cell(formatRate(row.ctr), "ctr"),
      cell(formatPosition(row.avgClickPosition), "position"),
      cell(percentileLabel(row.latencyP95, bucketBounds), "p95"),
      actionsFor(row),
    ],
  })),
  // actionsFor only reads the props below.
  [result, bucketBounds, props.canManageRules, props.canManageSynonyms, props.queryTesterPath, props.ruleCreatePath, props.synonymCreatePath, props.onBlock]);
  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / result.pageSize)) : 1;

  return (
    <Card headline={cardHeadline("Search terms", SORTS.find((s) => s.id === sort)?.title ?? SORTS[0].title)}>
      <Stack spacing={Spacing.M}>
        <NameToggleButtons selectedItemId={sort} items={SORTS.map((s) => ({ id: s.id, label: s.label }))} onChange={onSortChange} />
        {failed && <NotificationBarAlert>The queries could not be loaded. The last loaded table is shown; change the sort or period to retry.</NotificationBarAlert>}
        {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
        {inProgress && <Spinner />}
        {result !== undefined && result.rows.length === 0 ? (
          <p style={{ margin: 0, color: "var(--color-text-default-on-light)" }}>
            {sort === "NoResults" ? "No reported query was missing results in this period." : "No query reached the reporting threshold in this period."}
          </p>
        ) : (
          <Table columns={COLUMNS} rows={result === undefined ? undefined : rows} />
        )}
        {totalPages > 1 && <Pagination selectedPage={page} totalPages={totalPages} onPageChange={onPageChange} />}
        {result?.other && (
          <LabelWithTooltip
            label={`(other): ${formatCount(result.other.searches)} searches from queries below the reporting threshold`}
            tooltipText="Queries are named only after enough searches on enough distinct days (Surfacing settings). Everything else in the period, and texts already folded by retention, is counted here."
          />
        )}
      </Stack>
    </Card>
  );
};
