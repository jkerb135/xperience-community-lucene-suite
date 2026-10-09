import { useCallback, useEffect, useState, type FormEvent } from "react";
import { usePageCommandProvider } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  ButtonType,
  Callout,
  CalloutPlacementType,
  CalloutType,
  Card,
  CellType,
  Checkbox,
  Cols,
  Column,
  ColumnContentType,
  Headline,
  HeadlineSize,
  Input,
  Pagination,
  Row,
  Spacing,
  Stack,
  Table,
  TextWithLabel,
  type StringCell,
  type TableColumn,
  type TableRow,
} from "@kentico/xperience-admin-components";

interface QueryTesterProps {
  readonly indexName: string;
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

interface Facet {
  readonly dimension: string;
  readonly values: { readonly label: string; readonly count: number }[];
}

interface SearchResult {
  readonly hits: Hit[];
  readonly totalHits: number;
  readonly facets: Facet[];
  readonly elapsedMilliseconds: number;
  readonly error: string | null;
}

type FacetSelection = Record<string, string[]>;

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

export const QueryTesterTemplate = ({ indexName, isFaceted, pageSize, maxResultWindow, keyFields }: QueryTesterProps) => {
  const { executeCommand } = usePageCommandProvider();
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [selection, setSelection] = useState<FacetSelection>({});
  const [result, setResult] = useState<SearchResult>();
  const [expanded, setExpanded] = useState<number>();
  const [inProgress, setInProgress] = useState(false);

  const search = useCallback(
    async (searchQuery: string, searchPage: number, facets: FacetSelection) => {
      setInProgress(true);
      try {
        const response = await executeCommand<SearchResult, { query: string; page: number; facets: FacetSelection }>("Search", {
          query: searchQuery,
          page: searchPage,
          facets,
        });
        setResult(response);
        setPage(searchPage);
        setExpanded(undefined);
      } finally {
        setInProgress(false);
      }
    },
    [executeCommand],
  );

  useEffect(() => {
    void search("", 1, {});
  }, [search]);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void search(query, 1, selection);
  };

  const toggleFacet = (dimension: string, label: string, checked: boolean) => {
    const values = (selection[dimension] ?? []).filter((v) => v !== label);
    const next = { ...selection, [dimension]: checked ? [...values, label] : values };
    if (next[dimension].length === 0) {
      delete next[dimension];
    }
    setSelection(next);
    void search(query, 1, next);
  };

  const columns = [column("itemGuid", "ItemGuid"), column("score", "Score"), ...keyFields.map((f) => column(f, f))];
  const hits = result?.hits ?? [];
  const rows: TableRow[] = hits.map((hit, i) => ({
    identifier: i,
    disabled: false,
    cells: [
      cell(hit.itemGuid ?? "(none)", "itemGuid"),
      cell(hit.score.toFixed(4), "score"),
      ...keyFields.map((f) => cell(fieldText(hit, f), f)),
    ],
  }));
  const totalPages = Math.min(Math.ceil((result?.totalHits ?? 0) / pageSize), Math.floor(maxResultWindow / pageSize));
  const expandedHit = expanded === undefined ? undefined : hits[expanded];
  const facets = isFaceted ? (result?.facets ?? []) : [];

  return (
    <Stack spacing={Spacing.XL}>
      <Headline size={HeadlineSize.M}>Query tester: {indexName}</Headline>
      <form onSubmit={submit}>
        <Row spacingX={Spacing.M}>
          <Column cols={Cols.Col10}>
            <Input
              label="Query"
              placeholder="Leave empty to list all documents"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </Column>
          <Column cols={Cols.Col2}>
            <Button type={ButtonType.Submit} color={ButtonColor.Primary} label="Search" inProgress={inProgress} />
          </Column>
        </Row>
      </form>
      {result?.error && (
        <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Search rejected">
          {result.error}
        </Callout>
      )}
      {result && !result.error && (
        <Row spacingX={Spacing.XL}>
          <Column>
            <TextWithLabel label="Total hits" value={String(result.totalHits)} />
          </Column>
          <Column>
            <TextWithLabel label="Elapsed" value={`${result.elapsedMilliseconds.toFixed(1)} ms`} />
          </Column>
        </Row>
      )}
      <Row spacingX={Spacing.XL} spacingY={Spacing.XL}>
        {facets.length > 0 && (
          <Column cols={Cols.Col12} colsMd={Cols.Col3} colsLg={Cols.Col3}>
            <Stack spacing={Spacing.L}>
              {facets.map((facet) => (
                <Stack key={facet.dimension} spacing={Spacing.S}>
                  <Headline size={HeadlineSize.S}>{facet.dimension}</Headline>
                  {facet.values.map((value) => (
                    <Checkbox
                      key={value.label}
                      label={`${value.label} (${value.count})`}
                      aria-label={`${facet.dimension}: ${value.label} (${value.count})`}
                      checked={selection[facet.dimension]?.includes(value.label) ?? false}
                      onChange={(_, checked) => toggleFacet(facet.dimension, value.label, checked)}
                    />
                  ))}
                </Stack>
              ))}
            </Stack>
          </Column>
        )}
        <Column cols={Cols.Col12} colsMd={facets.length > 0 ? Cols.Col9 : Cols.Col12} colsLg={facets.length > 0 ? Cols.Col9 : Cols.Col12}>
          <Stack spacing={Spacing.L}>
            <div style={{ overflowX: "auto" }}>
              <Table columns={columns} rows={rows} onRowClick={(id) => setExpanded(id as number)} />
            </div>
            {totalPages > 1 && (
              <Pagination selectedPage={page} totalPages={totalPages} onPageChange={(p) => void search(query, p, selection)} />
            )}
            {expandedHit && (
              <Card headline={`All stored fields: ${expandedHit.itemGuid ?? "(no ItemGuid)"}`}>
                <Stack spacing={Spacing.M}>
                  {Object.keys(expandedHit.fields).map((field) => (
                    <TextWithLabel key={field} label={field} value={fieldText(expandedHit, field)} />
                  ))}
                </Stack>
              </Card>
            )}
          </Stack>
        </Column>
      </Row>
    </Stack>
  );
};
