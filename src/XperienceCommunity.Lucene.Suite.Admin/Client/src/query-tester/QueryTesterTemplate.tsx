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
  Pagination,
  Row,
  SidePanel,
  SidePanelSize,
  Spacing,
  Stack,
  Table,
  Tag,
  TextWithLabel,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";
import {
  humanizeFieldName,
  latestRequest,
  mergeSelectedIntoFacets,
  toggleSelection,
  visibleValues,
  type Facet,
  type FacetSelection,
} from "./state";

interface QueryTesterProps {
  readonly isFaceted: boolean;
  readonly pageSize: number;
  readonly maxResultWindow: number;
  readonly keyFields: string[];
}

interface Hit {
  readonly itemGuid: string | null;
  readonly score: number;
  readonly fields: Record<string, string[]>;
}

interface SearchResult {
  readonly hits: Hit[];
  readonly totalHits: number;
  readonly facets: Facet[];
  readonly elapsedMilliseconds: number;
  readonly error: string | null;
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

export const QueryTesterTemplate = ({ isFaceted, pageSize, maxResultWindow, keyFields }: QueryTesterProps) => {
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
  const [detail, setDetail] = useState<number>();
  const detailTrigger = useRef<HTMLElement | null>(null);

  const search = useCallback(
    async (searchQuery: string, searchPage: number, facets: FacetSelection) => {
      setInProgress(true);
      const { latest, value } = await request((abort) =>
        executeCommand<SearchResult, { query: string; page: number; facets: FacetSelection }>(
          "Search",
          { query: searchQuery, page: searchPage, facets },
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

  const openDetail = (index: number) => {
    detailTrigger.current = document.activeElement as HTMLElement | null;
    setDetail(index);
  };

  const closeDetail = () => {
    setDetail(undefined);
    detailTrigger.current?.focus();
  };

  const hits = result?.hits ?? [];
  const firstField = keyFields[0];
  const columns = [...keyFields.map((f) => column(f, f)), column("score", "Score"), column("itemGuid", "Item GUID")];
  const rows: TableRow[] = hits.map((hit, i) => ({
    identifier: i,
    disabled: false,
    cells: [
      ...keyFields.map((f) => cell(fieldText(hit, f), f)),
      cell(hit.score.toFixed(4), "score"),
      cell(hit.itemGuid ?? "(none)", "itemGuid"),
    ],
  }));
  const totalPages = Math.min(Math.ceil((result?.totalHits ?? 0) / pageSize), Math.floor(maxResultWindow / pageSize));
  const detailHit = detail === undefined ? undefined : hits[detail];
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
      <form onSubmit={submit} role="search">
        <Input
          label="Query"
          explanationText="Leave empty to list all documents. Press Enter to search."
          actionElement={<Icon name="xp-magnifier" />}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </form>
      {applied.length > 0 && (
        <Inline spacingX={Spacing.S} spacingY={Spacing.S}>
          <span>Applied filters:</span>
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
              onRemoveClick={() => applySelection(toggleSelection(selection, dimension, label, false))}
            />
          ))}
        </Inline>
      )}
      <Row spacingX={Spacing.XL} spacingY={Spacing.XL}>
        {facets.length > 0 && (
          <Column cols={Cols.Col3}>
            <Stack spacing={Spacing.L}>
              {facets.map((facet) => {
                const selected = selection[facet.dimension] ?? [];
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
                          disabled={inProgress}
                          onChange={(_, checked) => applySelection(toggleSelection(selection, facet.dimension, value.label, checked))}
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
          </Column>
        )}
        <Column cols={facets.length > 0 ? Cols.Col9 : Cols.Col12}>
          <Stack spacing={Spacing.L}>
            {result && !result.error && (
              <Row spacingX={Spacing.XL}>
                <Column>
                  <TextWithLabel label="Total hits" value={result.totalHits.toLocaleString()} />
                </Column>
                <Column>
                  <TextWithLabel label="Elapsed" value={`${result.elapsedMilliseconds.toFixed(1)} ms`} />
                </Column>
              </Row>
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
              <Table
                columns={columns}
                rows={inProgress || (result === undefined && !failed) ? undefined : rows}
                onRowClick={(id) => openDetail(id as number)}
              />
            )}
            {totalPages > 1 && !noHits && (
              <Pagination selectedPage={page} totalPages={totalPages} onPageChange={(p) => void search(submittedQuery, p, selection)} />
            )}
          </Stack>
        </Column>
      </Row>
      <SidePanel
        headline={(detailHit && firstField && fieldText(detailHit, firstField)) || detailHit?.itemGuid || "Document"}
        size={SidePanelSize.Stackable}
        isVisible={detailHit !== undefined}
        autofocusCancelButton
        tooltips={{ close: "Close" }}
        onClose={closeDetail}
      >
        {detailHit && (
          <Stack spacing={Spacing.M}>
            <TextWithLabel label="Item GUID" value={detailHit.itemGuid ?? "(none)"} />
            <TextWithLabel label="Score" value={detailHit.score.toFixed(4)} />
            {Object.keys(detailHit.fields).map((field) => (
              <TextWithLabel key={field} label={field} value={fieldText(detailHit, field)} />
            ))}
          </Stack>
        )}
      </SidePanel>
    </Stack>
  );
};
