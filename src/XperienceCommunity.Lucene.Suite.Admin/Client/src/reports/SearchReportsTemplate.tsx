import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  BorderRadius,
  Button,
  ButtonColor,
  ButtonSize,
  Callout,
  CalloutPlacementType,
  CalloutType,
  Card,
  CellType,
  Cols,
  Column,
  ColumnContentType,
  DateTimeRangeInput,
  Divider,
  DividerOrientation,
  FormItemWrapper,
  Headline,
  HeadlineSize,
  NameToggleButtons,
  NotificationBarAlert,
  Pagination,
  Paper,
  PaperElevation,
  Row,
  SidePanel,
  SidePanelSize,
  Spacing,
  Spinner,
  Stack,
  Switch,
  SwitchSize,
  Table,
  TextWithLabel,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state.ts";
import { returnFocus } from "../query-tester/focus.ts";
import {
  CUSTOM,
  METRICS,
  PRESETS,
  addDays,
  bucketLabels,
  dayLabel,
  defaultRange,
  deltaOf,
  deltaTone,
  formatCount,
  formatMetric,
  formatMetricDelta,
  formatRate,
  insights,
  isAllowed,
  lengthOf,
  metricById,
  percentileLabel,
  presetForLength,
  presetOf,
  presetRange,
  previousRange,
  rangeLabel,
  rollingAverage,
  series,
  shiftRange,
  toDay,
  toPickerDate,
  valueOf,
  type DayRange,
  type KpiDay,
  type KpiTotals,
  type Metric,
  type Percentile,
} from "./report.ts";
import { ACCENT, BORDER, DeltaText, Eyebrow, SectionHeading, cardHeadline, focusOnMount, panelStyle, visuallyHidden } from "./dashboard.tsx";
import { BarChart } from "./TrendChart.tsx";
import { indexFromPath, loadReportState, saveReportState } from "./handoff.ts";
import { NeedsAttention } from "./NeedsAttention.tsx";
import { QueryTable, SORTS } from "./QueryTable.tsx";
import { pageStyle } from "../layout.ts";

interface SearchReportsProps {
  readonly today: string;
  readonly maxDays: number;
  readonly canManageRules: boolean;
  readonly canManageSynonyms: boolean;
  readonly queryTesterPath: string;
  readonly ruleCreatePath: string;
  readonly synonymCreatePath: string;
}

interface ReportResult {
  readonly error?: string | null;
  readonly current: KpiTotals;
  readonly previous: KpiTotals;
  readonly previousIsPartial: boolean;
  readonly firstDataDay: string | null;
  readonly completeThrough: string | null;
  readonly days: KpiDay[];
  readonly latency: {
    readonly p50: Percentile | null;
    readonly p95: Percentile | null;
    readonly p99: Percentile | null;
    readonly buckets: number[];
    readonly bucketUpperBoundsMs: number[];
  };
}

/** Days per page of the trend table: one week, newest first. */
const TREND_PAGE = 7;

const VOLUME = METRICS.filter((m) => m.kind === "count");
const QUALITY = METRICS.filter((m) => m.kind !== "count");

const column = (name: string, caption: string): TableColumn => ({
  name,
  caption,
  visible: true,
  minWidth: 8,
  maxWidth: 24,
  contentType: ColumnContentType.Text,
  sortable: false,
  searchable: false,
});

const cell = (value: string, columnName: string): StringCell => ({ type: CellType.String, value, columnName });

/** A compact KPI tile; selecting it charts the metric. The definition is a tooltip and screen-reader text. */
const MetricTile = ({ metric, selected, result, onSelect }: { metric: Metric; selected: boolean; result: ReportResult; onSelect: () => void }) => {
  const value = valueOf(metric, result.current);
  const previous = valueOf(metric, result.previous);
  const delta = deltaOf(metric, value, previous, result.previousIsPartial);
  return (
    <button
      type="button"
      aria-pressed={selected}
      title={metric.definition}
      onClick={onSelect}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: Spacing.XXS,
        textAlign: "left",
        padding: Spacing.M,
        borderRadius: BorderRadius.RadiusM,
        border: selected ? `${Spacing.Micro} solid transparent` : BORDER,
        background: selected ? ACCENT : "var(--color-paper-background)",
        color: selected ? "var(--color-text-default-on-dark)" : "var(--color-text-default-on-light)",
        font: "inherit",
        cursor: "pointer",
      }}
    >
      <Eyebrow color={selected ? "inherit" : undefined}>{metric.caption}</Eyebrow>
      <span style={{ fontSize: "x-large", fontWeight: 700 }}>{formatMetric(metric, value)}</span>
      <span style={{ fontSize: "smaller" }}>
        <DeltaText text={formatMetricDelta(metric, value, previous, result.previousIsPartial, delta)} tone={deltaTone(metric, delta)} onAccent={selected} />
      </span>
      <span style={visuallyHidden}>{metric.definition}</span>
    </button>
  );
};

const MetricGroup = ({ label, metrics, flex, children }: { label: string; metrics: readonly Metric[]; flex: string; children: (metric: Metric) => ReactNode }) => (
  <div role="group" aria-label={`${label} metrics`} style={{ flex, minWidth: 0, display: "flex", flexDirection: "column", gap: Spacing.S }}>
    <Eyebrow>{label}</Eyebrow>
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(8.5rem, 1fr))", gap: Spacing.M }}>{metrics.map(children)}</div>
  </div>
);

const Fact = ({ label, value, detail }: { label: string; value: string; detail: ReactNode }) => (
  <div style={{ border: BORDER, borderRadius: BorderRadius.RadiusM, padding: Spacing.L, display: "flex", flexDirection: "column", gap: Spacing.XXS }}>
    <Eyebrow>{label}</Eyebrow>
    <span style={{ fontSize: "x-large", fontWeight: 700, color: "var(--color-text-default-on-light)" }}>{value}</span>
    <span style={{ fontSize: "smaller", color: "var(--color-text-low-emphasis)" }}>{detail}</span>
  </div>
);

export const SearchReportsTemplate = (props: SearchReportsProps) => {
  const { today, maxDays } = props;
  const [index] = useState(() => indexFromPath(window.location.pathname));
  // The page restores its last state for this index (e.g. after a row action), never from the URL.
  const [saved] = useState(() => {
    const state = loadReportState(index);
    return state && isAllowed(state, today, maxDays) && SORTS.some((s) => s.id === state.sort) ? state : null;
  });
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [range, setRange] = useState<DayRange>(() => (saved ? { from: saved.from, to: saved.to } : defaultRange(today, maxDays)));
  // A stepped range keeps its preset ("7 days" one week back is still a 7-day view).
  const [preset, setPreset] = useState(() =>
    saved?.preset === CUSTOM ? CUSTOM : (presetForLength(range, saved?.preset, maxDays) ?? presetOf(range, today, maxDays)),
  );
  const [sort, setSort] = useState<string>(() => saved?.sort ?? SORTS[0].id);
  const [page, setPage] = useState(() => saved?.page ?? 1);
  const [metric, setMetric] = useState<Metric>(() => metricById(saved?.metric));
  const [result, setResult] = useState<ReportResult>();
  const [error, setError] = useState<string>();
  const [failed, setFailed] = useState(false);
  const [inProgress, setInProgress] = useState(false);
  const [trendTable, setTrendTable] = useState(false);
  const [trendPage, setTrendPage] = useState(1);
  const [latencyTable, setLatencyTable] = useState(false);
  const [panel, setPanel] = useState<"breakdown" | "latency">();
  const breakdownButton = useRef<HTMLButtonElement>(null);
  const latencyButton = useRef<HTMLButtonElement>(null);

  const load = useCallback(
    async (target: DayRange) => {
      setInProgress(true);
      const { latest, value } = await request((abort) => executeCommand<ReportResult, DayRange>("Report", target, undefined, abort));
      if (!latest) {
        return;
      }
      setInProgress(false);
      setFailed(value === undefined);
      setError(value?.error ?? undefined);
      // A failed or rejected request keeps the last good report on screen.
      if (value !== undefined && !value.error) {
        setResult(value);
      }
    },
    [executeCommand, request],
  );

  useEffect(() => {
    saveReportState(index, { from: range.from, to: range.to, preset, sort, page, metric: metric.id });
  }, [index, range, preset, sort, page, metric]);

  useEffect(() => {
    void load(range);
  }, [load, range]);

  useEffect(() => {
    setTrendPage(1);
  }, [range, metric]);

  const choosePreset = (id: string) => {
    setPreset(id);
    const match = PRESETS.find((p) => p.id === id);
    if (match) {
      setRange(presetRange(match.days, today, maxDays));
      setPage(1);
    }
  };

  const chooseCustom = (value: { from: Date; to: Date } | null) => {
    if (!value) {
      return;
    }
    const picked = { from: toDay(value.from), to: toDay(value.to) };
    if (isAllowed(picked, today, maxDays)) {
      setRange(picked);
      setPage(1);
    }
  };

  const step = (direction: -1 | 1) => {
    const next = shiftRange(range, direction, today, maxDays);
    if (next) {
      setRange(next);
      setPage(1);
    }
  };

  const closePanel = () => {
    const opener = panel === "latency" ? latencyButton : breakdownButton;
    setPanel(undefined);
    returnFocus(() => opener.current);
  };

  const minDay = addDays(today, 1 - maxDays);
  const previous = previousRange(range);
  const current = result?.current;
  const partial = result?.previousIsPartial ?? false;
  const notFinal = result?.days.filter((day) => !day.isComplete).length ?? 0;
  const bounds = result?.latency.bucketUpperBoundsMs ?? [];
  const values = result ? series(result.days, metric) : [];
  const rolling = rollingAverage(values);
  const facts = result && insights(result.days, metric, result.current, result.previous, partial);
  const format = (value: number | null) => formatMetric(metric, value);
  const tile = (m: Metric) =>
    result && <MetricTile key={m.id} metric={m} selected={m.id === metric.id} result={result} onSelect={() => setMetric(m)} />;

  return (
    <div style={pageStyle}>
      <Stack spacing={Spacing.XL}>
        <div role="heading" aria-level={1}>
          <Headline size={HeadlineSize.M}>Search reports</Headline>
        </div>
        <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: Spacing.M }}>
          <NameToggleButtons
            selectedItemId={preset}
            items={[...PRESETS.map((p) => ({ id: p.id, label: p.label })), { id: CUSTOM, label: "Custom" }]}
            onChange={choosePreset}
          />
          <div style={{ display: "flex", alignItems: "center", gap: Spacing.XS }}>
            <Button
              icon="xp-chevron-left"
              size={ButtonSize.S}
              color={ButtonColor.Quinary}
              borderless
              aria-label={`Previous ${lengthOf(range)} days`}
              title={`Previous ${lengthOf(range)} days`}
              disabled={shiftRange(range, -1, today, maxDays) === null}
              onClick={() => step(-1)}
            />
            <span aria-live="polite" style={{ color: "var(--color-text-default-on-light)" }}>
              {`${rangeLabel(range)} (UTC)`}
              <span style={{ color: "var(--color-text-low-emphasis)" }}>{`, compared with ${rangeLabel(previous)}`}</span>
            </span>
            <Button
              icon="xp-chevron-right"
              size={ButtonSize.S}
              color={ButtonColor.Quinary}
              borderless
              aria-label={`Next ${lengthOf(range)} days`}
              title={`Next ${lengthOf(range)} days`}
              disabled={shiftRange(range, 1, today, maxDays) === null}
              onClick={() => step(1)}
            />
          </div>
          {inProgress && <Spinner />}
        </div>
        {preset === CUSTOM && (
          <FormItemWrapper label="Custom range">
            <DateTimeRangeInput
              value={{ from: toPickerDate(range.from), to: toPickerDate(range.to) }}
              minDate={toPickerDate(minDay)}
              maxDate={toPickerDate(today)}
              onChange={chooseCustom}
            />
          </FormItemWrapper>
        )}
        {failed && <NotificationBarAlert>The report could not be loaded. The last loaded report is shown; choose the period again to retry.</NotificationBarAlert>}
        {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
        {result === undefined && !failed && !error && <Spinner />}
        {result !== undefined && current !== undefined && facts && (
          <>
            {partial && (
              <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="No comparison">
                {`The previous period starts before the first data${result.firstDataDay ? ` (${dayLabel(result.firstDataDay)})` : ""}, so changes are not shown.`}
              </Callout>
            )}
            {notFinal > 0 && (
              <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="Recent days are not final">
                {result.completeThrough
                  ? `Days after ${dayLabel(result.completeThrough)} have not been rolled up yet; their numbers can still grow.`
                  : "No day has been rolled up yet, so every day can still change."}
              </Callout>
            )}
            {current.searches === 0 ? (
              <Card headline={cardHeadline("Overview", "No searches")}>
                <p style={{ margin: 0, color: "var(--color-text-default-on-light)" }}>No searches were recorded on this index in the selected period.</p>
              </Card>
            ) : (
              <>
                <Paper elevation={PaperElevation.S} borderRadius={BorderRadius.RadiusL}>
                  <div style={panelStyle}>
                    <Stack spacing={Spacing.M}>
                      <span style={visuallyHidden} role="heading" aria-level={2}>
                        Key metrics
                      </span>
                      <div style={{ display: "flex", flexWrap: "wrap", gap: Spacing.XL }}>
                        <MetricGroup label="Volume" metrics={VOLUME} flex="2 1 18rem">
                          {tile}
                        </MetricGroup>
                        <MetricGroup label="Quality" metrics={QUALITY} flex="4 1 18rem">
                          {tile}
                        </MetricGroup>
                      </div>
                      <span style={{ fontSize: "smaller", color: "var(--color-text-low-emphasis)" }}>Select a metric to chart it below.</span>
                    </Stack>
                  </div>
                </Paper>
                <div>
                  <Row spacingX={Spacing.L} spacingY={Spacing.L}>
                    <Column cols={Cols.Col12} colsMd={Cols.Col8} colsLg={Cols.Col8}>
                      <Paper elevation={PaperElevation.S} borderRadius={BorderRadius.RadiusL} fullHeight>
                        <div style={panelStyle}>
                          <Stack spacing={Spacing.L}>
                            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "flex-start", justifyContent: "space-between", gap: Spacing.M }}>
                              <div>
                                <SectionHeading eyebrow="Trend" title={`${metric.caption} per day`} />
                                <p style={{ margin: 0, color: "var(--color-text-low-emphasis)" }}>{metric.definition}</p>
                              </div>
                              <Button
                                label="Show breakdown"
                                icon="xp-list"
                                size={ButtonSize.S}
                                color={ButtonColor.Secondary}
                                buttonRef={breakdownButton}
                                aria-label={`Show the ${metric.caption.toLowerCase()} breakdown for this period`}
                                onClick={() => setPanel("breakdown")}
                              />
                            </div>
                            <Switch label="Show as table" size={SwitchSize.M} value={trendTable} onChange={setTrendTable} />
                            {trendTable ? (
                              <Stack spacing={Spacing.M}>
                                <Table
                                  columns={[column("day", "Day"), column("value", metric.caption), column("average", "7-day average"), column("final", "Final")]}
                                  rows={result.days
                                    .map((day, i) => ({ day, i }))
                                    .reverse()
                                    .slice((trendPage - 1) * TREND_PAGE, trendPage * TREND_PAGE)
                                    .map(
                                      ({ day, i }): TableRow => ({
                                        identifier: i,
                                        disabled: false,
                                        cells: [
                                          cell(dayLabel(day.day), "day"),
                                          cell(format(values[i]), "value"),
                                          cell(format(rolling[i]), "average"),
                                          cell(day.isComplete ? "Yes" : "Not yet", "final"),
                                        ],
                                      }),
                                    )}
                                />
                                {result.days.length > TREND_PAGE && (
                                  <Pagination selectedPage={trendPage} totalPages={Math.ceil(result.days.length / TREND_PAGE)} onPageChange={setTrendPage} />
                                )}
                              </Stack>
                            ) : (
                              <BarChart
                                bars={result.days.map((day, i) => ({ label: dayLabel(day.day, undefined, true), value: values[i] ?? 0, muted: !day.isComplete }))}
                                line={rolling}
                                lineLabel="7-day rolling average"
                                fractional={metric.kind !== "count"}
                                summary={`${metric.caption} per day from ${rangeLabel(range)}: ${format(facts.value)} for the period${facts.peak ? `, highest on ${dayLabel(facts.peak.day)} (${format(facts.peak.value)})` : ""}. Use "Show as table" for each day's values.`}
                                valueLabel={metric.caption}
                                mutedLabel="Not final yet"
                                formatValue={(v) => format(v)}
                              />
                            )}
                          </Stack>
                        </div>
                      </Paper>
                    </Column>
                    <Column cols={Cols.Col12} colsMd={Cols.Col4} colsLg={Cols.Col4}>
                      <Paper elevation={PaperElevation.S} borderRadius={BorderRadius.RadiusL} fullHeight>
                        <div style={panelStyle}>
                          <Stack spacing={Spacing.M}>
                            <SectionHeading eyebrow="Needs attention" title="Queries without results" />
                            <NeedsAttention
                              range={range}
                              canManageRules={props.canManageRules}
                              canManageSynonyms={props.canManageSynonyms}
                              queryTesterPath={props.queryTesterPath}
                              ruleCreatePath={props.ruleCreatePath}
                              synonymCreatePath={props.synonymCreatePath}
                            />
                            <Divider orientation={DividerOrientation.Horizontal} />
                            <SectionHeading eyebrow="Performance" title="Response time" level={3} size={HeadlineSize.S} />
                            <div style={{ display: "flex", flexWrap: "wrap", gap: Spacing.L }}>
                              <TextWithLabel label="Median" value={percentileLabel(result.latency.p50, bounds)} />
                              <TextWithLabel label="p95" value={percentileLabel(result.latency.p95, bounds)} />
                              <TextWithLabel label="p99" value={percentileLabel(result.latency.p99, bounds)} />
                            </div>
                            <div>
                              <Button
                                label="Response-time details"
                                icon="xp-graph"
                                size={ButtonSize.S}
                                color={ButtonColor.Tertiary}
                                buttonRef={latencyButton}
                                onClick={() => setPanel("latency")}
                              />
                            </div>
                          </Stack>
                        </div>
                      </Paper>
                    </Column>
                  </Row>
                </div>
                <QueryTable
                  range={range}
                  sort={sort}
                  page={page}
                  onSortChange={(next) => {
                    setSort(next);
                    setPage(1);
                  }}
                  onPageChange={setPage}
                  bucketBounds={bounds}
                  canManageRules={props.canManageRules}
                  canManageSynonyms={props.canManageSynonyms}
                  queryTesterPath={props.queryTesterPath}
                  ruleCreatePath={props.ruleCreatePath}
                  synonymCreatePath={props.synonymCreatePath}
                />
                <SidePanel
                  headline={`${metric.caption}: period breakdown`}
                  size={SidePanelSize.Stackable}
                  isVisible={panel === "breakdown"}
                  tooltips={{ close: "Close" }}
                  onClose={closePanel}
                >
                  {panel === "breakdown" && (
                    <div ref={focusOnMount} tabIndex={-1} style={{ outline: "none" }}>
                      <Stack spacing={Spacing.XL}>
                        <Stack spacing={Spacing.M}>
                          <SectionHeading eyebrow="At a glance" title={rangeLabel(range)} level={3} size={HeadlineSize.S} />
                          <Fact label={metric.kind === "count" ? "Total for the period" : "For the period"} value={format(facts.value)} detail={metric.definition} />
                          <Fact
                            label="Previous period"
                            value={format(facts.previous)}
                            detail={<DeltaText text={formatMetricDelta(metric, facts.value, facts.previous, partial, facts.delta)} tone={facts.tone} />}
                          />
                          <Fact
                            label="Average per day"
                            value={format(facts.dailyAverage)}
                            detail={metric.kind === "count" ? `Over all ${facts.totalDays} days` : "Over the days that had searches"}
                          />
                        </Stack>
                        <Stack spacing={Spacing.M}>
                          <SectionHeading eyebrow="Highest and lowest days" title="Final days only" level={3} size={HeadlineSize.S} />
                          <Fact label="Highest day" value={format(facts.peak?.value ?? null)} detail={facts.peak ? dayLabel(facts.peak.day) : "No final day yet"} />
                          <Fact label="Lowest day" value={format(facts.lowest?.value ?? null)} detail={facts.lowest ? dayLabel(facts.lowest.day) : "No final day yet"} />
                        </Stack>
                        <div
                          style={{
                            background: "var(--color-info-background-low-emphasis)",
                            border: `${Spacing.Micro} dashed var(--color-background-selected)`,
                            borderRadius: BorderRadius.RadiusM,
                            padding: Spacing.L,
                          }}
                        >
                          <Stack spacing={Spacing.S}>
                            <SectionHeading eyebrow="Patterns" title="What stands out" level={3} size={HeadlineSize.S} />
                            <TextWithLabel label="Days with searches" value={`${facts.activeDays} of ${facts.totalDays}`} />
                            <TextWithLabel
                              label="Latest final day vs its 7-day average"
                              value={facts.latest ? `${format(facts.latest.value)} vs ${format(facts.latest.average)} (${dayLabel(facts.latest.day)})` : "No final day yet"}
                            />
                            <TextWithLabel label="Searches with no results" value={formatRate(facts.noResultShare)} />
                          </Stack>
                        </div>
                      </Stack>
                    </div>
                  )}
                </SidePanel>
                <SidePanel
                  headline="Response time"
                  size={SidePanelSize.Stackable}
                  isVisible={panel === "latency"}
                  tooltips={{ close: "Close" }}
                  onClose={closePanel}
                >
                  {panel === "latency" && (
                    <div ref={focusOnMount} tabIndex={-1} style={{ outline: "none" }}>
                      <Stack spacing={Spacing.M}>
                        <span style={{ color: "var(--color-text-low-emphasis)" }}>{`How long searches took, ${rangeLabel(range)}.`}</span>
                        <Switch label="Show as table" size={SwitchSize.M} value={latencyTable} onChange={setLatencyTable} />
                        {latencyTable ? (
                          <Table
                            columns={[column("bucket", "Response time"), column("searches", "Searches")]}
                            rows={bucketLabels(bounds).map(
                              (label, i): TableRow => ({
                                identifier: i,
                                disabled: false,
                                cells: [cell(label, "bucket"), cell(formatCount(result.latency.buckets[i] ?? 0), "searches")],
                              }),
                            )}
                          />
                        ) : (
                          <BarChart
                            bars={bucketLabels(bounds).map((label, i) => ({ label, value: result.latency.buckets[i] ?? 0 }))}
                            summary={`Searches by response time: median ${percentileLabel(result.latency.p50, bounds)}, p95 ${percentileLabel(result.latency.p95, bounds)}. Use "Show as table" for each bucket.`}
                            valueLabel="Searches"
                            formatValue={formatCount}
                          />
                        )}
                      </Stack>
                    </div>
                  )}
                </SidePanel>
              </>
            )}
          </>
        )}
      </Stack>
    </div>
  );
};
