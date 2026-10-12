import { useCallback, useEffect, useMemo, useReducer, useRef, useState, type MutableRefObject, type ReactNode, type RefObject } from "react";
import { RoutingContentPlaceholder, usePageCommandProvider } from "@kentico/xperience-admin-base";
import { useLocation, useNavigate } from "react-router-dom";
import {
  BorderRadius,
  Button,
  ButtonColor,
  ButtonSize,
  Callout,
  CalloutPlacementType,
  CalloutType,
  CellType,
  ColumnContentType,
  Dialog,
  Headline,
  HeadlineSize,
  Icon,
  Input,
  Link,
  LinkButton,
  MenuItem,
  NameToggleButtons,
  NotificationBarAlert,
  NotificationBarWarning,
  Pagination,
  RadioButton,
  RadioGroup,
  Select,
  SortType,
  Spacing,
  Spinner,
  Stack,
  Table,
  type ComponentCell,
  type IconName,
  type SortModel,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";
import { pageStyle } from "../layout.ts";
import { returnFocus } from "../query-tester/focus.ts";
import { adminHref, latestRequest } from "../query-tester/state.ts";
import { visuallyHidden } from "../reports/dashboard.tsx";
import { indexFromPath } from "../reports/handoff.ts";
import {
  PAGE_SIZES,
  STATUSES,
  bulkTarget,
  deleteDialogNames,
  loadView,
  pageRange,
  parsePriority,
  partialFailureText,
  rangeText,
  rulesText,
  saveView,
  selectedCount,
  selectionFrom,
  statusCounts,
  statusLabel,
  transportFailureText,
  viewReducer,
  type BulkAction,
  type BulkResult,
  type PriorityMode,
  type RuleListItem,
  type RuleListResult,
  type RuleSort,
  type RuleStatus,
  type StatusFilter,
} from "./listing.ts";

export interface RuleListingTemplateProps {
  readonly canManage: boolean;
  readonly createPath: string;
  readonly indexName: string;
}

const TEXT = "var(--color-text-default-on-light)";
const LOW = "var(--color-text-low-emphasis)";
const LOAD_ERROR = "The rules could not be loaded. Check the connection and try again.";

const STATUS_DISPLAY: Record<RuleStatus, { readonly icon: IconName; readonly color: string }> = {
  Active: { icon: "xp-check-circle", color: "var(--color-success-text)" },
  Scheduled: { icon: "xp-clock", color: "var(--color-warning-text)" },
  Expired: { icon: "xp-times-circle", color: "var(--color-alert-text)" },
  Disabled: { icon: "xp-minus-circle", color: LOW },
};

const column = (name: string, caption: string, maxWidth: number, minWidth: number, sortable = false, component = false): TableColumn => ({
  name,
  caption,
  visible: true,
  minWidth,
  maxWidth,
  contentType: component ? ColumnContentType.Component : ColumnContentType.Text,
  sortable,
  searchable: false,
});

const COLUMNS = [
  column("name", "Name", 40, 14, true, true),
  column("priority", "Priority", 12, 11, true),
  column("status", "Status", 14, 12, false, true),
  column("validFrom", "Valid from (UTC)", 18, 17),
  column("validUntil", "Valid until (UTC)", 18, 17),
  column("conditions", "Conditions", 30, 12),
  column("actions", "Actions", 22, 10),
  // npm Table shows the column name for an empty caption; native listings leave the row-action header blank.
  column("rowActions", "\u00a0", 9, 7, false, true),
];

// Below this table width the date columns are hidden (the status already says whether a rule is live) so headers never truncate.
const DATES_MIN_WIDTH = 1100;
const DATE_COLUMNS = new Set(["validFrom", "validUntil"]);

const text = (value: string, columnName: string): StringCell => ({ type: CellType.String, value, columnName, tooltipText: value || undefined });

const StatusText = ({ status }: { readonly status: RuleStatus }) => (
  <span style={{ display: "inline-flex", alignItems: "center", gap: Spacing.XS, whiteSpace: "nowrap", color: STATUS_DISPLAY[status].color }}>
    <span aria-hidden="true" style={{ display: "inline-flex" }}>
      <Icon name={STATUS_DISPLAY[status].icon} />
    </span>
    {status}
  </span>
);

const editHref = (row: RuleListItem) => adminHref(row.editPath, window.location.pathname);

interface RowHandlers {
  readonly remove: (row: RuleListItem) => void;
  readonly edit: (row: RuleListItem) => void;
}

// Plain left clicks navigate inside the admin router; modified clicks keep the browser's own link behaviour.
const isPlainClick = (event: React.MouseEvent) => event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;

const RowActions = ({ row, canManage, handlers }: { readonly row: RuleListItem; readonly canManage: boolean; readonly handlers: MutableRefObject<RowHandlers> }) => (
  <div style={{ display: "flex", flexWrap: "nowrap", alignItems: "center", gap: Spacing.XXS }}>
    <Button
      icon="xp-edit"
      size={ButtonSize.S}
      color={ButtonColor.Quinary}
      borderless
      aria-label={`Edit rule "${row.name}"`}
      title="Edit"
      onClick={() => handlers.current.edit(row)}
    />
    {canManage && (
      <Button
        icon="xp-bin"
        size={ButtonSize.S}
        color={ButtonColor.Quinary}
        borderless
        destructive
        aria-label={`Delete rule "${row.name}"`}
        title="Delete"
        onClick={() => handlers.current.remove(row)}
      />
    )}
  </div>
);

/** Kentico's Input and Select take no aria-label prop; it is set on the native input. */
const useAriaLabel = (ref: RefObject<HTMLInputElement>, label: string) =>
  useEffect(() => {
    ref.current?.setAttribute("aria-label", label);
  });

interface ConfirmProps {
  readonly isOpen: boolean;
  readonly headline: string;
  readonly confirmLabel: string;
  readonly destructive?: boolean;
  readonly disabled?: boolean;
  readonly busy: boolean;
  readonly error?: string;
  readonly returnFocus: boolean;
  readonly onConfirm: () => void;
  readonly onClose: () => void;
  readonly onAfterOpen?: () => void;
  readonly children: ReactNode;
}

const ConfirmDialog = (props: ConfirmProps) => (
  <Dialog
    isOpen={props.isOpen}
    headline={props.headline}
    headerCloseButton={{ tooltipText: "Close" }}
    isDismissable={!props.busy}
    shouldReturnFocusAfterClose={props.returnFocus}
    onAfterOpen={props.onAfterOpen}
    onClose={props.onClose}
    actionInProgress={props.busy}
    notificationBar={props.error ? <NotificationBarAlert>{props.error}</NotificationBarAlert> : undefined}
    cancelAction={{ label: "Cancel", onClick: props.onClose, disabled: props.busy }}
    confirmAction={{ label: props.confirmLabel, destructive: props.destructive, inProgress: props.busy, disabled: props.disabled, onClick: props.onConfirm }}
  >
    <div style={{ color: TEXT }}>{props.children}</div>
  </Dialog>
);

type DialogKind = "Disable" | "Delete" | "ChangePriority";

export const RuleListingTemplate = ({ canManage, createPath }: RuleListingTemplateProps) => {
  const [index] = useState(() => indexFromPath(window.location.pathname));
  // The view comes back from sessionStorage (e.g. after a rule's edit page), never from the URL.
  const [view, dispatch] = useReducer(viewReducer, index, loadView);
  const [search, setSearch] = useState(view.query);
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [result, setResult] = useState<RuleListResult>();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string>();
  const [reload, setReload] = useState(0);
  const [dialog, setDialog] = useState<DialogKind>();
  const [dialogError, setDialogError] = useState<string>();
  const [returnToOpener, setReturnToOpener] = useState(true);
  const [busy, setBusy] = useState(false);
  const running = useRef(false);
  const [notice, setNotice] = useState<{ readonly kind: "warning" | "alert"; readonly text: string }>();
  const [announcement, setAnnouncement] = useState("");
  const [priorityMode, setPriorityMode] = useState<PriorityMode>("shift");
  const [priorityText, setPriorityText] = useState("");
  const searchRef = useRef<HTMLInputElement>(null);
  const pageSizeRef = useRef<HTMLInputElement>(null);
  const firstRadioRef = useRef<HTMLInputElement>(null);
  useAriaLabel(searchRef, "Search rules");
  useAriaLabel(pageSizeRef, "Rules per page");

  const { query, status, sort, descending, page, pageSize } = view;
  const load = useCallback(async () => {
    setLoading(true);
    const { latest, value } = await request((abort) =>
      executeCommand<RuleListResult, object>("List", { query, status, sort, descending, page, pageSize }, undefined, abort),
    );
    if (!latest) {
      return;
    }
    setLoading(false);
    if (value === undefined || value.error) {
      // The last loaded rows stay on screen.
      setLoadError(value?.error ?? LOAD_ERROR);
      return;
    }
    setLoadError(undefined);
    setResult(value);
    if (value.page !== page) {
      dispatch({ type: "page", page: value.page });
    }
  }, [executeCommand, request, query, status, sort, descending, page, pageSize]);

  useEffect(() => {
    void load();
  }, [load, reload]);

  useEffect(() => saveView(index, view), [index, view]);

  useEffect(() => {
    const timer = setTimeout(() => dispatch({ type: "query", query: search }), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const total = result?.total ?? 0;
  const count = selectedCount(view, total);
  const counts = statusCounts(result?.counts);
  const pageRows = result?.rows ?? [];
  const selectedIds = view.allMatching ? pageRows.map((row) => row.id) : view.selected.map((row) => row.id);
  const pageAllSelected = pageRows.length > 0 && pageRows.every((row) => selectedIds.includes(row.id));

  const openDialog = (kind: DialogKind) => {
    setDialogError(undefined);
    setReturnToOpener(true);
    setDialog(kind);
  };

  // Read by the row action cells through a ref, so the cells never depend on changing state.
  const [showDates, setShowDates] = useState(true);
  const observer = useRef<ResizeObserver>();
  const tableBox = useCallback((box: HTMLDivElement | null) => {
    observer.current?.disconnect();
    if (box) {
      observer.current = new ResizeObserver(([entry]) => setShowDates(entry.contentRect.width >= DATES_MIN_WIDTH));
      observer.current.observe(box);
    }
  }, []);
  // npm 31.9.1 Table ignores TableColumn.visible, so hidden columns (and their cells) are left out.
  const shown = useCallback((name: string) => showDates || !DATE_COLUMNS.has(name), [showDates]);
  const columns = useMemo(() => COLUMNS.filter((c) => shown(c.name)), [shown]);

  const handlers = useRef<RowHandlers>({ remove: () => undefined, edit: () => undefined });
  const navigate = useNavigate();
  const location = useLocation();
  handlers.current = {
    // The router's pathname is relative to its basename, so adminHref yields a path the router can resolve.
    edit: (row) => navigate(adminHref(row.editPath, location.pathname)),
    remove: (row) => {
      dispatch({ type: "select", selected: [{ id: row.id, name: row.name, version: row.version }] });
      openDialog("Delete");
    },
  };

  const rows: TableRow[] | undefined = useMemo(
    () =>
      result?.rows.map((row) => ({
        identifier: row.id,
        disabled: false,
        cells: [
          {
            type: CellType.Component,
            columnName: "name",
            component: () => (
              <div title={row.name} style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                <span
                  onClickCapture={(event) => {
                    if (isPlainClick(event)) {
                      event.preventDefault();
                      handlers.current.edit(row);
                    }
                  }}
                >
                  <Link href={editHref(row)} text={row.name} />
                </span>
              </div>
            ),
          } satisfies ComponentCell,
          text(String(row.priority), "priority"),
          { type: CellType.Component, columnName: "status", component: () => <StatusText status={row.status} /> } satisfies ComponentCell,
          text(row.validFromUtc, "validFrom"),
          text(row.validUntilUtc, "validUntil"),
          text(row.conditions, "conditions"),
          text(row.actions, "actions"),
          {
            type: CellType.Component,
            columnName: "rowActions",
            component: () => <RowActions row={row} canManage={canManage} handlers={handlers} />,
          } satisfies ComponentCell,
        ].filter((cell) => shown(cell.columnName ?? "")),
      })),
    [result, canManage, shown],
  );

  const closeDialog = () => {
    if (!running.current) {
      setDialog(undefined);
    }
  };

  const run = async (action: BulkAction) => {
    if (running.current) {
      return;
    }
    running.current = true;
    setBusy(true);
    setDialogError(undefined);
    const target = bulkTarget(view, total);
    let value: BulkResult | undefined;
    try {
      value = await executeCommand<BulkResult, object>(action, action === "ChangePriority" ? { ...target, mode: priorityMode, value: priorityText } : target);
    } catch {
      value = undefined;
    }
    running.current = false;
    setBusy(false);
    if (value === undefined) {
      // Nothing is known to have changed: the dialog stays open for another try.
      if (dialog) {
        setDialogError(transportFailureText(action));
      } else {
        setNotice({ kind: "alert", text: transportFailureText(action) });
      }
      return;
    }
    const failure = value.error ?? partialFailureText(action, value.failed);
    setNotice(failure ? { kind: value.error ? "alert" : "warning", text: failure } : undefined);
    // The success toast comes from the command response; this repeats it for screen readers.
    setAnnouncement(failure ?? (value.succeeded > 0 ? `${rulesText(value.succeeded)} done.` : ""));
    setReturnToOpener(false);
    setDialog(undefined);
    dispatch({ type: "clearSelection" });
    setReload((n) => n + 1);
    // The opener (bulk bar or row) goes away with the selection or the rule.
    returnFocus(() => searchRef.current);
  };

  const clearFilters = () => {
    setSearch("");
    dispatch({ type: "clearFilters" });
  };

  const priorityValue = parsePriority(priorityText, priorityMode);
  const deleteNames = deleteDialogNames(view.allMatching ? pageRows.map((row) => row.name) : view.selected.map((row) => row.name), count);
  const { from, to } = pageRange(result?.page ?? page, result?.pageSize ?? pageSize, total);
  const totalPages = Math.max(1, Math.ceil(total / (result?.pageSize ?? pageSize)));
  const filtered = query !== "" || status !== "All";
  const createButton = (color: ButtonColor) => (
    <span
      onClickCapture={(event) => {
        if (isPlainClick(event)) {
          event.preventDefault();
          navigate(adminHref(createPath, location.pathname));
        }
      }}
    >
      <LinkButton label="Create rule" icon="xp-plus" color={color} href={adminHref(createPath, window.location.pathname)} />
    </span>
  );

  // Child pages (rule edit, create) render in place of the listing through the placeholder's routes.
  return (
    <RoutingContentPlaceholder>
      <div style={pageStyle}>
        <Stack spacing={Spacing.XL}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: Spacing.L }}>
            <div role="heading" aria-level={1}>
              <Headline size={HeadlineSize.M}>Rules</Headline>
            </div>
            {canManage && createButton(ButtonColor.Primary)}
          </div>
          {loadError && (
            <NotificationBarAlert actions={<Button label="Retry" size={ButtonSize.S} color={ButtonColor.Secondary} onClick={() => setReload((n) => n + 1)} />}>
              {loadError}
            </NotificationBarAlert>
          )}
          {notice?.kind === "warning" && <NotificationBarWarning onDismiss={() => setNotice(undefined)}>{notice.text}</NotificationBarWarning>}
          {notice?.kind === "alert" && <NotificationBarAlert onDismiss={() => setNotice(undefined)}>{notice.text}</NotificationBarAlert>}
          {/* The bulk bar takes the toolbar's place (same minimum height), so selecting a row never shifts the table. */}
          <div style={canManage && count > 0 ? { position: "sticky", top: Spacing.S, zIndex: 5 } : undefined}>
            <div aria-live="polite" style={visuallyHidden}>
              {announcement || (canManage && count > 0 ? `${rulesText(count)} selected` : "")}
            </div>
            {canManage && count > 0 ? (
              <div
                role="region"
                aria-label="Bulk actions"
                style={{
                  minHeight: Spacing.XXXXL,
                  boxSizing: "border-box",
                  display: "flex",
                  flexWrap: "wrap",
                  alignItems: "center",
                  gap: Spacing.M,
                  padding: `${Spacing.S} ${Spacing.S} ${Spacing.S} ${Spacing.L}`,
                  borderRadius: BorderRadius.RadiusM,
                  background: "var(--color-background-selected)",
                }}
              >
                <span style={{ fontWeight: 700, color: TEXT }}>{`${rulesText(count)} selected`}</span>
                {view.allMatching ? (
                  <span style={{ color: LOW }}>All matching rules are selected.</span>
                ) : (
                  pageAllSelected &&
                  total > pageRows.length && (
                    <Button
                      size={ButtonSize.S}
                      color={ButtonColor.Tertiary}
                      label={`Select all ${total.toLocaleString("en-US")} matching`}
                      onClick={() => {
                        setAnnouncement("");
                        dispatch({ type: "selectAllMatching" });
                      }}
                    />
                  )
                )}
                <div style={{ marginLeft: "auto", display: "flex", flexWrap: "wrap", gap: Spacing.S }}>
                  <Button size={ButtonSize.S} color={ButtonColor.Secondary} icon="xp-check-circle" label="Enable" inProgress={busy && !dialog} disabled={busy} onClick={() => void run("Enable")} />
                  <Button size={ButtonSize.S} color={ButtonColor.Secondary} icon="xp-minus-circle" label="Disable" disabled={busy} onClick={() => openDialog("Disable")} />
                  <Button size={ButtonSize.S} color={ButtonColor.Secondary} icon="xp-arrows-v" label="Change priority" disabled={busy} onClick={() => openDialog("ChangePriority")} />
                  <Button size={ButtonSize.S} color={ButtonColor.Secondary} destructive icon="xp-bin" label="Delete" disabled={busy} onClick={() => openDialog("Delete")} />
                  <Button
                    size={ButtonSize.S}
                    color={ButtonColor.Quinary}
                    icon="xp-times"
                    aria-label="Clear selection"
                    title="Clear selection"
                    onClick={() => dispatch({ type: "clearSelection" })}
                  />
                </div>
              </div>
            ) : (
            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: Spacing.L, minHeight: Spacing.XXXXL }}>
              <div role="search" style={{ width: "320px" }}>
                <Input
                  inputRef={searchRef}
                  placeholder="Search by name"
                  actionElement={<Icon name="xp-magnifier" />}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
              <NameToggleButtons
                selectedItemId={status}
                items={STATUSES.map((s) => ({ id: s, label: statusLabel(s, counts?.[s]) }))}
                onChange={(id) => dispatch({ type: "status", status: id as StatusFilter })}
              />
              {loading && result !== undefined && <Spinner />}
            </div>
            )}
          </div>
          {result === undefined ? (
            loading ? (
              <Spinner />
            ) : null
          ) : total === 0 ? (
            filtered ? (
              <Callout
                type={CalloutType.QuickTip}
                placement={CalloutPlacementType.OnDesk}
                headline="No rules match"
                actionButton={<Button label="Clear filters" color={ButtonColor.Secondary} onClick={clearFilters} />}
              >
                Try a different search or status.
              </Callout>
            ) : (
              <Callout
                type={CalloutType.QuickTip}
                placement={CalloutPlacementType.OnDesk}
                headline="No rules yet"
                actionButton={canManage ? createButton(ButtonColor.Secondary) : undefined}
              >
                Rules pin, boost or hide results for matching searches. Start from a query in Search reports{canManage ? ", or create one here." : "."}
              </Callout>
            )
          ) : (
            <div ref={tableBox}>
              <Table
                columns={columns}
                rows={rows}
                selectable={canManage}
                selectRowLabel="Select rule"
                selectAllRowsLabel="Select all rules on this page"
                preselectedRows={selectedIds}
                onSelectedItemsChange={(ids) => {
                  setAnnouncement("");
                  dispatch({ type: "select", selected: selectionFrom(ids, pageRows, view.selected) });
                }}
                sortModel={{ sortBy: sort, sortType: descending ? SortType.Desc : SortType.Asc }}
                onSortChange={(model: SortModel) =>
                  dispatch({ type: "sort", sort: (model.sortBy === "name" ? "name" : "priority") as RuleSort, descending: model.sortType === SortType.Desc })
                }
              />
              <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", justifyContent: "space-between", gap: Spacing.L, marginTop: Spacing.L }}>
                <span style={{ color: TEXT, fontVariantNumeric: "tabular-nums" }}>{rangeText(from, to, total, "rules")}</span>
                <div style={{ display: "flex", alignItems: "center", gap: Spacing.L }}>
                  <div style={{ width: "140px" }}>
                    <Select inputRef={pageSizeRef} value={String(pageSize)} onChange={(value) => dispatch({ type: "pageSize", pageSize: Number(value) })}>
                      {PAGE_SIZES.map((size) => (
                        <MenuItem key={size} primaryLabel={`${size} per page`} value={String(size)} selected={size === pageSize} />
                      ))}
                    </Select>
                  </div>
                  {totalPages > 1 && <Pagination selectedPage={result.page} totalPages={totalPages} onPageChange={(next) => dispatch({ type: "page", page: next })} />}
                </div>
              </div>
            </div>
          )}
        </Stack>
        <ConfirmDialog
          isOpen={dialog === "Delete"}
          headline={`Delete ${rulesText(count)}?`}
          confirmLabel="Delete"
          destructive
          busy={busy}
          error={dialogError}
          returnFocus={returnToOpener}
          onConfirm={() => void run("Delete")}
          onClose={closeDialog}
        >
          <Stack spacing={Spacing.M}>
            <span>
              {count === 1 ? "The rule will be permanently deleted. Searches stop using it immediately." : "These rules will be permanently deleted. Searches stop using them immediately."}
            </span>
            <ul style={{ margin: 0, paddingLeft: Spacing.L, color: LOW }}>
              {deleteNames.map((name, i) => (
                <li key={i}>{name}</li>
              ))}
            </ul>
          </Stack>
        </ConfirmDialog>
        <ConfirmDialog
          isOpen={dialog === "Disable"}
          headline={`Disable ${rulesText(count)}?`}
          confirmLabel="Disable"
          busy={busy}
          error={dialogError}
          returnFocus={returnToOpener}
          onConfirm={() => void run("Disable")}
          onClose={closeDialog}
        >
          Disabled rules stay in the list but stop affecting searches. You can enable them again at any time.
        </ConfirmDialog>
        <ConfirmDialog
          isOpen={dialog === "ChangePriority"}
          headline={`Change priority of ${rulesText(count)}`}
          confirmLabel="Apply"
          disabled={priorityValue === null}
          busy={busy}
          error={dialogError}
          returnFocus={returnToOpener}
          onAfterOpen={() => firstRadioRef.current?.focus()}
          onConfirm={() => void run("ChangePriority")}
          onClose={closeDialog}
        >
          <Stack spacing={Spacing.L}>
            <RadioGroup name="rule-priority-mode" label="Change" value={priorityMode} onChange={(value) => setPriorityMode(value as PriorityMode)}>
              <RadioButton value="shift" inputRef={firstRadioRef} caption="Adds the value to each rule's priority. Use a negative number to move rules earlier.">
                Shift by
              </RadioButton>
              <RadioButton value="set" caption="Gives every selected rule the same priority.">
                Set to
              </RadioButton>
            </RadioGroup>
            <div style={{ maxWidth: "200px" }}>
              <Input
                type="number"
                label={priorityMode === "set" ? "Priority" : "Shift by"}
                value={priorityText}
                explanationText="Lower numbers run first."
                invalid={priorityText !== "" && priorityValue === null}
                validationMessage={
                  priorityText !== "" && priorityValue === null ? (priorityMode === "set" ? "Enter a whole number of 1 or more." : "Enter a whole number.") : undefined
                }
                onChange={(e) => setPriorityText(e.target.value)}
              />
            </div>
          </Stack>
        </ConfirmDialog>
      </div>
    </RoutingContentPlaceholder>
  );
};
