import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  BorderRadius,
  Button,
  ButtonColor,
  ButtonSize,
  Callout,
  CalloutPlacementType,
  CalloutType,
  Checkbox,
  Headline,
  HeadlineSize,
  Icon,
  Inline,
  Input,
  NotificationBarWarning,
  Paper,
  PaperElevation,
  SidePanel,
  SidePanelSize,
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
import {
  firedRuleRows,
  humanizeFieldName,
  latestRequest,
  mergeSelectedIntoFacets,
  pinningRuleName,
  position,
  scoreRatio,
  toggleSelection,
  visibleValues,
  type FacetSelection,
  type FiredRule,
} from "./state";
import type { Hit, SearchResult } from "./compare";
import { CompareView } from "./CompareView";
import { RuleChips, SynonymTags, TitleCell, cell, column, componentCell } from "./ResultParts";
import { clear, peek } from "../reports/handoff";
import { DataBar, RangeFooter, Stale } from "../reports/patterns";
import { openerOrFallback, returnFocus } from "./focus";
import { pageStyle } from "../layout";

interface QueryTesterProps {
  readonly isFaceted: boolean;
  readonly pageSize: number;
  readonly maxResultWindow: number;
  readonly keyFields: string[];
  readonly maxContexts: number;
  readonly maxContextLength: number;
}

interface OpenDetail {
  readonly hit: Hit;
  readonly firedRules: readonly FiredRule[];
  /** Where focus goes when the panel closes; null returns it to the table. */
  readonly returnTo: HTMLElement | null;
}

interface SearchArgs {
  readonly query: string;
  readonly page: number;
  readonly facets: FacetSelection;
  readonly size: number;
}

const collapsedFacetValues = 5;
const PAGE_SIZES = [10, 25, 50];

const GuidText = ({ guid }: { guid: string | null }) => (
  <span
    style={{
      display: "block",
      overflow: "hidden",
      textOverflow: "ellipsis",
      whiteSpace: "nowrap",
      fontFamily: "monospace",
      fontSize: "12px",
      color: "var(--color-text-low-emphasis)",
    }}
  >
    {guid ?? "(none)"}
  </span>
);

const fieldText = (hit: Hit, field: string) => (hit.fields[field] ?? []).join(", ");

// Module-level so the callback ref is stable: it focuses a panel's content once, when it mounts.
const focusOnMount = (element: HTMLElement | null) => element?.focus();

export const QueryTesterTemplate = ({
  isFaceted,
  pageSize: defaultPageSize,
  maxResultWindow,
  keyFields,
  maxContexts,
  maxContextLength,
}: QueryTesterProps) => {
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  // A query handed over by a Search reports row action is searched on arrival (untracked like every tester search).
  const [handedQuery] = useState(() => peek("queryTester", window.location.pathname));
  const [query, setQuery] = useState(handedQuery ?? "");
  const [submittedQuery, setSubmittedQuery] = useState(handedQuery ?? "");
  // Page and size describe the rows on screen: both are committed only with a successful result.
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const lastSearch = useRef<SearchArgs>(undefined);
  const [selection, setSelection] = useState<FacetSelection>({});
  const [result, setResult] = useState<SearchResult>();
  const [failed, setFailed] = useState(false);
  const [failures, setFailures] = useState(0);
  const [inProgress, setInProgress] = useState(false);
  const [expandedFacets, setExpandedFacets] = useState<Record<string, boolean>>({});
  const [detail, setDetail] = useState<OpenDetail>();
  const [compare, setCompare] = useState(false);
  const [draft, setDraft] = useState<FacetSelection>();
  const [applyRules, setApplyRules] = useState(true);
  const applyRulesRef = useRef(true);
  const [applySynonyms, setApplySynonyms] = useState(true);
  const applySynonymsRef = useRef(true);
  const tableArea = useRef<HTMLDivElement>(null);
  const filterButton = useRef<HTMLButtonElement>(null);
  // The Filter button or the chip that opened the panel.
  const filterOpener = useRef<HTMLElement | null>(null);

  const search = useCallback(
    async (searchQuery: string, searchPage: number, facets: FacetSelection, size: number) => {
      lastSearch.current = { query: searchQuery, page: searchPage, facets, size };
      setInProgress(true);
      const { latest, value } = await request((abort) =>
        executeCommand<SearchResult, { query: string; page: number; pageSize: number; facets: FacetSelection; applyRules: boolean; applySynonyms: boolean }>(
          "Search",
          { query: searchQuery, page: searchPage, pageSize: size, facets, applyRules: applyRulesRef.current, applySynonyms: applySynonymsRef.current },
          undefined,
          abort,
        ),
      );
      if (!latest) {
        return;
      }
      setInProgress(false);
      setFailed(value === undefined);
      if (value === undefined) {
        setFailures((count) => count + 1);
      }
      if (value !== undefined) {
        setResult(value);
        setPage(searchPage);
        setPageSize(size);
        setDetail(undefined);
      }
    },
    [executeCommand, request],
  );

  useEffect(() => {
    clear("queryTester", window.location.pathname);
    void search(handedQuery ?? "", 1, {}, defaultPageSize);
  }, [search, handedQuery, defaultPageSize]);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setSubmittedQuery(query);
    void search(query, 1, selection, pageSize);
  };

  const applySelection = (next: FacetSelection) => {
    setSelection(next);
    void search(submittedQuery, 1, next, pageSize);
  };

  const toggleRules = (value: boolean) => {
    applyRulesRef.current = value;
    setApplyRules(value);
    void search(submittedQuery, page, selection, pageSize);
  };

  const toggleSynonyms = (value: boolean) => {
    applySynonymsRef.current = value;
    setApplySynonyms(value);
    void search(submittedQuery, page, selection, pageSize);
  };

  // Re-runs the last request as sent (query, page, size, filters) with the current switches.
  const retry = () => {
    const last = lastSearch.current;
    if (last) {
      void search(last.query, last.page, last.facets, last.size);
    }
  };

  const closeDetail = () => {
    const returnTo = detail?.returnTo;
    setDetail(undefined);
    // The npm Table's row button is display: contents and cannot take focus, so table rows return focus to the table itself.
    returnFocus(() => returnTo ?? tableArea.current);
  };

  const closeFilters = (commit: boolean) => {
    if (commit && draft) {
      applySelection(draft);
    }
    setDraft(undefined);
    returnFocus(() => openerOrFallback(filterOpener.current, filterButton.current));
  };

  const openFilters = () => {
    filterOpener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setDraft(selection);
  };

  const hits = result?.hits ?? [];
  const firstField = keyFields[0];
  const firedRules = result?.firedRules ?? [];
  const { columns, rows } = useMemo(() => {
    const pageHits = result?.hits ?? [];
    const rules = result?.firedRules ?? [];
    // A key field no hit on this page has a value for (e.g. Body without a summary field) gets no empty column.
    const otherFields = keyFields.slice(1).filter((f) => pageHits.length === 0 || pageHits.some((hit) => fieldText(hit, f) !== ""));
    const top = Math.max(0, ...pageHits.map((hit) => hit.score));
    return {
      columns: [
        column("position", "#", 5, 5),
        ...(firstField ? [column("title", "Title")] : []),
        ...otherFields.map((f) => column(f, humanizeFieldName(f), 16)),
        column("score", "Score", 22, 20),
        column("itemGuid", "Item GUID", 18),
      ],
      rows: pageHits.map((hit, i): TableRow => {
        const at = position(page, pageSize, i);
        const pinnedBy = pinningRuleName(hit, rules);
        return {
          identifier: i,
          disabled: false,
          cells: [
            cell(String(at), "position"),
            ...(firstField
              ? [componentCell("title", () => <TitleCell title={fieldText(hit, firstField)} pinnedAt={pinnedBy === null ? null : at} pinnedBy={pinnedBy} />)]
              : []),
            ...otherFields.map((f) => cell(fieldText(hit, f), f)),
            componentCell("score", () => <DataBar value={hit.score.toFixed(4)} ratio={scoreRatio(hit.score, top)} tone="info" />),
            componentCell("itemGuid", () => <GuidText guid={hit.itemGuid} />),
          ],
        };
      }),
    };
  }, [result, page, pageSize, keyFields, firstField]);
  const sizes = [...new Set([defaultPageSize, ...PAGE_SIZES])].sort((a, b) => a - b);
  const detailHit = detail?.hit;
  const facets = isFaceted ? mergeSelectedIntoFacets(result?.facets ?? [], selection) : [];
  const applied = Object.entries(selection).flatMap(([dimension, labels]) => labels.map((label) => ({ dimension, label })));
  const noHits = result !== undefined && !result.error && result.totalHits === 0 && !inProgress && !failed;

  return (
    <div style={pageStyle}>
      <Stack spacing={Spacing.XL}>
        <div role="heading" aria-level={1}>
          <Headline size={HeadlineSize.M}>Query tester</Headline>
        </div>
        {failed && (
          <Callout
            type={CalloutType.FriendlyWarning}
            placement={CalloutPlacementType.OnDesk}
            headline="Search failed"
            actionButton={<Button label="Try again" color={ButtonColor.Secondary} disabled={inProgress} onClick={retry} />}
          >
            The previous results are still shown. Try again.
          </Callout>
        )}
        {result?.error && (
          <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Search rejected">
            {result.error}
          </Callout>
        )}
        {!compare && result?.rulesFailed && (
          <NotificationBarWarning>Rules could not be applied — these results are unruled.</NotificationBarWarning>
        )}
        {!compare && result?.synonymsFailed && (
          <NotificationBarWarning>Synonyms could not be applied — the original text was searched.</NotificationBarWarning>
        )}
        <div style={{ display: "flex", alignItems: "flex-start", gap: Spacing.M }}>
          <form onSubmit={submit} role="search" style={{ flex: 1, minWidth: 0 }}>
            <Input
              aria-label="Query"
              placeholder="Search — leave empty to list all documents"
              actionElement={<Icon name="xp-magnifier" />}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </form>
          {isFaceted && (
            <Button
              label="Filter"
              icon="xp-filter-1"
              color={ButtonColor.Secondary}
              badge={applied.length > 0}
              buttonRef={filterButton}
              onClick={openFilters}
            />
          )}
        </div>
        {applied.length > 0 && (
          <Inline spacingX={Spacing.S} spacingY={Spacing.S}>
            <div style={{ display: "flex", alignItems: "center", height: "100%", color: "var(--color-text-low-emphasis)", whiteSpace: "nowrap" }}>
              Applied filters:
            </div>
            <Button
              label="Clear all"
              icon="xp-times-circle"
              size={ButtonSize.S}
              color={ButtonColor.Tertiary}
              disabled={inProgress}
              onClick={() => applySelection({})}
            />
            {applied.map(({ dimension, label }) => (
              <Tag
                key={`${dimension}\u0000${label}`}
                label={`${humanizeFieldName(dimension)}: ${label}`}
                disabled={inProgress}
                onClick={openFilters}
                onRemoveClick={() => applySelection(toggleSelection(selection, dimension, label, false))}
              />
            ))}
          </Inline>
        )}
        <Inline spacingX={Spacing.L} spacingY={Spacing.S}>
          <Switch label="Compare" size={SwitchSize.M} value={compare} onChange={setCompare} />
          {!compare && <Switch label="Apply rules" size={SwitchSize.M} value={applyRules} onChange={toggleRules} />}
          {!compare && <Switch label="Apply synonyms" size={SwitchSize.M} value={applySynonyms} onChange={toggleSynonyms} />}
          {!compare && inProgress && hits.length > 0 && <Spinner />}
        </Inline>
        {compare ? (
          <CompareView
            query={submittedQuery}
            selection={selection}
            titleField={firstField}
            maxContexts={maxContexts}
            maxContextLength={maxContextLength}
            onOpenHit={(hit, rules, returnTo) => setDetail({ hit, firedRules: rules, returnTo })}
          />
        ) : (
          <Stale.Provider value={failed}>
            {/* Old rows stay visible while a search runs, so the region says it is updating. */}
            <div aria-busy={inProgress}>
              <Stack spacing={Spacing.XL}>
                {result && !result.error && (
                  <Paper elevation={PaperElevation.S} borderRadius={BorderRadius.RadiusM}>
                    <div style={{ display: "flex", flexDirection: "column", gap: Spacing.L, padding: `${Spacing.L} ${Spacing.XL}` }}>
                      <div style={{ display: "flex", flexWrap: "wrap", gap: Spacing.XXXL }}>
                        <TextWithLabel label="Total hits" value={result.totalHits.toLocaleString()} />
                        <TextWithLabel label="Elapsed" value={`${result.elapsedMilliseconds.toFixed(1)} ms`} />
                      </div>
                      {applyRules && !result.rulesFailed && (
                        <section aria-labelledby="fired-rules-heading">
                          <Stack spacing={Spacing.S}>
                            <div role="heading" aria-level={2} id="fired-rules-heading">
                              <Headline size={HeadlineSize.S}>{`Fired rules (${firedRules.length})`}</Headline>
                            </div>
                            {firedRules.length === 0 ? (
                              <span style={{ color: "var(--color-text-low-emphasis)" }}>No rules fired for this search.</span>
                            ) : (
                              <>
                                <RuleChips rules={firedRules} />
                                {firedRuleRows(firedRules, window.location.pathname)
                                  .filter((rule) => rule.userData)
                                  .map((rule) => (
                                    <pre
                                      key={rule.ruleId}
                                      aria-label={`Custom data of ${rule.label}`}
                                      style={{
                                        margin: 0,
                                        padding: Spacing.S,
                                        overflowX: "auto",
                                        fontFamily: "monospace",
                                        color: "var(--color-text-high-emphasis)",
                                        background: "var(--color-background-shelf-solid)",
                                        border: `${Spacing.Micro} solid var(--color-border-default)`,
                                      }}
                                    >
                                      {rule.userData}
                                    </pre>
                                  ))}
                              </>
                            )}
                          </Stack>
                        </section>
                      )}
                      {applySynonyms && !result.synonymsFailed && submittedQuery.trim() !== "" && (
                        <section aria-labelledby="applied-synonyms-heading">
                          <Stack spacing={Spacing.S}>
                            <div role="heading" aria-level={2} id="applied-synonyms-heading">
                              <Headline size={HeadlineSize.S}>{`Synonyms applied (${result.appliedSynonyms.length})`}</Headline>
                            </div>
                            {result.appliedSynonyms.length === 0 ? (
                              <span style={{ color: "var(--color-text-low-emphasis)" }}>No synonyms applied to this search.</span>
                            ) : (
                              <SynonymTags synonyms={result.appliedSynonyms} />
                            )}
                          </Stack>
                        </section>
                      )}
                    </div>
                  </Paper>
                )}
                {noHits ? (
                  <Callout
                    type={CalloutType.QuickTip}
                    placement={CalloutPlacementType.OnDesk}
                    headline="No documents match"
                    actionButton={
                      applied.length > 0 ? (
                        <Button label="Clear filters" color={ButtonColor.Secondary} onClick={() => applySelection({})} />
                      ) : undefined
                    }
                  >
                    {applied.length > 0 ? "Try removing a filter or changing the query." : "Try a different query."}
                  </Callout>
                ) : inProgress && hits.length === 0 ? (
                  <Spinner />
                ) : (
                  <div ref={tableArea} tabIndex={-1} style={{ outline: "none", minWidth: 0, overflowX: "auto" }}>
                    <Table
                      columns={columns}
                      rows={rows}
                      onRowClick={(id) => setDetail({ hit: hits[id as number], firedRules, returnTo: null })}
                    />
                  </div>
                )}
                {result && !result.error && result.totalHits > 0 && !noHits && (
                  <RangeFooter
                    page={page}
                    pageSize={pageSize}
                    resetKey={failures}
                    total={result.totalHits}
                    noun={result.totalHits === 1 ? "document" : "documents"}
                    sizes={sizes}
                    sizeLabel="Rows per page"
                    pageLimit={Math.floor(maxResultWindow / pageSize)}
                    onPageSize={(size) => void search(submittedQuery, 1, selection, size)}
                    onPage={(p) => void search(submittedQuery, p, selection, pageSize)}
                  />
                )}
              </Stack>
            </div>
          </Stale.Provider>
        )}
        <SidePanel
          headline="Filters"
          size={SidePanelSize.Stackable}
          isVisible={draft !== undefined}
          tooltips={{ close: "Close" }}
          onClose={() => closeFilters(false)}
          footer={
            <div style={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
              <Button label="Clear all" icon="xp-times-circle" destructive color={ButtonColor.Secondary} onClick={() => setDraft({})} />
              <Inline spacingX={Spacing.S}>
                <Button label="Cancel" color={ButtonColor.Secondary} onClick={() => closeFilters(false)} />
                <Button label="Apply" color={ButtonColor.Primary} onClick={() => closeFilters(true)} />
              </Inline>
            </div>
          }
        >
          {draft && (
            <div ref={focusOnMount} tabIndex={-1} style={{ outline: "none" }}>
              <Stack spacing={Spacing.XL}>
                {facets.map((facet) => {
                  const selected = draft[facet.dimension] ?? [];
                  const expanded = expandedFacets[facet.dimension] ?? false;
                  const shown = visibleValues(facet.values, selected, expanded, collapsedFacetValues);
                  const heading = humanizeFieldName(facet.dimension);
                  return (
                    <div key={facet.dimension} role="group" aria-label={heading}>
                      <Stack spacing={Spacing.S}>
                        <div role="heading" aria-level={2}>
                          <Headline size={HeadlineSize.S}>{heading}</Headline>
                        </div>
                        {shown.map((value) => (
                          <Checkbox
                            key={value.label}
                            label={`${value.label} (${value.count.toLocaleString()})`}
                            checked={selected.includes(value.label)}
                            onChange={(_, checked) => setDraft(toggleSelection(draft, facet.dimension, value.label, checked))}
                          />
                        ))}
                        {facet.values.length > collapsedFacetValues && (
                          <div>
                            <Button
                              label={expanded ? "Show less" : "Show more"}
                              size={ButtonSize.S}
                              color={ButtonColor.Tertiary}
                              aria-expanded={expanded}
                              onClick={() => setExpandedFacets({ ...expandedFacets, [facet.dimension]: !expanded })}
                            />
                          </div>
                        )}
                      </Stack>
                    </div>
                  );
                })}
              </Stack>
            </div>
          )}
        </SidePanel>
        <SidePanel
          headline={(detailHit && firstField && fieldText(detailHit, firstField)) || detailHit?.itemGuid || "Document"}
          size={SidePanelSize.Stackable}
          isVisible={detailHit !== undefined}
          tooltips={{ close: "Close" }}
          onClose={closeDetail}
        >
          {detailHit && (
            <div ref={focusOnMount} tabIndex={-1} style={{ outline: "none" }}>
              <Stack spacing={Spacing.M}>
                <TextWithLabel label="Item GUID" value={detailHit.itemGuid ?? "(none)"} />
                {pinningRuleName(detailHit, detail.firedRules) !== null && (
                  <TextWithLabel label="Pinned by" value={pinningRuleName(detailHit, detail.firedRules) ?? ""} />
                )}
                <TextWithLabel label="Score" value={detailHit.score.toFixed(4)} />
                {Object.keys(detailHit.fields).map((field) => (
                  <TextWithLabel key={field} label={field} value={fieldText(detailHit, field)} />
                ))}
              </Stack>
            </div>
          )}
        </SidePanel>
      </Stack>
    </div>
  );
};
