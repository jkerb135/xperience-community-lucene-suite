import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
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
import { DASH, formatCount, formatPosition, formatRate, percentileLabel, type DayRange, type Percentile } from "./report.ts";

export const SORTS = [
  { id: "Searches", label: "Top" },
  { id: "NoResults", label: "No results" },
  { id: "LowestCtr", label: "Lowest CTR" },
  { id: "Slowest", label: "Slowest" },
] as const;

interface QueryRow {
  readonly queryText: string | null;
  readonly isBrowse: boolean;
  readonly hasPlaceholder: boolean;
  readonly searches: number;
  readonly ctr: number | null;
  readonly noResultRate: number | null;
  readonly avgClickPosition: number | null;
  readonly latencyP95: Percentile | null;
}

interface QueriesResult {
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
  column("noResults", "No-result rate"),
  column("ctr", "CTR"),
  column("position", "Avg. position"),
  column("p95", "p95 latency"),
  // Wide enough for the widest labelled button; the three are stacked.
  column("actions", "Actions", 18, 18),
];

const BROWSE = "(browse – no query text)";
const PLACEHOLDER = "Contains a removed value; a rule or synonym would never match";

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

  const open = (target: HandoffTarget, path: string, query: string) => {
    give(target, path, query);
    window.location.assign(adminHref(path, window.location.pathname));
  };

  // Labelled buttons rather than the npm ActionCell: in 31.9.1 its icon-only buttons are all announced as "button".
  // The 31.9.1 ComponentCell renders `component` as a component type, not an element.
  const actionsFor = (row: QueryRow): ComponentCell => {
    const query = row.queryText ?? "";
    const blocked = row.isBrowse ? "The browse row has no query text" : row.hasPlaceholder ? PLACEHOLDER : null;
    return {
      type: CellType.Component,
      columnName: "actions",
      component: () => (
        <Stack spacing={Spacing.XS}>
          <Button
            label="Query tester"
            icon="xp-magnifier"
            size={ButtonSize.S}
            color={ButtonColor.Tertiary}
            title="Open this query in the query tester"
            onClick={() => open("queryTester", props.queryTesterPath, query)}
          />
          <Button
            label="Create rule"
            icon="xp-plus-circle"
            size={ButtonSize.S}
            color={ButtonColor.Tertiary}
            disabled={blocked !== null || !props.canManageRules}
            title={blocked ?? (props.canManageRules ? "Create a rule for this query" : "Requires Site Search → Manage rules")}
            onClick={() => open("ruleCreate", props.ruleCreatePath, query)}
          />
          <Button
            label="Add synonym"
            icon="xp-arrows-h"
            size={ButtonSize.S}
            color={ButtonColor.Tertiary}
            disabled={blocked !== null || !props.canManageSynonyms}
            title={blocked ?? (props.canManageSynonyms ? "Add a synonym starting with this query" : "Requires Site Search → Manage synonyms")}
            onClick={() => open("synonymCreate", props.synonymCreatePath, query)}
          />
        </Stack>
      ),
    };
  };

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
  [result, bucketBounds, props.canManageRules, props.canManageSynonyms, props.queryTesterPath, props.ruleCreatePath, props.synonymCreatePath]);
  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / result.pageSize)) : 1;

  return (
    <Card headline={heading("Queries")}>
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

const heading = (children: ReactNode) => (
  <span role="heading" aria-level={2}>
    {children}
  </span>
);
