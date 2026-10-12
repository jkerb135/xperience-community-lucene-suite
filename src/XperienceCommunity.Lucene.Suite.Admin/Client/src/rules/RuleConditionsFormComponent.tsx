import { useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { useFormComponentCommandProvider } from "@kentico/xperience-admin-base";
import {
  BorderRadius,
  Button,
  ButtonColor,
  Callout,
  CalloutPlacementType,
  CalloutType,
  FormItemWrapper,
  Headline,
  HeadlineSize,
  Input,
  MenuItem,
  NotificationBarAlert,
  Select,
  Spacing,
  Stack,
} from "@kentico/xperience-admin-components";
import type { ContactGroupsState } from "../query-tester/ContextPicker";
import type { Facet } from "../query-tester/state";
import { clear, peek } from "../reports/handoff";
import { InlineError, RemoveButton, isEditable, useFocusTargets, useServerErrors, useTouched, type RuleJsonFormComponentProps } from "./shared";
import {
  TYPED,
  anchoringOptions,
  canAdd,
  conditionControlPaths,
  contextChoice,
  contextFromSelect,
  contextOptions,
  contextSelectValue,
  dimensionOptions,
  emptyCondition,
  facetChoice,
  facetFromSelect,
  facetSelectValue,
  facetValueOptions,
  parseConditions,
  partitionErrors,
  removeAt,
  replaceAt,
  serializeConditions,
  type Anchoring,
  type ConditionRow,
} from "./state";

const CONTEXT_HELP = "For example a contact group code name. Letters, digits, '_', '.' and '-'.";
const GROUPS_ERROR = "Contact groups could not be loaded. Type contexts instead.";
const FACETS_ERROR = "Facet values could not be loaded.";

const grid = (columns: string, align: "start" | "end"): CSSProperties => ({ display: "grid", gridTemplateColumns: columns, gap: Spacing.L, alignItems: align });
const ROW = "minmax(0,5fr) minmax(0,5fr) auto";

interface FacetValuesResult {
  readonly facets: Facet[];
  readonly error: string | null;
}

/** Calls a component command once, the first time `needed` is true; undefined while pending, null when it failed. */
const useLazyCommand = <T,>(props: RuleJsonFormComponentProps, name: string, needed: boolean) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const [value, setValue] = useState<T | null>();
  const requested = useRef(false);
  const propsRef = useRef(props);
  propsRef.current = props;

  useEffect(() => {
    if (!needed || requested.current) {
      return;
    }
    requested.current = true;
    void (async () => {
      try {
        setValue((await executeCommand<T>(propsRef.current, name)) ?? null);
      } catch {
        setValue(null);
      }
    })();
  }, [needed, executeCommand, name]);

  return value;
};

export const RuleConditionsFormComponent = (props: RuleJsonFormComponentProps) => {
  const rows = useMemo(() => parseConditions(props.value), [props.value]);
  const json = useMemo(() => serializeConditions(rows), [rows]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json);
  const { byPath, unmatched } = partitionErrors(errors, conditionControlPaths(rows));
  const { ref, focus } = useFocusTargets();
  const { touch, visible, leaveGroup } = useTouched(props);
  const id = (suffix: string) => `${props.name}-${suffix}`;
  const inputRef = (key: string) => ref(key) as React.RefObject<HTMLInputElement>;
  const buttonRef = (key: string) => ref(key) as React.RefObject<HTMLButtonElement>;

  // Picker data only feeds the controls; it never changes the stored value.
  const groupsResult = useLazyCommand<ContactGroupsState>(props, "ContactGroups", rows.some((row) => row.contexts.length > 0));
  const facetsResult = useLazyCommand<FacetValuesResult>(props, "FacetValues", rows.some((row) => row.facets.length > 0));
  const groups = groupsResult?.allowed ? groupsResult.groups : [];
  const pickGroups = groups.length > 0;
  const facets = facetsResult && !facetsResult.error ? facetsResult.facets : undefined;
  const pickFacets = facetsResult === undefined || (facets !== undefined && facets.length > 0);
  const facetsError = facetsResult === null ? FACETS_ERROR : facetsResult?.error;
  // Rows where the user chose "Custom context…" / "Other value…" (keys "x{i}-{j}" / "f{i}-{j}"); UI state only.
  const [typed, setTyped] = useState<ReadonlySet<string>>(new Set());
  const setTypedKey = (key: string, on: boolean) =>
    setTyped((current) => {
      const next = new Set(current);
      if (on) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  // Indexes shift on removal, so the condition's choices are forgotten; stored typed contexts still show as custom.
  const forget = (prefix: string) => setTyped((current) => new Set([...current].filter((key) => !key.startsWith(prefix))));

  const update = (next: readonly ConditionRow[]) => props.onChange?.(serializeConditions(next));
  // A query handed over by a Search reports row action becomes an "Is" condition; only an empty editor on Create rule takes it.
  const [handedQuery] = useState(() => (editable && rows.length === 0 ? peek("ruleCreate", window.location.pathname) : null));
  useEffect(() => {
    clear("ruleCreate", window.location.pathname);
    if (handedQuery !== null) {
      update([{ ...emptyCondition, anchoring: "is", pattern: handedQuery }]);
    }
    // Runs once on mount: the handoff is a one-time prefill.
  }, []);
  const set = (index: number, change: Partial<ConditionRow>) => update(replaceAt(rows, index, { ...rows[index], ...change }));
  const error = (path: string) => {
    const shown = visible(path) && byPath.has(path);
    return { invalid: shown, validationMessage: shown ? byPath.get(path) : undefined, onBlur: () => touch(path) };
  };

  const addCondition = () => {
    update([...rows, emptyCondition]);
    focus(`pattern-${rows.length}`);
  };
  const removeCondition = (index: number) => {
    update(removeAt(rows, index));
    setTyped(new Set());
    focus(index > 0 ? `remove-condition-${index - 1}` : "add-condition");
  };

  const facetRow = (row: ConditionRow, i: number, j: number) => {
    const path = `conditions[${i}]`;
    const facet = row.facets[j];
    const key = `f${i}-${j}`;
    const isTyped = typed.has(key);
    const setFacet = (change: Partial<typeof facet>) => set(i, { facets: replaceAt(row.facets, j, { ...facet, ...change }) });
    const loading = facetsResult === undefined;
    const valuePath = `${path}.facets[${j}].value`;
    const dimensionPath = `${path}.facets[${j}].dimension`;

    const dimension = pickFacets ? (
      <Select
        id={id(`c${i}-f${j}-dimension`)}
        label="Facet"
        value={facet.dimension}
        readOnly={!editable}
        disabled={loading}
        explanationText={loading ? "Loading…" : undefined}
        inputRef={inputRef(`facet-${i}-${j}`)}
        {...error(dimensionPath)}
        onChange={(selected) => {
          touch(dimensionPath);
          if ((selected ?? "") !== facet.dimension) {
            setTypedKey(key, false);
            setFacet({ dimension: selected ?? "", value: "" });
          }
        }}
      >
        {dimensionOptions(facet.dimension, facets).map((option) => (
          <MenuItem key={option.value} primaryLabel={option.primary} secondaryLabel={option.secondary} value={option.value} selected={option.value === facet.dimension} />
        ))}
      </Select>
    ) : (
      <Input
        id={id(`c${i}-f${j}-dimension`)}
        label="Facet"
        value={facet.dimension}
        readOnly={!editable}
        inputRef={inputRef(`facet-${i}-${j}`)}
        {...error(dimensionPath)}
        onChange={(event) => setFacet({ dimension: event.target.value })}
      />
    );

    const valueInput = (label: string) => (
      <Input
        id={id(`c${i}-f${j}-value`)}
        label={label}
        value={facet.value}
        readOnly={!editable}
        inputRef={inputRef(`facet-value-${i}-${j}`)}
        {...error(valuePath)}
        onChange={(event) => setFacet({ value: event.target.value })}
      />
    );
    const selectValue = facetSelectValue(facet.dimension, facet.value, facets ?? [], isTyped);
    const value = !pickFacets ? (
      valueInput("Value")
    ) : (
      <Stack spacing={Spacing.S}>
        <Select
          id={id(`c${i}-f${j}-value-choice`)}
          label="Value"
          value={selectValue}
          readOnly={!editable}
          disabled={loading || facet.dimension === ""}
          explanationText={loading ? "Loading…" : undefined}
          {...(facetChoice(facet.dimension, facet.value, facets ?? [], isTyped) === "other" ? { onBlur: () => touch(valuePath) } : error(valuePath))}
          onChange={(selected) => {
            touch(valuePath);
            const next = facetFromSelect(selected, facet.dimension, facet.value, facets ?? []);
            setTypedKey(key, next.typed);
            if (next.value !== facet.value) {
              setFacet({ value: next.value });
            }
            if (next.typed) {
              focus(`facet-value-${i}-${j}`);
            }
          }}
        >
          {facetValueOptions(facet.dimension, facet.value, facets, isTyped).map((option) => (
            <MenuItem key={option.value} primaryLabel={option.primary} secondaryLabel={option.secondary} value={option.value} selected={option.value === selectValue} />
          ))}
        </Select>
        {selectValue === TYPED && valueInput("Facet value")}
      </Stack>
    );

    return (
      <div key={`facet-${j}`} style={grid(ROW, "end")}>
        {dimension}
        {value}
        {editable ? (
          <RemoveButton
            label={`Remove facet ${j + 1} of condition ${i + 1}`}
            buttonRef={buttonRef(`remove-facet-${i}-${j}`)}
            onClick={() => {
              set(i, { facets: removeAt(row.facets, j) });
              forget(`f${i}-`);
              focus(j > 0 ? `remove-facet-${i}-${j - 1}` : `add-facet-${i}`);
            }}
          />
        ) : (
          <span />
        )}
      </div>
    );
  };

  const contextRow = (row: ConditionRow, i: number, j: number) => {
    const path = `conditions[${i}].contexts[${j}]`;
    const context = row.contexts[j];
    const key = `x${i}-${j}`;
    const loading = groupsResult === undefined;
    const choice = contextChoice(context, groups, typed.has(key));
    const setContext = (value: string) => set(i, { contexts: replaceAt(row.contexts, j, value) });
    const input = (label: string, explanationText: string | undefined, focusKey: string) => (
      <Input
        id={id(`c${i}-x${j}`)}
        label={label}
        explanationText={explanationText}
        value={context}
        readOnly={!editable}
        inputRef={inputRef(focusKey)}
        {...error(path)}
        onChange={(event) => setContext(event.target.value)}
      />
    );

    let controls;
    if (!loading && !pickGroups) {
      const help = groupsResult && !groupsResult.allowed ? `You need Contact groups access to pick groups. ${CONTEXT_HELP}` : CONTEXT_HELP;
      controls = <div style={{ gridColumn: "span 2" }}>{input("Visitor context", help, `context-${i}-${j}`)}</div>;
    } else {
      const selectValue = contextSelectValue(context, groups, typed.has(key));
      controls = (
        <>
          <Select
            id={id(`c${i}-x${j}-choice`)}
            label="Visitor context"
            value={selectValue}
            readOnly={!editable}
            disabled={loading}
            explanationText={loading ? "Loading…" : groupsResult?.truncated ? "Only the first 500 contact groups are listed." : undefined}
            inputRef={inputRef(`context-${i}-${j}`)}
            {...(!loading && choice === "custom" ? { onBlur: () => touch(path) } : error(path))}
            onChange={(selected) => {
              touch(path);
              const next = contextFromSelect(selected, context, groups);
              setTypedKey(key, next.typed);
              if (next.value !== context) {
                setContext(next.value);
              }
              if (next.typed) {
                focus(`context-input-${i}-${j}`);
              }
            }}
          >
            {loading && context !== "" ? (
              <MenuItem primaryLabel={context} value={selectValue} selected />
            ) : (
              contextOptions(groups).map((option) => (
                <MenuItem key={option.value} primaryLabel={option.primary} secondaryLabel={option.secondary} value={option.value} selected={option.value === selectValue} />
              ))
            )}
          </Select>
          {!loading && choice === "custom" ? input("Custom context", CONTEXT_HELP, `context-input-${i}-${j}`) : <span />}
        </>
      );
    }

    return (
      <div key={`context-${j}`} style={grid(ROW, "end")}>
        {controls}
        {editable ? (
          <RemoveButton
            label={`Remove context ${j + 1} of condition ${i + 1}`}
            buttonRef={buttonRef(`remove-context-${i}-${j}`)}
            onClick={() => {
              set(i, { contexts: removeAt(row.contexts, j) });
              forget(`x${i}-`);
              focus(j > 0 ? `remove-context-${i}-${j - 1}` : `add-context-${i}`);
            }}
          />
        ) : (
          <span />
        )}
      </div>
    );
  };

  const condition = (row: ConditionRow, i: number) => {
    const path = `conditions[${i}]`;
    return (
      <div
        key={i}
        role="group"
        aria-labelledby={id(`c${i}-title`)}
        aria-describedby={id(`c${i}-errors`)}
        onBlur={leaveGroup(path)}
        style={{ border: `${Spacing.Micro} solid var(--color-paper-border)`, borderRadius: BorderRadius.RadiusM, padding: Spacing.L }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: Spacing.S, marginBottom: Spacing.L }}>
          <div role="heading" aria-level={3} id={id(`c${i}-title`)}>
            <Headline size={HeadlineSize.S}>{`Condition ${i + 1}`}</Headline>
          </div>
          {editable && <RemoveButton label={`Remove condition ${i + 1}`} buttonRef={buttonRef(`remove-condition-${i}`)} onClick={() => removeCondition(i)} />}
        </div>
        <Stack spacing={Spacing.L}>
          <div style={grid("minmax(0,1fr) minmax(0,2fr)", "start")}>
            <Select
              id={id(`c${i}-anchoring`)}
              label="Query"
              value={row.anchoring}
              readOnly={!editable}
              {...error(`${path}.anchoring`)}
              onChange={(value) => {
                touch(`${path}.anchoring`);
                set(i, { anchoring: (value ?? "any") as Anchoring });
              }}
            >
              {anchoringOptions.map((option) => (
                <MenuItem key={option.value} primaryLabel={option.label} value={option.value} selected={option.value === row.anchoring} />
              ))}
            </Select>
            {row.anchoring !== "any" ? (
              <Input
                id={id(`c${i}-pattern`)}
                label="Text"
                value={row.pattern}
                readOnly={!editable}
                explanationText={row.anchoring === "is" ? "Leave empty to match the empty (browse) query." : "Whole words, not case-sensitive."}
                inputRef={inputRef(`pattern-${i}`)}
                {...error(`${path}.pattern`)}
                onChange={(event) => set(i, { pattern: event.target.value })}
              />
            ) : (
              <span />
            )}
          </div>

          {row.facets.map((_, j) => facetRow(row, i, j))}
          {row.contexts.map((_, j) => contextRow(row, i, j))}

          <InlineError id={id(`c${i}-errors`)}>
            {[`${path}.facets`, `${path}.contexts`]
              .filter((p) => visible(p) && byPath.has(p))
              .map((p) => byPath.get(p))
              .join(" ")}
          </InlineError>

          {editable && (
            <div style={{ display: "flex", flexWrap: "wrap", gap: Spacing.M }}>
              <Button
                label="Add facet"
                icon="xp-plus"
                color={ButtonColor.Secondary}
                disabled={!canAdd(row.facets.length, props.maxFacets)}
                buttonRef={buttonRef(`add-facet-${i}`)}
                onClick={() => {
                  set(i, { facets: [...row.facets, { dimension: "", value: "" }] });
                  focus(`facet-${i}-${row.facets.length}`);
                }}
              />
              <Button
                label="Add context"
                icon="xp-plus"
                color={ButtonColor.Secondary}
                disabled={!canAdd(row.contexts.length, props.maxContexts)}
                buttonRef={buttonRef(`add-context-${i}`)}
                onClick={() => {
                  set(i, { contexts: [...row.contexts, ""] });
                  focus(`context-${i}-${row.contexts.length}`);
                }}
              />
            </div>
          )}
        </Stack>
      </div>
    );
  };

  const loadErrors = [groupsResult === null ? GROUPS_ERROR : undefined, facetsError ?? undefined].filter(Boolean).join(" ");

  return (
    <FormItemWrapper label={props.label} invalid={props.invalid} validationMessage={props.validationMessage} editMode={props.editMode}>
      <div onBlur={leaveGroup("")} aria-describedby={id("errors")}>
        <Stack spacing={Spacing.M}>
          {loadErrors && <NotificationBarAlert>{loadErrors}</NotificationBarAlert>}
          <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnPaper} headline="How conditions match">
            The rule fires when any condition matches. Within one condition, the text, every facet and at least one context must match. A rule
            without conditions fires on every search.
          </Callout>
          <InlineError id={id("errors")}>{visible("") ? unmatched.map((e) => e.message).join(" ") : ""}</InlineError>
          {rows.map(condition)}
          {editable && (
            <div>
              <Button
                label="Add condition"
                icon="xp-plus"
                color={ButtonColor.Secondary}
                disabled={!canAdd(rows.length, props.maxConditions)}
                buttonRef={buttonRef("add-condition")}
                onClick={addCondition}
              />
            </div>
          )}
        </Stack>
      </div>
    </FormItemWrapper>
  );
};
