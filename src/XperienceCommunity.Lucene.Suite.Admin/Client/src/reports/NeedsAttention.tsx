import { useCallback, useEffect, useId, useState, type RefObject } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  ButtonSize,
  Headline,
  HeadlineSize,
  Link,
  NotificationBarAlert,
  NotificationBarInfo,
  Spacing,
  Spinner,
  Stack,
} from "@kentico/xperience-admin-components";
import { adminHref, latestRequest } from "../query-tester/state.ts";
import { BORDER, Eyebrow } from "./dashboard.tsx";
import { evidence, findingBar, fixLinks, groupByKind, kindInfo, summary, topFindings, type InsightRow, type InsightsResult } from "./insights.ts";
import { DataBar, Stale } from "./patterns.tsx";
import { RowActions, type QueriesResult, type QueryRow, type RowActionsProps } from "./QueryTable.tsx";
import { formatCount, formatRate, type DayRange } from "./report.ts";

/** The row-action settings shared by the card, the panel and the fallback list. */
export type ActionProps = Omit<RowActionsProps, "row" | "actions">;

export interface NeedsAttentionProps extends ActionProps {
  readonly range: DayRange;
  readonly bucketBounds: readonly number[];
  /** The last loaded findings; owned by the template so the all-insights panel shows the same data. */
  readonly insights: InsightsResult | undefined;
  readonly onInsights: (result: InsightsResult) => void;
  readonly onShowAll: () => void;
  readonly showAllButton: RefObject<HTMLButtonElement>;
}

const SHOWN = 5;
const LOW = "var(--color-text-low-emphasis)";

/** Finding items with their kind, evidence, existing fixes and the kind's actions; ids are unique per list instance. */
export const FindingList = ({ rows, label, bounds, actions }: { rows: readonly InsightRow[]; label: string; bounds: readonly number[]; actions: ActionProps }) => {
  const id = useId();
  return (
    <ol aria-label={label} style={{ listStyle: "none", margin: 0, padding: 0 }}>
      {rows.map((row, i) => {
        const info = kindInfo(row.kind);
        const links = fixLinks(row);
        const bar = findingBar(row);
        const queryId = `${id}-${i}`;
        return (
          <li key={`${row.kind}-${row.queryText}`} aria-labelledby={queryId} style={{ borderTop: i === 0 ? undefined : BORDER, padding: `${Spacing.S} 0` }}>
            <Stack spacing={Spacing.XS}>
              <Eyebrow>{info.label}</Eyebrow>
              <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: Spacing.S }}>
                <span id={queryId} style={{ minWidth: 0, fontWeight: 600, color: "var(--color-text-default-on-light)", overflowWrap: "anywhere" }}>
                  {row.queryText}
                </span>
                <span style={{ flexShrink: 0, fontSize: "smaller", color: LOW, fontVariantNumeric: "tabular-nums" }}>{`${formatCount(row.searches)} searches`}</span>
              </div>
              <DataBar value={bar.label} ratio={bar.ratio} tone={bar.tone} width={160} />
              <span style={{ fontSize: "smaller", color: LOW }}>{evidence(row, bounds)}</span>
              {links.length > 0 && (
                <div style={{ display: "flex", flexWrap: "wrap", alignItems: "baseline", columnGap: Spacing.S, fontSize: "smaller", color: LOW }}>
                  <span>Already applies:</span>
                  {links.map((link) => (
                    <Link key={link.key} href={adminHref(link.editUrl, window.location.pathname)} text={link.label} />
                  ))}
                </div>
              )}
              <RowActions row={{ queryText: row.queryText, isBrowse: false, hasPlaceholder: row.hasPlaceholder }} actions={info.actions} {...actions} />
            </Stack>
          </li>
        );
      })}
    </ol>
  );
};

const InsightSummary = ({ result }: { result: InsightsResult }) => (
  <Stack spacing={Spacing.S}>
    <span style={{ color: LOW }}>{summary(result)}</span>
    {result.annotationsFailed && <NotificationBarInfo>Existing fixes could not be checked.</NotificationBarInfo>}
  </Stack>
);

/** Every returned finding, grouped by kind (content of the all-insights side panel). */
export const AllInsights = ({ result, bounds, actions }: { result: InsightsResult; bounds: readonly number[]; actions: ActionProps }) => (
  <Stack spacing={Spacing.XL}>
    <InsightSummary result={result} />
    {groupByKind(result.findings).map((group) => {
      const info = kindInfo(group.kind);
      return (
        <Stack key={group.kind} spacing={Spacing.S}>
          <div role="heading" aria-level={3}>
            <Headline size={HeadlineSize.S}>{`${info.label} (${formatCount(group.rows.length)})`}</Headline>
          </div>
          <p style={{ margin: 0, color: LOW }}>{info.explanation}</p>
          <FindingList rows={group.rows} label={info.label} bounds={bounds} actions={actions} />
        </Stack>
      );
    })}
  </Stack>
);

/** Fallback when nothing passes the insight thresholds: the surfaced queries with the most no-result searches, as before insights. */
const NoResultQueries = ({ range, actions }: { range: DayRange; actions: ActionProps }) => {
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
    return <p style={{ margin: 0, color: LOW }}>Nothing needs attention in this period.</p>;
  }
  return (
    <Stack spacing={Spacing.S}>
      <p style={{ margin: 0, color: LOW }}>No findings at your thresholds. Top queries without results:</p>
      <ol aria-label="Queries without results" style={{ listStyle: "none", margin: 0, padding: 0 }}>
        {rows.map((row, i) => {
          const query = row.queryText ?? "";
          return (
            <li key={query} style={{ borderTop: i === 0 ? undefined : BORDER, padding: `${Spacing.S} 0` }}>
              <Stack spacing={Spacing.XS}>
                <span style={{ fontWeight: 600, color: "var(--color-text-default-on-light)", overflowWrap: "anywhere" }}>{query}</span>
                <span style={{ fontSize: "smaller", color: LOW }}>{`${formatCount(row.searches)} searches · ${formatRate(row.noResultRate)} without results`}</span>
                <RowActions
                  row={row}
                  canManageRules={actions.canManageRules}
                  canManageSynonyms={actions.canManageSynonyms}
                  queryTesterPath={actions.queryTesterPath}
                  ruleCreatePath={actions.ruleCreatePath}
                  synonymCreatePath={actions.synonymCreatePath}
                />
              </Stack>
            </li>
          );
        })}
      </ol>
    </Stack>
  );
};

/** The top insight findings, each one click from its fixes; falls back to the queries without results when there are none. */
export const NeedsAttention = (props: NeedsAttentionProps) => {
  const { range, bucketBounds, insights, onInsights, onShowAll, showAllButton, ...actions } = props;
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [failure, setFailure] = useState<string>();

  const load = useCallback(async () => {
    const { latest, value } = await request((abort) => executeCommand<InsightsResult, DayRange>("Insights", range, undefined, abort));
    if (!latest) {
      return;
    }
    setFailure(value === undefined ? "The findings could not be loaded. The last loaded findings are shown; change the period to retry." : (value.error ?? undefined));
    // A failed or rejected request keeps the last findings.
    if (value !== undefined && !value.error) {
      onInsights(value);
    }
  }, [executeCommand, request, range, onInsights]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <Stale.Provider value={failure !== undefined}>
      <Stack spacing={Spacing.M}>
        {failure && <NotificationBarAlert>{failure}</NotificationBarAlert>}
        {insights === undefined ? (
          !failure && <Spinner />
        ) : insights.findings.length === 0 ? (
          <NoResultQueries range={range} actions={actions} />
        ) : (
          <>
            <InsightSummary result={insights} />
            <FindingList rows={topFindings(insights.findings, SHOWN)} label="Top findings" bounds={bucketBounds} actions={actions} />
            {(insights.findings.length > SHOWN || insights.omitted > 0) && (
              <div>
                <Button label="Show all insights" icon="xp-list" size={ButtonSize.S} color={ButtonColor.Tertiary} buttonRef={showAllButton} onClick={onShowAll} />
              </div>
            )}
          </>
        )}
      </Stack>
    </Stale.Provider>
  );
};
