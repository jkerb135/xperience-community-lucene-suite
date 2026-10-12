import { useMemo } from "react";
import {
  Button,
  ButtonColor,
  Cols,
  Column,
  FormItemWrapper,
  Input,
  NotificationBarAlert,
  Row,
  Spacing,
  Stack,
} from "@kentico/xperience-admin-components";
import { isEditable, useFocusTargets, useServerErrors, useTouched, type RuleJsonFormComponentProps } from "./shared";
import { boostAlias, boostControlPaths, canAdd, emptyBoost, parseBoosts, partitionErrors, removeAt, replaceAt, serializeBoosts, type BoostRow } from "./state";

export const RuleBoostsFormComponent = (props: RuleJsonFormComponentProps) => {
  const rows = useMemo(() => parseBoosts(props.value), [props.value]);
  const json = useMemo(() => serializeBoosts(rows), [rows]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json);
  const { byPath, unmatched } = partitionErrors(errors, boostControlPaths(rows), boostAlias);
  const { ref, focus } = useFocusTargets();
  const { touch, visible } = useTouched(props);

  const update = (next: readonly BoostRow[]) => props.onChange?.(serializeBoosts(next));
  const add = () => {
    update([...rows, emptyBoost]);
    focus(`field-${rows.length}`);
  };
  const remove = (index: number) => {
    update(removeAt(rows, index));
    focus(index > 0 ? `remove-${index - 1}` : "add");
  };
  const field = (index: number, name: keyof BoostRow, label: string, extra: object = {}) => {
    const path = `actions.boosts[${index}].${name}`;
    return (
      <Input
        id={`${props.name}-boost-${index}-${name}`}
        label={label}
        value={rows[index][name]}
        readOnly={!editable}
        invalid={visible(path) && byPath.has(path)}
        validationMessage={visible(path) ? byPath.get(path) : undefined}
        onBlur={() => touch(path)}
        inputRef={ref(`${name}-${index}`) as React.RefObject<HTMLInputElement>}
        onChange={(event) => update(replaceAt(rows, index, { ...rows[index], [name]: event.target.value }))}
        {...extra}
      />
    );
  };

  return (
    <FormItemWrapper
      label={props.label}
      explanationText="Strength −100 to 100 (not 0). Positive values boost matching results; negative values bury them."
      invalid={props.invalid}
      validationMessage={props.validationMessage}
      editMode={props.editMode}
    >
      <Stack spacing={Spacing.M}>
        {unmatched.length > 0 && (
          <NotificationBarAlert>{unmatched.map((error) => error.message).join(" ")}</NotificationBarAlert>
        )}
        {rows.map((_, index) => (
          <div key={index}>
            <Row spacingX={Spacing.M}>
              <Column cols={Cols.Col4}>{field(index, "field", "Field")}</Column>
              <Column cols={Cols.Col4}>{field(index, "value", "Value")}</Column>
              <Column cols={Cols.Col2}>{field(index, "strength", "Strength", { type: "number", min: -100, max: 100 })}</Column>
              {editable && (
                <Column cols={Cols.Col2}>
                  <Button
                    label="Remove"
                    title={`Remove boost ${index + 1}`}
                    icon="xp-bin"
                    color={ButtonColor.Tertiary}
                    buttonRef={ref(`remove-${index}`) as React.RefObject<HTMLButtonElement>}
                    onClick={() => remove(index)}
                  />
                </Column>
              )}
            </Row>
          </div>
        ))}
        {editable && (
          <div>
            <Button
              label="Add boost"
              icon="xp-plus"
              color={ButtonColor.Secondary}
              disabled={!canAdd(rows.length, props.maxBoosts)}
              buttonRef={ref("add") as React.RefObject<HTMLButtonElement>}
              onClick={add}
            />
          </div>
        )}
      </Stack>
    </FormItemWrapper>
  );
};
