import { useEffect, useMemo, useState } from "react";
import {
  Button,
  ButtonColor,
  Callout,
  CalloutPlacementType,
  CalloutType,
  Cols,
  Card,
  Column,
  FormItemWrapper,
  Input,
  MenuItem,
  NotificationBarAlert,
  Row,
  Select,
  Spacing,
  Stack,
} from "@kentico/xperience-admin-components";
import { clear, peek } from "../reports/handoff";
import { isEditable, useFocusTargets, useServerErrors, useTouched, type RuleJsonFormComponentProps } from "./shared";
import {
  anchoringOptions,
  canAdd,
  conditionControlPaths,
  emptyCondition,
  parseConditions,
  partitionErrors,
  removeAt,
  replaceAt,
  serializeConditions,
  type Anchoring,
  type ConditionRow,
} from "./state";

export const RuleConditionsFormComponent = (props: RuleJsonFormComponentProps) => {
  const rows = useMemo(() => parseConditions(props.value), [props.value]);
  const json = useMemo(() => serializeConditions(rows), [rows]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json);
  const { byPath, unmatched } = partitionErrors(errors, conditionControlPaths(rows));
  const { ref, focus } = useFocusTargets();
  const { touch, visible } = useTouched(props);
  const id = (suffix: string) => `${props.name}-${suffix}`;
  const inputRef = (key: string) => ref(key) as React.RefObject<HTMLInputElement>;
  const buttonRef = (key: string) => ref(key) as React.RefObject<HTMLButtonElement>;

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
    focus(index > 0 ? `remove-condition-${index - 1}` : "add-condition");
  };

  const condition = (row: ConditionRow, i: number) => {
    const path = `conditions[${i}]`;
    return (
      <Card
        key={i}
        headline={<span role="heading" aria-level={4}>{`Condition ${i + 1}`}</span>}
        footer={
          editable && (
            <Button
              label="Remove condition"
              icon="xp-bin"
              destructive
              color={ButtonColor.Tertiary}
              buttonRef={buttonRef(`remove-condition-${i}`)}
              onClick={() => removeCondition(i)}
            />
          )
        }
      >
        <Stack spacing={Spacing.M}>
          <Row spacingX={Spacing.M}>
            <Column cols={Cols.Col4}>
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
            </Column>
            {row.anchoring !== "any" && (
              <Column cols={Cols.Col8}>
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
              </Column>
            )}
          </Row>

          {row.facets.map((facet, j) => (
            <Row key={`facet-${j}`} spacingX={Spacing.M}>
              <Column cols={Cols.Col5}>
                <Input
                  id={id(`c${i}-f${j}-dimension`)}
                  label="Facet"
                  value={facet.dimension}
                  readOnly={!editable}
                  inputRef={inputRef(`facet-${i}-${j}`)}
                  {...error(`${path}.facets[${j}].dimension`)}
                  onChange={(event) => set(i, { facets: replaceAt(row.facets, j, { ...facet, dimension: event.target.value }) })}
                />
              </Column>
              <Column cols={Cols.Col5}>
                <Input
                  id={id(`c${i}-f${j}-value`)}
                  label="Value"
                  value={facet.value}
                  readOnly={!editable}
                  {...error(`${path}.facets[${j}].value`)}
                  onChange={(event) => set(i, { facets: replaceAt(row.facets, j, { ...facet, value: event.target.value }) })}
                />
              </Column>
              {editable && (
                <Column cols={Cols.Col2}>
                  <Button
                    label="Remove"
                    title={`Remove facet ${j + 1} of condition ${i + 1}`}
                    icon="xp-bin"
                    color={ButtonColor.Tertiary}
                    buttonRef={buttonRef(`remove-facet-${i}-${j}`)}
                    onClick={() => {
                      set(i, { facets: removeAt(row.facets, j) });
                      focus(j > 0 ? `remove-facet-${i}-${j - 1}` : `add-facet-${i}`);
                    }}
                  />
                </Column>
              )}
            </Row>
          ))}

          {row.contexts.map((context, j) => (
            <Row key={`context-${j}`} spacingX={Spacing.M}>
              <Column cols={Cols.Col10}>
                <Input
                  id={id(`c${i}-x${j}`)}
                  label="Visitor context"
                  explanationText="For example a contact group code name. Letters, digits, '_', '.' and '-'."
                  value={context}
                  readOnly={!editable}
                  inputRef={inputRef(`context-${i}-${j}`)}
                  {...error(`${path}.contexts[${j}]`)}
                  onChange={(event) => set(i, { contexts: replaceAt(row.contexts, j, event.target.value) })}
                />
              </Column>
              {editable && (
                <Column cols={Cols.Col2}>
                  <Button
                    label="Remove"
                    title={`Remove context ${j + 1} of condition ${i + 1}`}
                    icon="xp-bin"
                    color={ButtonColor.Tertiary}
                    buttonRef={buttonRef(`remove-context-${i}-${j}`)}
                    onClick={() => {
                      set(i, { contexts: removeAt(row.contexts, j) });
                      focus(j > 0 ? `remove-context-${i}-${j - 1}` : `add-context-${i}`);
                    }}
                  />
                </Column>
              )}
            </Row>
          ))}

          {(byPath.has(`${path}.facets`) || byPath.has(`${path}.contexts`)) && (
            <NotificationBarAlert>{[byPath.get(`${path}.facets`), byPath.get(`${path}.contexts`)].filter(Boolean).join(" ")}</NotificationBarAlert>
          )}

          {editable && (
            <Row spacingX={Spacing.M}>
              <Column>
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
              </Column>
              <Column>
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
              </Column>
            </Row>
          )}
        </Stack>
      </Card>
    );
  };

  return (
    <FormItemWrapper label={props.label} invalid={props.invalid} validationMessage={props.validationMessage} editMode={props.editMode}>
      <Stack spacing={Spacing.M}>
        <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnPaper} headline="How conditions match">
          The rule fires when any condition matches. Within one condition, the text, every facet and at least one context must match. A rule
          without conditions fires on every search.
        </Callout>
        {unmatched.length > 0 && <NotificationBarAlert>{unmatched.map((e) => e.message).join(" ")}</NotificationBarAlert>}
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
    </FormItemWrapper>
  );
};
