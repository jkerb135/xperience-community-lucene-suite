import { useCallback, useEffect, useState } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import { NotificationBarAlert, Spacing, Spinner, Stack } from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state.ts";
import { BORDER } from "./dashboard.tsx";
import { RowActions, type QueriesResult, type QueryRow } from "./QueryTable.tsx";
import { formatCount, formatRate, type DayRange } from "./report.ts";

export interface NeedsAttentionProps {
  readonly range: DayRange;
  readonly canManageRules: boolean;
  readonly canManageSynonyms: boolean;
  readonly queryTesterPath: string;
  readonly ruleCreatePath: string;
  readonly synonymCreatePath: string;
}

const SHOWN = 5;

/** The surfaced queries with the most no-result searches, each one click from a rule or synonym. */
export const NeedsAttention = (props: NeedsAttentionProps) => {
  const { range } = props;
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [rows, setRows] = useState<QueryRow[]>();
  const [failed, setFailed] = useState(false);

  const load = useCallback(async () => {
    const { latest, value } = await request((abort) =>
      executeCommand<QueriesResult, DayRange & { sort: string; page: number }>("Queries", { ...range, sort: "NoResults", page: 1 }, undefined, abort),
    );
    if (!latest) {
      return;
    }
    setFailed(value === undefined || Boolean(value.error));
    if (value !== undefined && !value.error) {
      setRows(value.rows.filter((row) => !row.isBrowse && (row.noResultRate ?? 0) > 0).slice(0, SHOWN));
    }
  }, [executeCommand, request, range]);

  useEffect(() => {
    void load();
  }, [load]);

  if (failed) {
    return <NotificationBarAlert>The queries without results could not be loaded; change the period to retry.</NotificationBarAlert>;
  }
  if (rows === undefined) {
    return <Spinner />;
  }
  if (rows.length === 0) {
    return <p style={{ margin: 0, color: "var(--color-text-low-emphasis)" }}>No reported query was missing results in this period.</p>;
  }
  return (
    <ol aria-label="Queries without results" style={{ listStyle: "none", margin: 0, padding: 0 }}>
      {rows.map((row, i) => {
        const query = row.queryText ?? "";
        return (
          <li key={query} style={{ borderTop: i === 0 ? undefined : BORDER, padding: `${Spacing.S} 0` }}>
            <Stack spacing={Spacing.XS}>
              <span style={{ fontWeight: 600, color: "var(--color-text-default-on-light)", overflowWrap: "anywhere" }}>{query}</span>
              <span style={{ fontSize: "smaller", color: "var(--color-text-low-emphasis)" }}>
                {`${formatCount(row.searches)} searches · ${formatRate(row.noResultRate)} without results`}
              </span>
              <RowActions
                row={row}
                canManageRules={props.canManageRules}
                canManageSynonyms={props.canManageSynonyms}
                queryTesterPath={props.queryTesterPath}
                ruleCreatePath={props.ruleCreatePath}
                synonymCreatePath={props.synonymCreatePath}
              />
            </Stack>
          </li>
        );
      })}
    </ol>
  );
};
