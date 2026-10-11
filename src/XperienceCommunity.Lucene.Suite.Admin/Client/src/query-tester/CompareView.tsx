import { useCallback, useEffect, useState } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  ButtonColor,
  Card,
  Cols,
  Column,
  Headline,
  HeadlineSize,
  Inline,
  LinkButton,
  NotificationBarAlert,
  NotificationBarWarning,
  Row,
  Spacing,
  Spinner,
  Stack,
  Switch,
  SwitchSize,
  TextWithLabel,
} from "@kentico/xperience-admin-components";
import { ContextPicker, type ContactGroupsState } from "./ContextPicker";
import { defaultSides, markerLabel, missingFrom, rankChanges, type Hit, type SearchResult, type SideSettings } from "./compare";
import { appliedSynonymRows, firedRuleRows, latestRequest, type FacetSelection, type FiredRule } from "./state";

const ComparePageSize = 20;

interface CompareViewProps {
  readonly query: string;
  readonly selection: FacetSelection;
  readonly titleField: string | undefined;
  readonly maxContexts: number;
  readonly maxContextLength: number;
  readonly onOpenHit: (hit: Hit, firedRules: readonly FiredRule[], returnTo: HTMLElement) => void;
}

const titleOf = (hit: Hit, field: string | undefined) => (field && hit.fields[field]?.[0]) || hit.itemGuid || "(no title)";

const markerColor = (kind: string) =>
  kind === "up" ? "var(--color-success-text)" : kind === "down" ? "var(--color-alert-text)" : "var(--color-text-low-emphasis)";

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

  const column = (key: "baseline" | "tuned", name: string, search: ReturnType<typeof useColumnSearch>) => {
    const settings = sides[key];
    const update = (next: Partial<SideSettings>) => setSides((current) => ({ ...current, [key]: { ...current[key], ...next } }));
    const result = search.result;
    const hits = result && !result.error ? result.hits : [];
    const changes = key === "tuned" && baseline.result && !baseline.result.error ? rankChanges(baseline.result.hits, hits) : [];
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
                    <Inline spacingX={Spacing.S} spacingY={Spacing.S}>
                      <span style={{ color: "var(--color-text-default-on-light)" }}>Fired rules:</span>
                      {firedRuleRows(result.firedRules, window.location.pathname).map((rule) =>
                        rule.href ? (
                          <LinkButton key={rule.ruleId} label={rule.label} href={rule.href} color={ButtonColor.Tertiary} />
                        ) : (
                          <span key={rule.ruleId}>{`${rule.label} (deleted)`}</span>
                        ),
                      )}
                    </Inline>
                  )}
                  {settings.applySynonyms && result.appliedSynonyms.length > 0 && (
                    <Inline spacingX={Spacing.S} spacingY={Spacing.S}>
                      <span style={{ color: "var(--color-text-default-on-light)" }}>Synonyms applied:</span>
                      {appliedSynonymRows(result.appliedSynonyms, window.location.pathname).map((row) =>
                        row.href ? (
                          <LinkButton key={row.key} label={row.label} href={row.href} color={ButtonColor.Tertiary} />
                        ) : (
                          <span key={row.key}>{`${row.label} (deleted)`}</span>
                        ),
                      )}
                    </Inline>
                  )}
                  {hits.length === 0 ? (
                    <span style={{ color: "var(--color-text-low-emphasis)" }}>No documents match.</span>
                  ) : (
                    <ol
                      aria-label={`${name} results`}
                      style={{ margin: 0, paddingLeft: Spacing.XL, color: "var(--color-text-default-on-light)" }}
                    >
                      {hits.map((hit, index) => {
                        const marker = changes[index] ? markerLabel(changes[index]) : null;
                        return (
                          <li key={`${hit.itemGuid ?? "none"}-${index}`} style={{ paddingBottom: Spacing.XS }}>
                            <button
                              type="button"
                              onClick={(event) => onOpenHit(hit, result.firedRules, event.currentTarget)}
                              style={{
                                background: "none",
                                border: "none",
                                padding: 0,
                                font: "inherit",
                                color: "var(--color-text-high-emphasis)",
                                cursor: "pointer",
                                textAlign: "left",
                              }}
                            >
                              {titleOf(hit, titleField)}
                            </button>
                            {marker && (
                              <span
                                aria-label={marker.aria}
                                title={marker.aria}
                                style={{ marginLeft: Spacing.S, color: markerColor(changes[index].kind) }}
                              >
                                {marker.text}
                              </span>
                            )}
                          </li>
                        );
                      })}
                    </ol>
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
    <Stack spacing={Spacing.S}>
      <span style={{ color: "var(--color-text-low-emphasis)" }}>
        {`Both columns show the first ${ComparePageSize} results. Contexts are sent as the site would send them; contact-group membership is not checked.`}
      </span>
      <Row spacingX={Spacing.L} spacingY={Spacing.L}>
        <Column cols={Cols.Col12} colsMd={Cols.Col6} colsLg={Cols.Col6}>
          {column("baseline", "Baseline", baseline)}
        </Column>
        <Column cols={Cols.Col12} colsMd={Cols.Col6} colsLg={Cols.Col6}>
          {column("tuned", "Tuned", tuned)}
        </Column>
      </Row>
    </Stack>
  );
};
