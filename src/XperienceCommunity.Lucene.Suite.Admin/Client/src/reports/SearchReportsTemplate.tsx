import { useCallback, useEffect, useState, type ReactNode } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  Callout,
  CalloutPlacementType,
  CalloutType,
  Card,
  CellType,
  Cols,
  Column,
  ColumnContentType,
  DateTimeRangeInput,
  FormItemWrapper,
  Headline,
  HeadlineSize,
  InfoCard,
  NameToggleButtons,
  NotificationBarAlert,
  Row,
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
import {
  CUSTOM,
  PRESETS,
  addDays,
  bucketLabels,
  countDelta,
  dayLabel,
  formatCount,
  formatPosition,
  formatRate,
  defaultRange,
  isAllowed,
  percentileLabel,
  positionDelta,
  presetOf,
  presetRange,
  previousRange,
  rangeLabel,
  rateDelta,
  toDay,
  toPickerDate,
  type DayRange,
  type Percentile,
} from "./report.ts";
import { BarChart } from "./TrendChart.tsx";

interface SearchReportsProps {
  readonly today: string;
  readonly maxDays: number;
}

interface Totals {
  readonly searches: number;
  readonly zeroResultSearches: number;
  readonly clickedSearches: number;
  readonly conversions: number;
  readonly sessionDays: number;
  readonly noResultRate: number | null;
  readonly ctr: number | null;
  readonly conversionRate: number | null;
  readonly avgClickPosition: number | null;
}

interface ReportDay {
  readonly day: string;
  readonly searches: number;
  readonly zeroResultSearches: number;
  readonly clickedSearches: number;
  readonly ctr: number | null;
  readonly isComplete: boolean;
}

interface ReportResult {
  readonly error?: string | null;
  readonly current: Totals;
  readonly previous: Totals;
  readonly deltas: { readonly searches: number | null; readonly sessionDays: number | null };
  readonly previousIsPartial: boolean;
  readonly firstDataDay: string | null;
  readonly completeThrough: string | null;
  readonly days: ReportDay[];
  readonly latency: {
    readonly p50: Percentile | null;
    readonly p95: Percentile | null;
    readonly p99: Percentile | null;
    readonly buckets: number[];
    readonly bucketUpperBoundsMs: number[];
  };
}

const DEFINITIONS = {
  searches: "Logical searches: as-you-type requests merged into one search, counted on the UTC day they started.",
  noResults: "Searches whose results were empty, divided by searches.",
  ctr: "Searches with at least one result click, divided by searches.",
  conversion: "Searches followed by at least one attributed conversion, divided by searches.",
  position: "Average 1-based position of clicked results.",
  sessions: "Sum of each day's distinct sessions; a session active on two days counts twice.",
};

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

const heading = (level: 2 | 3, children: ReactNode) => (
  <span role="heading" aria-level={level}>
    {children}
  </span>
);

export const SearchReportsTemplate = ({ today, maxDays }: SearchReportsProps) => {
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [range, setRange] = useState<DayRange>(() => defaultRange(today, maxDays));
  const [preset, setPreset] = useState(() => presetOf(range, today, maxDays));
  const [result, setResult] = useState<ReportResult>();
  const [error, setError] = useState<string>();
  const [failed, setFailed] = useState(false);
  const [inProgress, setInProgress] = useState(false);
  const [trendTable, setTrendTable] = useState(false);
  const [latencyTable, setLatencyTable] = useState(false);

  const load = useCallback(
    async (target: DayRange) => {
      setInProgress(true);
      const { latest, value } = await request((abort) =>
        executeCommand<ReportResult, DayRange>("Report", target, undefined, abort),
      );
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
    void load(range);
  }, [load, range]);

  const choosePreset = (id: string) => {
    setPreset(id);
    const match = PRESETS.find((p) => p.id === id);
    if (match) {
      setRange(presetRange(match.days, today, maxDays));
    }
  };

  const chooseCustom = (value: { from: Date; to: Date } | null) => {
    if (!value) {
      return;
    }
    const picked = { from: toDay(value.from), to: toDay(value.to) };
    if (isAllowed(picked, today, maxDays)) {
      setRange(picked);
    }
  };

  const minDay = addDays(today, 1 - maxDays);
  const previous = previousRange(range);
  const current = result?.current;
  const partial = result?.previousIsPartial ?? false;
  const notFinal = result?.days.filter((day) => !day.isComplete).length ?? 0;
  const bounds = result?.latency.bucketUpperBoundsMs ?? [];
  const peak = result?.days.reduce<ReportDay | undefined>((best, day) => (best === undefined || day.searches > best.searches ? day : best), undefined);

  return (
    <Stack spacing={Spacing.XL}>
      <div role="heading" aria-level={1}>
        <Headline size={HeadlineSize.M}>Search reports</Headline>
      </div>
      <Card headline={heading(2, "Period")}>
        <Stack spacing={Spacing.M}>
          <NameToggleButtons
            selectedItemId={preset}
            items={[...PRESETS.map((p) => ({ id: p.id, label: p.label })), { id: CUSTOM, label: "Custom" }]}
            onChange={choosePreset}
          />
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
          <Row spacingX={Spacing.XL}>
            <Column>
              <TextWithLabel label="Reporting (UTC days)" value={rangeLabel(range)} />
            </Column>
            <Column>
              <TextWithLabel label="Compared with" value={rangeLabel(previous)} />
            </Column>
            <Column>{inProgress && <Spinner />}</Column>
          </Row>
        </Stack>
      </Card>
      {failed && <NotificationBarAlert>The report could not be loaded. The last loaded report is shown; choose the period again to retry.</NotificationBarAlert>}
      {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
      {result === undefined && !failed && !error && <Spinner />}
      {result !== undefined && current !== undefined && (
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
            <Card headline={heading(2, "No searches")}>
              <p style={{ margin: 0, color: "var(--color-text-default-on-light)" }}>No searches were recorded on this index in the selected period.</p>
            </Card>
          ) : (
            <>
              <Row spacingX={Spacing.L} spacingY={Spacing.L}>
                {[
                  { caption: "Searches", tooltip: DEFINITIONS.searches, text: formatCount(current.searches), details: countDelta(result.deltas.searches, partial) },
                  { caption: "No-result rate", tooltip: DEFINITIONS.noResults, text: formatRate(current.noResultRate), details: rateDelta(current.noResultRate, result.previous.noResultRate, partial) },
                  { caption: "Click-through rate", tooltip: DEFINITIONS.ctr, text: formatRate(current.ctr), details: rateDelta(current.ctr, result.previous.ctr, partial) },
                  { caption: "Conversion rate", tooltip: DEFINITIONS.conversion, text: formatRate(current.conversionRate), details: rateDelta(current.conversionRate, result.previous.conversionRate, partial) },
                  { caption: "Avg. click position", tooltip: DEFINITIONS.position, text: formatPosition(current.avgClickPosition), details: positionDelta(current.avgClickPosition, result.previous.avgClickPosition, partial) },
                  { caption: "Session-days", tooltip: DEFINITIONS.sessions, text: formatCount(current.sessionDays), details: countDelta(result.deltas.sessionDays, partial) },
                ].map((kpi) => (
                  <Column key={kpi.caption} cols={Cols.Col6} colsMd={Cols.Col4} colsLg={Cols.Col4}>
                    <InfoCard {...kpi} />
                  </Column>
                ))}
              </Row>
              <Card headline={heading(2, "Searches per day")}>
                <Stack spacing={Spacing.M}>
                  <Switch label="Show as table" size={SwitchSize.M} value={trendTable} onChange={setTrendTable} />
                  {trendTable ? (
                    <Table
                      columns={[column("day", "Day"), column("searches", "Searches"), column("zero", "No results"), column("ctr", "CTR"), column("final", "Final")]}
                      rows={result.days.map(
                        (day, i): TableRow => ({
                          identifier: i,
                          disabled: false,
                          cells: [
                            cell(dayLabel(day.day), "day"),
                            cell(formatCount(day.searches), "searches"),
                            cell(formatCount(day.zeroResultSearches), "zero"),
                            cell(formatRate(day.ctr), "ctr"),
                            cell(day.isComplete ? "Yes" : "Not yet", "final"),
                          ],
                        }),
                      )}
                    />
                  ) : (
                    <BarChart
                      bars={result.days.map((day) => ({ label: dayLabel(day.day, undefined, true), value: day.searches, overlay: day.zeroResultSearches, muted: !day.isComplete }))}
                      summary={`Searches per day from ${rangeLabel(range)}: ${formatCount(current.searches)} in total${peak ? `, most on ${dayLabel(peak.day)} (${formatCount(peak.searches)})` : ""}. Use "Show as table" for each day's values.`}
                      valueLabel="Searches"
                      overlayLabel="No results"
                      mutedLabel="Not final yet"
                      formatValue={formatCount}
                    />
                  )}
                </Stack>
              </Card>
              <Card headline={heading(2, "Latency")}>
                <Stack spacing={Spacing.M}>
                  <Row spacingX={Spacing.XL}>
                    <Column>
                      <TextWithLabel label="Median (p50)" value={percentileLabel(result.latency.p50, bounds)} />
                    </Column>
                    <Column>
                      <TextWithLabel label="p95" value={percentileLabel(result.latency.p95, bounds)} />
                    </Column>
                    <Column>
                      <TextWithLabel label="p99" value={percentileLabel(result.latency.p99, bounds)} />
                    </Column>
                  </Row>
                  <Switch label="Show histogram as table" size={SwitchSize.M} value={latencyTable} onChange={setLatencyTable} />
                  {latencyTable ? (
                    <Table
                      columns={[column("bucket", "Latency"), column("searches", "Searches")]}
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
                      summary={`Searches by latency: median ${percentileLabel(result.latency.p50, bounds)}, p95 ${percentileLabel(result.latency.p95, bounds)}. Use "Show histogram as table" for each bucket.`}
                      valueLabel="Searches"
                      formatValue={formatCount}
                    />
                  )}
                </Stack>
              </Card>
            </>
          )}
        </>
      )}
    </Stack>
  );
};
