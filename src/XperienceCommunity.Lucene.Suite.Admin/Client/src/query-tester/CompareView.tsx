import { useCallback, useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  Card,
  Cols,
  Column,
  Headline,
  HeadlineSize,
  Icon,
  Inline,
  NotificationBarAlert,
  NotificationBarWarning,
  Row,
  Spacing,
  Spinner,
  Stack,
  Switch,
  SwitchSize,
  Table,
  Tag,
  TextWithLabel,
  type TableRow,
} from "@kentico/xperience-admin-components";
import { ContextPicker, type ContactGroupsState } from "./ContextPicker";
import { defaultSides, markerLabel, missingFrom, rankChanges, type Hit, type RankChange, type SearchResult, type SideSettings } from "./compare";
import { latestRequest, pinningRuleName, scoreRatio, type FacetSelection, type FiredRule } from "./state";
import { RuleChips, SynonymTags, TitleCell, cell, column as tableColumn, componentCell } from "./ResultParts";
import { DataBar } from "../reports/patterns";
import { visuallyHidden } from "../reports/dashboard";

const ComparePageSize = 20;

const BASELINE_COLUMNS = [tableColumn("position", "#", 5, 5), tableColumn("title", "Title"), tableColumn("score", "Score", 22, 20)];
const TUNED_COLUMNS = [tableColumn("position", "#", 5, 5), tableColumn("title", "Title"), tableColumn("moved", "Moved", 10, 8)];

interface CompareViewProps {
  readonly query: string;
  readonly selection: FacetSelection;
  readonly titleField: string | undefined;
  readonly maxContexts: number;
  readonly maxContextLength: number;
  readonly onOpenHit: (hit: Hit, firedRules: readonly FiredRule[], returnTo: HTMLElement) => void;
}

const titleOf = (hit: Hit, field: string | undefined) => (field && hit.fields[field]?.[0]) || hit.itemGuid || "(no title)";

const hitsOf = (result: SearchResult | undefined) => (result && !result.error ? result.hits : []);

/** How far Tuned moved the document: arrow + n, "—" or a "New" Tag; the arrow and dash are announced as hidden text. */
const Moved = ({ change }: { change: RankChange }) => {
  const { text, hidden } = markerLabel(change);
  if (change.kind === "new") {
    return <Tag label={text} tooltipText={hidden} />;
  }
  const moved = change.kind === "up" || change.kind === "down";
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: Spacing.XXS,
        fontWeight: moved ? 600 : 400,
        color: change.kind === "up" ? "var(--color-success-text)" : change.kind === "down" ? "var(--color-alert-text)" : "var(--color-text-low-emphasis)",
      }}
    >
      {moved && (
        <span aria-hidden="true" style={{ display: "inline-flex", fontSize: "12px" }}>
          <Icon name={change.kind === "up" ? "xp-arrow-up" : "xp-arrow-down"} />
        </span>
      )}
      <span aria-hidden="true">{text}</span>
      <span style={visuallyHidden}>{hidden}</span>
    </span>
  );
};

const titleCell = (hit: Hit, index: number, rules: readonly FiredRule[], titleField: string | undefined) => {
  const pinnedBy = pinningRuleName(hit, rules);
  return componentCell("title", () => <TitleCell title={titleOf(hit, titleField)} pinnedAt={pinnedBy === null ? null : index + 1} pinnedBy={pinnedBy} />);
};

/** Runs one column's search whenever the shared query or its own settings change; stale answers are dropped. */
const useColumnSearch = (query: string, selection: FacetSelection, settings: SideSettings) => {
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [result, setResult] = useState<SearchResult>();
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      const { latest, value } = await request((abort) =>
        executeCommand<SearchResult, object>(
          "Search",
          {
            query,
            page: 1,
            pageSize: ComparePageSize,
            facets: selection,
            applyRules: settings.applyRules,
            applySynonyms: settings.applySynonyms,
            contexts: settings.contexts,
          },
          undefined,
          abort,
        ),
      );
      if (latest) {
        setLoading(false);
        setFailed(value === undefined);
        setResult(value);
      }
    })();
  }, [executeCommand, request, query, selection, settings]);

  return { result, loading, failed };
};

export const CompareView = ({ query, selection, titleField, maxContexts, maxContextLength, onOpenHit }: CompareViewProps) => {
  const { executeCommand } = usePageCommandProvider();
  const [sides, setSides] = useState(defaultSides);
  const [groups, setGroups] = useState<ContactGroupsState | null>();
  const baseline = useColumnSearch(query, selection, sides.baseline);
  const tuned = useColumnSearch(query, selection, sides.tuned);
  // Table rows cannot take focus, so the details panel returns focus to the column's table.
  const baselineTable = useRef<HTMLDivElement>(null);
  const tunedTable = useRef<HTMLDivElement>(null);

  const loadGroups = useCallback(() => {
    void (async () => {
      try {
        setGroups(undefined);
        setGroups((await executeCommand<ContactGroupsState, void>("ContactGroups")) ?? null);
      } catch {
        setGroups(null);
      }
    })();
  }, [executeCommand]);

  // Loaded once so both columns can label contact-group chips and know whether the picker is allowed.
  useEffect(loadGroups, [loadGroups]);

  const baselineRows = useMemo(() => {
    const hits = hitsOf(baseline.result);
    const rules = baseline.result?.firedRules ?? [];
    const top = Math.max(0, ...hits.map((hit) => hit.score));
    return hits.map(
      (hit, i): TableRow => ({
        identifier: i,
        disabled: false,
        cells: [
          cell(String(i + 1), "position"),
          titleCell(hit, i, rules, titleField),
          componentCell("score", () => <DataBar value={hit.score.toFixed(4)} ratio={scoreRatio(hit.score, top)} tone="info" />),
        ],
      }),
    );
  }, [baseline.result, titleField]);

  const tunedRows = useMemo(() => {
    const hits = hitsOf(tuned.result);
    const rules = tuned.result?.firedRules ?? [];
    const changes = baseline.result && !baseline.result.error ? rankChanges(baseline.result.hits, hits) : [];
    return hits.map((hit, i): TableRow => {
      const change = changes[i];
      return {
        identifier: i,
        disabled: false,
        cells: [
          cell(String(i + 1), "position"),
          titleCell(hit, i, rules, titleField),
          componentCell("moved", () => (change ? <Moved change={change} /> : <span />)),
        ],
      };
    });
  }, [baseline.result, tuned.result, titleField]);

  const column = (
    key: "baseline" | "tuned",
    name: string,
    search: ReturnType<typeof useColumnSearch>,
    rows: TableRow[],
    tableRef: RefObject<HTMLDivElement>,
  ) => {
    const settings = sides[key];
    const update = (next: Partial<SideSettings>) => setSides((current) => ({ ...current, [key]: { ...current[key], ...next } }));
    const result = search.result;
    const hits = hitsOf(result);
    const missing =
      key === "tuned" && baseline.result && !baseline.result.error && result && !result.error
        ? missingFrom(baseline.result.hits, hits)
        : [];
    const headingId = `compare-${key}-heading`;

    return (
      <Card
        headline={
          <span role="heading" aria-level={2} id={headingId}>
            {name}
          </span>
        }
      >
        <section aria-labelledby={headingId}>
          <Stack spacing={Spacing.M}>
            <Inline spacingX={Spacing.L} spacingY={Spacing.S}>
              <Switch
                label="Apply rules"
                size={SwitchSize.M}
                value={settings.applyRules}
                onChange={(value) => update({ applyRules: value })}
              />
              <Switch
                label="Apply synonyms"
                size={SwitchSize.M}
                value={settings.applySynonyms}
                onChange={(value) => update({ applySynonyms: value })}
              />
            </Inline>
            <ContextPicker
              id={`compare-${key}`}
              columnName={name}
              contexts={settings.contexts}
              onChange={(contexts) => update({ contexts })}
              groups={groups}
              onLoadGroups={loadGroups}
              maxContexts={maxContexts}
              maxContextLength={maxContextLength}
            />
            {search.failed && <NotificationBarAlert>Search failed. Change a setting to try again.</NotificationBarAlert>}
            {result?.error && <NotificationBarAlert>{result.error}</NotificationBarAlert>}
            {result?.rulesFailed && (
              <NotificationBarWarning>Rules could not be applied — these results are unruled.</NotificationBarWarning>
            )}
            {result?.synonymsFailed && (
              <NotificationBarWarning>Synonyms could not be applied — the original text was searched.</NotificationBarWarning>
            )}
            {search.loading && result === undefined ? (
              <Spinner />
            ) : (
              result &&
              !result.error && (
                <Stack spacing={Spacing.M}>
                  <TextWithLabel label="Total hits" value={result.totalHits.toLocaleString()} />
                  {settings.applyRules && result.firedRules.length > 0 && (
                    <Stack spacing={Spacing.XS}>
                      <span style={{ color: "var(--color-text-default-on-light)" }}>Fired rules:</span>
                      <RuleChips rules={result.firedRules} />
                    </Stack>
                  )}
                  {settings.applySynonyms && result.appliedSynonyms.length > 0 && (
                    <Stack spacing={Spacing.XS}>
                      <span style={{ color: "var(--color-text-default-on-light)" }}>Synonyms applied:</span>
                      <SynonymTags synonyms={result.appliedSynonyms} />
                    </Stack>
                  )}
                  {hits.length === 0 ? (
                    <span style={{ color: "var(--color-text-low-emphasis)" }}>No documents match.</span>
                  ) : (
                    <div ref={tableRef} tabIndex={-1} style={{ outline: "none", minWidth: 0, overflowX: "auto" }}>
                      <Table
                        columns={key === "baseline" ? BASELINE_COLUMNS : TUNED_COLUMNS}
                        rows={rows}
                        onRowClick={(id) => tableRef.current && onOpenHit(hits[id as number], result.firedRules, tableRef.current)}
                      />
                    </div>
                  )}
                  {missing.length > 0 && (
                    <Stack spacing={Spacing.XS}>
                      <div role="heading" aria-level={3}>
                        <Headline size={HeadlineSize.S}>{`Not in this column (${missing.length})`}</Headline>
                      </div>
                      <ul style={{ margin: 0, paddingLeft: Spacing.XL, color: "var(--color-text-low-emphasis)" }}>
                        {missing.map((hit) => (
                          <li key={hit.itemGuid}>{titleOf(hit, titleField)}</li>
                        ))}
                      </ul>
                    </Stack>
                  )}
                </Stack>
              )
            )}
          </Stack>
        </section>
      </Card>
    );
  };

  return (
    <Stack spacing={Spacing.XL}>
      <span style={{ color: "var(--color-text-low-emphasis)" }}>
        {`Each column runs the same query with its own settings. Arrows show how far Tuned moved each document compared with Baseline. Both columns show the first ${ComparePageSize} results. Contexts are sent as the site would send them; contact-group membership is not checked.`}
      </span>
      <div>
        <Row spacingX={Spacing.L} spacingY={Spacing.L}>
          <Column cols={Cols.Col12} colsMd={Cols.Col6} colsLg={Cols.Col6}>
            {column("baseline", "Baseline", baseline, baselineRows, baselineTable)}
          </Column>
          <Column cols={Cols.Col12} colsMd={Cols.Col6} colsLg={Cols.Col6}>
            {column("tuned", "Tuned", tuned, tunedRows, tunedTable)}
          </Column>
        </Row>
      </div>
    </Stack>
  );
};
