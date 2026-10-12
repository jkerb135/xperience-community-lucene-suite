import { useMemo, useState } from "react";
import type { FormComponentProps } from "@kentico/xperience-admin-base";
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
  Tag,
  TextWithLabel,
} from "@kentico/xperience-admin-components";
import { ItemPicker, useItemTitles } from "./ItemPicker";
import { isEditable, useFocusTargets, useServerErrors, useTouched } from "./shared";
import {
  addPins,
  canAdd,
  fieldValue,
  parseHides,
  parsePins,
  partitionErrors,
  pinAlias,
  pinControlPaths,
  removeAt,
  replaceAt,
  sameGuid,
  serializePins,
  type PinRow,
} from "./state";

/** Props the server sends to the pins and hides components (RuleItemsClientProperties). */
export interface RuleItemsFormComponentProps extends FormComponentProps<string> {
  readonly maxPins: number;
  readonly maxPosition: number;
  readonly maxHides: number;
}

/** One stored item: its title (or "Not in this index") above its ItemGuid. */
export const ItemSummary = ({ guid, item }: { readonly guid: string; readonly item: { readonly title: string } | null | undefined }) => (
  <Stack spacing={Spacing.XS}>
    <TextWithLabel label={item?.title ?? (item === null ? "Unknown item" : "Loading…")} value={guid} />
    {item === null && (
      <div>
        <Tag label="Not in this index" />
      </div>
    )}
  </Stack>
);

export const RulePinsFormComponent = (props: RuleItemsFormComponentProps) => {
  const rows = useMemo(() => parsePins(props.value), [props.value]);
  const json = useMemo(() => serializePins(rows), [rows]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json);
  const { byPath, unmatched } = partitionErrors(errors, pinControlPaths(rows), pinAlias);
  const { ref, focus } = useFocusTargets();
  const { touch, visible } = useTouched(props);
  const { title, error, remember } = useItemTitles(props, rows.map((row) => row.itemGuid));
  const [picking, setPicking] = useState(false);

  const hidden = new Set(parseHides(fieldValue(props.fieldValues as Record<string, unknown>, "Hides")).map(sameGuid));
  const update = (next: readonly PinRow[]) => props.onChange?.(serializePins(next));
  const remove = (index: number) => {
    update(removeAt(rows, index));
    focus(index > 0 ? `remove-${index - 1}` : "add");
  };
  const closePicker = () => {
    setPicking(false);
    // Deferred so it runs after the SidePanel has finished moving focus itself.
    requestAnimationFrame(() => ref("add").current?.focus());
  };

  return (
    <FormItemWrapper
      label={props.label}
      explanationText="Pinned items show at fixed positions (1 is first) when the rule fires."
      invalid={props.invalid}
      validationMessage={props.validationMessage}
      editMode={props.editMode}
    >
      <Stack spacing={Spacing.M}>
        {(unmatched.length > 0 || error) && (
          <NotificationBarAlert>{[error, ...unmatched.map((e) => e.message)].filter(Boolean).join(" ")}</NotificationBarAlert>
        )}
        {rows.map((row, index) => {
          const positionPath = `actions.pins[${index}].position`;
          const itemPath = `actions.pins[${index}].itemGuid`;
          return (
            <div key={`${row.itemGuid}-${index}`}>
              <Row spacingX={Spacing.M}>
                <Column cols={Cols.Col4} colsLg={Cols.Col2}>
                  <Input
                    id={`${props.name}-pin-${index}-position`}
                    label="Position"
                    type="number"
                    min={1}
                    max={props.maxPosition}
                    value={row.position}
                    readOnly={!editable}
                    invalid={visible(positionPath) && byPath.has(positionPath)}
                    validationMessage={visible(positionPath) ? byPath.get(positionPath) : undefined}
                    onBlur={() => touch(positionPath)}
                    onChange={(event) => update(replaceAt(rows, index, { ...row, position: event.target.value }))}
                  />
                </Column>
                <Column cols={Cols.Col8} colsLg={editable ? Cols.Col7 : Cols.Col10}>
                  <ItemSummary guid={row.itemGuid} item={title(row.itemGuid)} />
                  {byPath.has(itemPath) && (
                    <span role="alert" style={{ color: "var(--color-alert-text)" }}>
                      {byPath.get(itemPath)}
                    </span>
                  )}
                </Column>
                {editable && (
                  <Column cols={Cols.Col12} colsLg={Cols.Col3}>
                    <Button
                      label="Remove"
                      title={`Remove pin ${index + 1}`}
                      icon="xp-bin"
                      color={ButtonColor.Tertiary}
                      buttonRef={ref(`remove-${index}`) as React.RefObject<HTMLButtonElement>}
                      onClick={() => remove(index)}
                    />
                  </Column>
                )}
              </Row>
            </div>
          );
        })}
        {editable && (
          <div>
            <Button
              label="Add pins"
              icon="xp-pin"
              color={ButtonColor.Secondary}
              disabled={!canAdd(rows.length, props.maxPins)}
              buttonRef={ref("add") as React.RefObject<HTMLButtonElement>}
              onClick={() => setPicking(true)}
            />
          </div>
        )}
      </Stack>
      {editable && (
        <ItemPicker
          componentProps={props}
          isVisible={picking}
          headline="Choose items to pin"
          added={new Set(rows.map((row) => sameGuid(row.itemGuid)))}
          excluded={hidden}
          excludedNote="Hidden by this rule"
          onAdd={(items) => {
            remember(items);
            update(addPins(rows, items.map((item) => item.itemGuid)));
            closePicker();
          }}
          onClose={closePicker}
        />
      )}
    </FormItemWrapper>
  );
};
