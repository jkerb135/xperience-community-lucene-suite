import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  BorderRadius,
  Button,
  ButtonColor,
  ButtonSize,
  CellType,
  ColumnContentType,
  NotificationBarAlert,
  Paper,
  PaperElevation,
  SidePanel,
  SidePanelSize,
  SortType,
  Spacing,
  Spinner,
  Stack,
  Table,
  Tag,
  type ComponentCell,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state.ts";
import { returnFocus } from "../query-tester/focus.ts";
import { QUERY_PAGE_SIZES } from "./handoff.ts";
import { SectionHeading, focusOnMount, panelStyle, visuallyHidden } from "./dashboard.tsx";
import { DataBar, RangeFooter, Stale } from "./patterns.tsx";
import { openTarget } from "./QueryTable.tsx";
import { DASH, ITEM_SORTS, barRatio, formatCount, formatPosition, itemActionReason, itemAriaSort, itemSortOf, type DayRange } from "./report.ts";

export interface ItemRow {
  readonly itemGuid: string;
  /** Null when the item is no longer in the index. */
  readonly title: string | null;
  readonly contentType: string | null;
  readonly clickedSearches: number;
  readonly conversions: number;
  readonly avgClickPosition: number | null;
}

interface TopItemsResult {
  readonly error?: string | null;
  readonly rows: ItemRow[];
  readonly totalCount: number;
  readonly pageSize: number;
  readonly clickedSearches: number;
  readonly conversions: number;
}

export interface TopItemsProps {
  readonly range: DayRange;
  readonly canManageRules: boolean;
  readonly ruleCreatePath: string;
}

type ItemActionsProps = Omit<TopItemsProps, "range">;

const CARD_ROWS = 10;
const LOW = "var(--color-text-low-emphasis)";

const column = (name: string, caption: string, maxWidth: number, minWidth: number): TableColumn => ({
  name,
  caption,
  visible: true,
  minWidth,
  maxWidth,
  contentType: ColumnContentType.Text,
  sortable: itemSortOf(name) !== null,
  searchable: false,
});

const COLUMNS = [
  column("title", "Item", 32, 12),
  column("contentType", "Content type", 16, 8),
  column("clicked", "Clicked searches", 20, 12),
  column("position", "Avg. position", 12, 8),
  column("conversions", "Conversions", 12, 8),
  column("actions", "Actions", 8, 8),
];

const cell = (value: string, columnName: string): StringCell => ({ type: CellType.String, value, columnName });

const ItemTitle = ({ row }: { row: ItemRow }) => (
  <span style={{ display: "flex", alignItems: "center", gap: Spacing.S, width: "100%", minWidth: 0, whiteSpace: "nowrap" }}>
    <span
      title={row.title ?? row.itemGuid}
      style={{ minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", color: row.title === null ? LOW : "var(--color-text-default-on-light)" }}
    >
      {row.title ?? row.itemGuid}
    </span>
    {row.title === null && (
      <span style={{ flexShrink: 0 }}>
        <Tag label="Not in this index" />
      </span>
    )}
  </span>
);

/** Icon buttons with full accessible names; a disabled button cannot take focus, so its reason is also hidden text. */
const ItemActions = ({ row, canManageRules, ruleCreatePath }: ItemActionsProps & { row: ItemRow }) => {
  const reason = itemActionReason(row.title, canManageRules);
  const name = row.title === null ? `item ${row.itemGuid}` : `"${row.title}"`;
  return (
    <div style={{ display: "flex", flexWrap: "nowrap", alignItems: "center", gap: Spacing.XXS }}>
      <Button
        icon="xp-pin"
        size={ButtonSize.S}
        color={ButtonColor.Quinary}
        borderless
        aria-label={`Pin ${name} in a new rule`}
        title={reason ?? "Pin in a new rule"}
        disabled={reason !== null}
        onClick={() => openTarget("rulePin", ruleCreatePath, row.itemGuid)}
      />
      <Button
        icon="xp-eye-slash"
        size={ButtonSize.S}
        color={ButtonColor.Quinary}
        borderless
        aria-label={`Hide ${name} in a new rule`}
        title={reason ?? "Hide in a new rule"}
        disabled={reason !== null}
        onClick={() => openTarget("ruleHide", ruleCreatePath, row.itemGuid)}
      />
      {reason && <span style={visuallyHidden}>{`Pin and hide unavailable: ${reason}.`}</span>}
    </div>
  );
};

/** One latest-request-wins page of clicked items; a failed or rejected request keeps the last result. */
const useTopItems = (range: DayRange, sort: string, page: number, pageSize: number) => {
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [result, setResult] = useState<TopItemsResult>();
  const [error, setError] = useState<string>();
  const [failed, setFailed] = useState(false);
  const [failures, setFailures] = useState(0);
  const [inProgress, setInProgress] = useState(false);

  const load = useCallback(async () => {
    setInProgress(true);
    const { latest, value } = await request((abort) =>
      executeCommand<TopItemsResult, DayRange & { sort: string; page: number; pageSize: number }>("TopItems", { ...range, sort, page, pageSize }, undefined, abort),
    );
    if (!latest) {
      return;
    }
    setInProgress(false);
    setFailed(value === undefined);
    setError(value?.error ?? undefined);
    if (value !== undefined && !value.error) {
      setResult(value);
    } else {
      setFailures((n) => n + 1);
    }
  }, [executeCommand, request, range, sort, page, pageSize]);

  useEffect(() => {
    void load();
  }, [load]);

  return { result, error, failed, failures, inProgress, load };
};

type ItemsState = ReturnType<typeof useTopItems>;

/**
 * The npm 31.9.1 sortable header: role="columnheader", Enter only, no aria-sort, and its own sort state that flips the
 * direction on every click. The table is remounted after each click so the header shows the sort's fixed direction,
 * focus goes back to the clicked header, and aria-sort is set on the header elements.
 */
const ItemTable = ({ rows, sort, onSort }: { rows: TableRow[] | undefined; sort: string; onSort: (sort: string) => void }) => {
  const table = useRef<HTMLDivElement>(null);
  const [mount, setMount] = useState(0);
  const refocus = useRef<number | null>(null);
  const current = ITEM_SORTS.find((s) => s.id === sort) ?? ITEM_SORTS[0];

  useEffect(() => {
    const headers = table.current?.querySelectorAll<HTMLElement>('[role="columnheader"]') ?? [];
    headers.forEach((header, i) => {
      const value = itemAriaSort(COLUMNS[i]?.name ?? "", sort);
      if (value === null) {
        header.removeAttribute("aria-sort");
      } else {
        header.setAttribute("aria-sort", value);
      }
    });
    if (refocus.current !== null) {
      headers[refocus.current]?.focus();
      refocus.current = null;
    }
  });

  return (
    <Table
      key={mount}
      ref={table}
      columns={COLUMNS}
      rows={rows}
      sortModel={{ sortBy: current.column, sortType: current.direction === "ascending" ? SortType.Asc : SortType.Desc }}
      onSortChange={(model) => {
        refocus.current = COLUMNS.findIndex((c) => c.name === model.sortBy);
        setMount((n) => n + 1);
        const next = itemSortOf(model.sortBy);
        if (next !== null) {
          onSort(next);
        }
      }}
    />
  );
};

/** Alerts, Spinner, the empty text and the table of one items request. */
const ItemsView = ({ state, sort, onSort, actions }: { state: ItemsState; sort: string; onSort: (sort: string) => void; actions: ItemActionsProps }) => {
  const { result, error, failed, inProgress, load } = state;
  const { canManageRules, ruleCreatePath } = actions;

  // Built from the result and stable props only: rebuilt cells remount the pressed button and drop its focus.
  const rows: TableRow[] | undefined = useMemo(() => {
    // Bars are scaled to the top value shown.
    const most = Math.max(0, ...(result?.rows.map((row) => row.clickedSearches) ?? []));
    // The 31.9.1 ComponentCell renders `component` as a component type, not an element.
    return result?.rows.map((row) => ({
      identifier: row.itemGuid,
      disabled: false,
      cells: [
        { type: CellType.Component, columnName: "title", component: () => <ItemTitle row={row} /> } satisfies ComponentCell,
        cell(row.contentType ?? DASH, "contentType"),
        {
          type: CellType.Component,
          columnName: "clicked",
          component: () => <DataBar value={formatCount(row.clickedSearches)} ratio={barRatio(row.clickedSearches, most)} tone="product" />,
        } satisfies ComponentCell,
        cell(formatPosition(row.avgClickPosition), "position"),
        cell(formatCount(row.conversions), "conversions"),
        {
          type: CellType.Component,
          columnName: "actions",
          component: () => <ItemActions row={row} canManageRules={canManageRules} ruleCreatePath={ruleCreatePath} />,
        } satisfies ComponentCell,
      ],
    }));
  }, [result, canManageRules, ruleCreatePath]);

  return (
    <>
      {failed && (
        <NotificationBarAlert actions={<Button label="Retry" size={ButtonSize.S} color={ButtonColor.Secondary} disabled={inProgress} onClick={() => void load()} />}>
          The clicked items could not be loaded. The last loaded items are shown.
        </NotificationBarAlert>
      )}
      {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
      {inProgress && <Spinner />}
      {result === undefined ? null : result.totalCount === 0 ? (
        <p style={{ margin: 0, color: LOW }}>No clicked items in this period.</p>
      ) : (
        <Stale.Provider value={failed || error !== undefined}>
          <ItemTable rows={rows} sort={sort} onSort={onSort} />
        </Stale.Provider>
      )}
    </>
  );
};

/** Content of the "All clicked items" panel: its own sort and paging, starting from the card's sort. */
const AllItems = ({ range, initialSort, actions }: { range: DayRange; initialSort: string; actions: ItemActionsProps }) => {
  const [sort, setSort] = useState(initialSort);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const state = useTopItems(range, sort, page, pageSize);
  const total = state.result?.totalCount ?? 0;

  useEffect(() => {
    setPage(1);
  }, [range]);

  return (
    <Stack spacing={Spacing.M}>
      <ItemsView
        state={state}
        sort={sort}
        onSort={(next) => {
          setSort(next);
          setPage(1);
        }}
        actions={actions}
      />
      {total > 0 && (
        <RangeFooter
          page={page}
          pageSize={pageSize}
          total={total}
          noun={total === 1 ? "item" : "items"}
          sizes={QUERY_PAGE_SIZES}
          sizeLabel="Items per page"
          onPageSize={(next) => {
            setPageSize(next);
            setPage(1);
          }}
          onPage={setPage}
          resetKey={state.failures}
        />
      )}
    </Stack>
  );
};

/** "Top clicked items": the range's 10 most clicked items, sortable, with an all-items panel and pin / hide shortcuts. */
export const TopItems = ({ range, canManageRules, ruleCreatePath }: TopItemsProps) => {
  const [sort, setSort] = useState<string>(ITEM_SORTS[0].id);
  const [open, setOpen] = useState(false);
  const showAll = useRef<HTMLButtonElement>(null);
  const card = useTopItems(range, sort, 1, CARD_ROWS);
  const actions = useMemo(() => ({ canManageRules, ruleCreatePath }), [canManageRules, ruleCreatePath]);
  const result = card.result;

  const close = () => {
    setOpen(false);
    returnFocus(() => showAll.current);
  };

  return (
    <>
      <Paper elevation={PaperElevation.S} borderRadius={BorderRadius.RadiusL}>
        <div style={panelStyle}>
          <Stack spacing={Spacing.M}>
            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "flex-start", justifyContent: "space-between", gap: Spacing.M }}>
              <div>
                <SectionHeading eyebrow="Popular content" title="Top clicked items" />
                {result !== undefined && result.totalCount > 0 && (
                  <p style={{ margin: 0, color: LOW }}>
                    {`${formatCount(result.totalCount)} ${result.totalCount === 1 ? "item" : "items"}, ${formatCount(result.clickedSearches)} clicked searches and ${formatCount(result.conversions)} conversions in this period.`}
                  </p>
                )}
              </div>
              {result !== undefined && result.totalCount > 0 && (
                <Button
                  label="Show all"
                  icon="xp-list"
                  size={ButtonSize.S}
                  color={ButtonColor.Secondary}
                  buttonRef={showAll}
                  aria-label="Show all clicked items"
                  onClick={() => setOpen(true)}
                />
              )}
            </div>
            <ItemsView state={card} sort={sort} onSort={setSort} actions={actions} />
          </Stack>
        </div>
      </Paper>
      <SidePanel headline="All clicked items" size={SidePanelSize.Stackable} isVisible={open} tooltips={{ close: "Close" }} onClose={close}>
        {open && (
          <div ref={focusOnMount} tabIndex={-1} style={{ outline: "none" }}>
            <AllItems range={range} initialSort={sort} actions={actions} />
          </div>
        )}
      </SidePanel>
    </>
  );
};
