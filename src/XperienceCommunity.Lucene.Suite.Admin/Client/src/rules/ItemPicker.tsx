import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { useFormComponentCommandProvider, type FormComponentProps } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  ButtonType,
  Checkbox,
  Headline,
  HeadlineSize,
  Inline,
  Input,
  NotificationBarAlert,
  SidePanel,
  SidePanelSize,
  Spacing,
  Spinner,
  Stack,
} from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state";
import { pickerRow, sameGuid, toggleItem, type RuleItem } from "./state";

interface ItemsResult {
  readonly items: RuleItem[];
  readonly error: string | null;
}

// Module-level so the callback ref is stable: focuses the search field once, when the panel content mounts.
const focusQueryOnMount = (element: HTMLElement | null) => element?.querySelector("input")?.focus();

/**
 * Titles of the given ItemGuids, resolved through the component's Describe command.
 * `null` marks an item no longer in the index; `remember` seeds items the picker already knows.
 */
export const useItemTitles = (props: FormComponentProps<string>, guids: readonly string[]) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const [known, setKnown] = useState<ReadonlyMap<string, RuleItem | null>>(new Map());
  const [error, setError] = useState<string>();
  const request = useMemo(() => latestRequest(), []);
  const propsRef = useRef(props);
  propsRef.current = props;

  const missing = guids.filter((guid) => guid && !known.has(sameGuid(guid)));
  const missingKey = missing.map(sameGuid).sort().join(",");

  useEffect(() => {
    if (!missingKey) {
      return;
    }
    const items = missingKey.split(",");
    void (async () => {
      const { latest, value } = await request((abort) =>
        executeCommand<ItemsResult, { items: string[] }>(propsRef.current, "Describe", { items }, undefined, abort),
      );
      if (!latest) {
        return;
      }
      if (!value || value.error) {
        setError(value?.error ?? "Item titles could not be loaded.");
        return;
      }
      setError(undefined);
      setKnown((current) => {
        const next = new Map(current);
        for (const guid of items) {
          next.set(guid, value.items.find((item) => sameGuid(item.itemGuid) === guid) ?? null);
        }
        return next;
      });
    })();
  }, [missingKey, executeCommand, request]);

  const remember = useCallback(
    (items: readonly RuleItem[]) =>
      setKnown((current) => {
        const next = new Map(current);
        items.forEach((item) => next.set(sameGuid(item.itemGuid), item));
        return next;
      }),
    [],
  );

  return {
    title: (guid: string) => known.get(sameGuid(guid)),
    error,
    remember,
  };
};

interface ItemPickerProps {
  readonly componentProps: FormComponentProps<string>;
  readonly isVisible: boolean;
  readonly headline: string;
  /** Items already in this list (shown checked and disabled). */
  readonly added: ReadonlySet<string>;
  /** Items in the other list (shown disabled with `excludedNote`). */
  readonly excluded: ReadonlySet<string>;
  readonly excludedNote: string;
  readonly onAdd: (items: RuleItem[]) => void;
  readonly onClose: () => void;
}

/** Searches this index and lets the marketer tick items to add. */
export const ItemPicker = ({ componentProps, isVisible, headline, added, excluded, excludedNote, onAdd, onClose }: ItemPickerProps) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const request = useMemo(() => latestRequest(), []);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<RuleItem[]>();
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const [chosen, setChosen] = useState<RuleItem[]>([]);
  const propsRef = useRef(componentProps);
  propsRef.current = componentProps;

  const search = useCallback(
    async (text: string) => {
      setLoading(true);
      const { latest, value } = await request((abort) =>
        executeCommand<ItemsResult, { query: string }>(propsRef.current, "Search", { query: text }, undefined, abort),
      );
      if (!latest) {
        return;
      }
      setLoading(false);
      setError(value ? (value.error ?? undefined) : "Search failed. Try again.");
      setResults(value?.items ?? []);
    },
    [executeCommand, request],
  );

  useEffect(() => {
    if (isVisible) {
      setQuery("");
      setSelected(new Set());
      setChosen([]);
      setResults(undefined);
      setError(undefined);
      void search("");
    }
  }, [isVisible, search]);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void search(query);
  };

  const toggle = (item: RuleItem, checked: boolean) => {
    setSelected(toggleItem(selected, item.itemGuid, checked));
    setChosen(checked ? [...chosen, item] : chosen.filter((c) => sameGuid(c.itemGuid) !== sameGuid(item.itemGuid)));
  };

  const count = chosen.length;

  return (
    <SidePanel
      headline={
        <span role="heading" aria-level={2}>
          {headline}
        </span>
      }
      size={SidePanelSize.Stackable}
      isVisible={isVisible}
      tooltips={{ close: "Close" }}
      onClose={onClose}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", width: "100%" }}>
          <Inline spacingX={Spacing.S}>
            <Button label="Cancel" color={ButtonColor.Secondary} onClick={onClose} />
            <Button label={`Add selected (${count})`} color={ButtonColor.Primary} disabled={count === 0} onClick={() => onAdd(chosen)} />
          </Inline>
        </div>
      }
    >
      {isVisible && (
        <div ref={focusQueryOnMount}>
          <Stack spacing={Spacing.L}>
            <form onSubmit={submit} role="search">
              <Stack spacing={Spacing.S}>
                <Input
                  id={`${componentProps.name}-picker-query`}
                  label="Search this index"
                  explanationText="Press Enter to search. Shows the first 20 results."
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                />
                <div>
                  <Button label="Search" icon="xp-magnifier" color={ButtonColor.Secondary} type={ButtonType.Submit} disabled={loading} />
                </div>
              </Stack>
            </form>
            {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
            {loading ? (
              <Spinner />
            ) : results && results.length === 0 && !error ? (
              <div role="status">
                <Headline size={HeadlineSize.S}>No results</Headline>
                <span style={{ color: "var(--color-text-low-emphasis)" }}>Try a different search.</span>
              </div>
            ) : (
              <div role="group" aria-label="Results">
                <Stack spacing={Spacing.S}>
                  {(results ?? []).map((item) => {
                    const row = pickerRow(item.itemGuid, selected, added, excluded, excludedNote);
                    const details = [item.contentType, row.note].filter(Boolean).join(" · ");
                    return (
                      <Checkbox
                        key={item.itemGuid}
                        label={details ? `${item.title} (${details})` : item.title}
                        checked={row.checked}
                        disabled={row.disabled}
                        onChange={(_, checked) => toggle(item, checked)}
                      />
                    );
                  })}
                </Stack>
              </div>
            )}
          </Stack>
        </div>
      )}
    </SidePanel>
  );
};
