import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  ButtonSize,
  Callout,
  CalloutPlacementType,
  CalloutType,
  CellType,
  Checkbox,
  Cols,
  Column,
  ColumnContentType,
  Headline,
  HeadlineSize,
  Icon,
  Inline,
  Input,
  LinkButton,
  NotificationBarWarning,
  Pagination,
  Row,
  SidePanel,
  SidePanelSize,
  Spacing,
  Stack,
  Switch,
  SwitchSize,
  Table,
  Tag,
  TextWithLabel,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";
import {
  appliedSynonymRows,
  firedRuleRows,
  humanizeFieldName,
  latestRequest,
  mergeSelectedIntoFacets,
  pinnedLabel,
  pinningRuleName,
  toggleSelection,
  visibleValues,
  type FacetSelection,
  type FiredRule,
} from "./state";
import type { Hit, SearchResult } from "./compare";
import { CompareView } from "./CompareView";

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

const collapsedFacetValues = 5;

const column = (name: string, caption: string): TableColumn => ({
  name,
  caption,
  visible: true,
  minWidth: 12,
  maxWidth: 40,
  contentType: ColumnContentType.Text,
  sortable: false,
  searchable: false,
});

// Stored fields can hold content HTML: they are only ever passed as plain strings (StringCell, TextWithLabel without valueAsHtml).
const cell = (value: string, columnName: string): StringCell => ({ type: CellType.String, value, columnName });

const fieldText = (hit: Hit, field: string) => (hit.fields[field] ?? []).join(", ");

// Deferred so it runs after the SidePanel has finished moving focus itself.
const focusLater = (target: () => HTMLElement | null | undefined) => requestAnimationFrame(() => target()?.focus());

// Module-level so the callback ref is stable: it focuses a panel's content once, when it mounts.
const focusOnMount = (element: HTMLElement | null) => element?.focus();

export const QueryTesterTemplate = ({
  isFaceted,
  pageSize,
  maxResultWindow,
  keyFields,
  maxContexts,
  maxContextLength,
}: QueryTesterProps) => {
  const { executeCommand } = usePageCommandProvider();
  const [request] = useState(latestRequest);
  const [query, setQuery] = useState("");
  const [submittedQuery, setSubmittedQuery] = useState("");
  const [page, setPage] = useState(1);
  const [selection, setSelection] = useState<FacetSelection>({});
  const [result, setResult] = useState<SearchResult>();
  const [failed, setFailed] = useState(false);
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

  const search = useCallback(
    async (searchQuery: string, searchPage: number, facets: FacetSelection) => {
      setInProgress(true);
      const { latest, value } = await request((abort) =>
        executeCommand<SearchResult, { query: string; page: number; facets: FacetSelection; applyRules: boolean; applySynonyms: boolean }>(
          "Search",
          { query: searchQuery, page: searchPage, facets, applyRules: applyRulesRef.current, applySynonyms: applySynonymsRef.current },
          undefined,
          abort,
        ),
      );
      if (!latest) {
        return;
      }
      setInProgress(false);
      setFailed(value === undefined);
      if (value !== undefined) {
        setResult(value);
        setPage(searchPage);
        setDetail(undefined);
      }
    },
    [executeCommand, request],
  );

  useEffect(() => {
    void search("", 1, {});
  }, [search]);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setSubmittedQuery(query);
    void search(query, 1, selection);
  };

  const applySelection = (next: FacetSelection) => {
    setSelection(next);
    void search(submittedQuery, 1, next);
  };

  const toggleRules = (value: boolean) => {
    applyRulesRef.current = value;
    setApplyRules(value);
    void search(submittedQuery, page, selection);
  };

  const toggleSynonyms = (value: boolean) => {
    applySynonymsRef.current = value;
    setApplySynonyms(value);
    void search(submittedQuery, page, selection);
  };

  const closeDetail = () => {
    const returnTo = detail?.returnTo;
    setDetail(undefined);
    // The npm Table's row button is display: contents and cannot take focus, so table rows return focus to the table itself.
    focusLater(() => returnTo ?? tableArea.current);
  };

  const closeFilters = (commit: boolean) => {
    if (commit && draft) {
      applySelection(draft);
    }
    setDraft(undefined);
    focusLater(() => filterButton.current);
  };

  const hits = result?.hits ?? [];
  const firstField = keyFields[0];
  const firedRules = result?.firedRules ?? [];
  const showPinned = hits.some((hit) => hit.pinnedByRuleId != null);
  const columns = [
    ...(showPinned ? [column("pinned", "Pinned")] : []),
    ...keyFields.map((f) => column(f, humanizeFieldName(f))),
    column("score", "Score"),
    column("itemGuid", "Item GUID"),
  ];
  const rows: TableRow[] = hits.map((hit, i) => ({
    identifier: i,
    disabled: false,
    cells: [
      ...(showPinned ? [cell(pinnedLabel(hit, firedRules), "pinned")] : []),
      ...keyFields.map((f) => cell(fieldText(hit, f), f)),
      cell(hit.score.toFixed(4), "score"),
      cell(hit.itemGuid ?? "(none)", "itemGuid"),
    ],
  }));
  const totalPages = Math.min(Math.ceil((result?.totalHits ?? 0) / pageSize), Math.floor(maxResultWindow / pageSize));
  const detailHit = detail?.hit;
  const facets = isFaceted ? mergeSelectedIntoFacets(result?.facets ?? [], selection) : [];
  const applied = Object.entries(selection).flatMap(([dimension, labels]) => labels.map((label) => ({ dimension, label })));
  const noHits = result !== undefined && !result.error && result.totalHits === 0 && !inProgress;

  return (
    <Stack spacing={Spacing.XL}>
      <div role="heading" aria-level={1}>
        <Headline size={HeadlineSize.M}>Query tester</Headline>
      </div>
      {failed && (
        <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Search failed">
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
      <Row spacingX={Spacing.XL}>
        <Column cols={Cols.Col12} colsMd={Cols.Col9} colsLg={Cols.Col9}>
          <form onSubmit={submit} role="search">
            <Input
              label="Query"
              explanationText="Leave empty to list all documents. Press Enter to search."
              actionElement={<Icon name="xp-magnifier" />}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </form>
        </Column>
        <Column cols={Cols.Col12} colsMd={Cols.Col3} colsLg={Cols.Col3}>
          <Stack spacing={Spacing.S}>
            <Switch label="Compare" size={SwitchSize.M} value={compare} onChange={setCompare} />
            {!compare && <Switch label="Apply rules" size={SwitchSize.M} value={applyRules} onChange={toggleRules} />}
            {!compare && <Switch label="Apply synonyms" size={SwitchSize.M} value={applySynonyms} onChange={toggleSynonyms} />}
          </Stack>
        </Column>
      </Row>
      {isFaceted && (
        <Inline spacingX={Spacing.S} spacingY={Spacing.S}>
          <Button
            label="Filter"
            icon="xp-filter-1"
            size={ButtonSize.S}
            color={ButtonColor.Secondary}
            badge={applied.length > 0}
            buttonRef={filterButton}
            onClick={() => setDraft(selection)}
          />
          {applied.length > 0 && [
            <div
              key="label"
              style={{
                display: "flex",
                alignItems: "center",
                height: "100%",
                marginLeft: Spacing.M,
                color: "var(--color-text-low-emphasis)",
                whiteSpace: "nowrap",
              }}
            >
              Applied filters:
            </div>,
            <Button
              key="clear"
              label="Clear all"
              icon="xp-times-circle"
              size={ButtonSize.S}
              color={ButtonColor.Tertiary}
              disabled={inProgress}
              onClick={() => applySelection({})}
            />,
            ...applied.map(({ dimension, label }) => (
              <Tag
                key={`${dimension}\u0000${label}`}
                label={`${humanizeFieldName(dimension)}: ${label}`}
                disabled={inProgress}
                onClick={() => setDraft(selection)}
                onRemoveClick={() => applySelection(toggleSelection(selection, dimension, label, false))}
              />
            )),
          ]}
        </Inline>
      )}
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
        <>
          {result && !result.error && (
            // Row sets its own inline margin-top, which would cancel the Stack spacing; the wrapper takes the spacing instead.
            <div>
              <Row spacingX={Spacing.XL}>
                <Column>
                  <TextWithLabel label="Total hits" value={result.totalHits.toLocaleString()} />
                </Column>
                <Column>
                  <TextWithLabel label="Elapsed" value={`${result.elapsedMilliseconds.toFixed(1)} ms`} />
                </Column>
              </Row>
            </div>
          )}
          {result && !result.error && applyRules && !result.rulesFailed && (
            <section aria-labelledby="fired-rules-heading">
              <Stack spacing={Spacing.S}>
                <div role="heading" aria-level={2} id="fired-rules-heading">
                  <Headline size={HeadlineSize.S}>{`Fired rules (${firedRules.length})`}</Headline>
                </div>
                {firedRules.length === 0 ? (
                  <span style={{ color: "var(--color-text-low-emphasis)" }}>No rules fired for this search.</span>
                ) : (
                  firedRuleRows(firedRules, window.location.pathname).map((rule) => (
                    <Stack key={rule.ruleId} spacing={Spacing.XS}>
                      {rule.href ? (
                        <div>
                          <LinkButton label={rule.label} href={rule.href} color={ButtonColor.Tertiary} />
                        </div>
                      ) : (
                        <span>{`${rule.label} (deleted)`}</span>
                      )}
                      {rule.userData && (
                        <pre
                          aria-label={`Custom data of ${rule.label}`}
                          style={{
                            margin: 0,
                            padding: Spacing.S,
                            overflowX: "auto",
                            fontFamily: "monospace",
                            color: "var(--color-text-high-emphasis)",
                            background: "var(--color-background-shelf-solid)",
                            border: `${Spacing.Micro}px solid var(--color-border-default)`,
                          }}
                        >
                          {rule.userData}
                        </pre>
                      )}
                    </Stack>
                  ))
                )}
              </Stack>
            </section>
          )}
          {result && !result.error && applySynonyms && !result.synonymsFailed && submittedQuery.trim() !== "" && (
            <section aria-labelledby="applied-synonyms-heading">
              <Stack spacing={Spacing.S}>
                <div role="heading" aria-level={2} id="applied-synonyms-heading">
                  <Headline size={HeadlineSize.S}>{`Synonyms applied (${result.appliedSynonyms.length})`}</Headline>
                </div>
                {result.appliedSynonyms.length === 0 ? (
                  <span style={{ color: "var(--color-text-low-emphasis)" }}>No synonyms applied to this search.</span>
                ) : (
                  <Inline spacingX={Spacing.S} spacingY={Spacing.S}>
                    {appliedSynonymRows(result.appliedSynonyms, window.location.pathname).map((row) =>
                      row.href ? (
                        <LinkButton key={row.key} label={row.label} href={row.href} color={ButtonColor.Tertiary} />
                      ) : (
                        <span key={row.key}>{`${row.label} (deleted)`}</span>
                      ),
                    )}
                  </Inline>
                )}
              </Stack>
            </section>
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
          ) : (
            <div ref={tableArea} tabIndex={-1} style={{ outline: "none" }}>
              <Table
                columns={columns}
                rows={inProgress || (result === undefined && !failed) ? undefined : rows}
                onRowClick={(id) => setDetail({ hit: hits[id as number], firedRules, returnTo: null })}
              />
            </div>
          )}
          {totalPages > 1 && !noHits && (
            <Pagination selectedPage={page} totalPages={totalPages} onPageChange={(p) => void search(submittedQuery, p, selection)} />
          )}
        </>
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
  );
};
