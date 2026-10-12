import { useMemo, useState } from "react";
import { Button, ButtonColor, Cols, Column, FormItemWrapper, NotificationBarAlert, Row, Spacing, Stack } from "@kentico/xperience-admin-components";
import { ItemPicker, useItemTitles } from "./ItemPicker";
import { ItemSummary, type RuleItemsFormComponentProps } from "./RulePinsFormComponent";
import { isEditable, useFocusTargets, useServerErrors } from "./shared";
import { addHides, canAdd, fieldValue, hideControlPaths, parseHides, parsePins, partitionErrors, removeAt, sameGuid, serializeHides } from "./state";

export const RuleHidesFormComponent = (props: RuleItemsFormComponentProps) => {
  const hides = useMemo(() => parseHides(props.value), [props.value]);
  const json = useMemo(() => serializeHides(hides), [hides]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json);
  const { byPath, unmatched } = partitionErrors(errors, hideControlPaths(hides));
  const { ref, focus } = useFocusTargets();
  const { title, error, remember } = useItemTitles(props, hides);
  const [picking, setPicking] = useState(false);

  const pinned = new Set(parsePins(fieldValue(props.fieldValues as Record<string, unknown>, "Pins")).map((pin) => sameGuid(pin.itemGuid)));
  const update = (next: readonly string[]) => props.onChange?.(serializeHides(next));
  const remove = (index: number) => {
    update(removeAt(hides, index));
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
      explanationText="Hidden items never show in results when the rule fires."
      invalid={props.invalid}
      validationMessage={props.validationMessage}
      editMode={props.editMode}
    >
      <Stack spacing={Spacing.M}>
        {(unmatched.length > 0 || error) && (
          <NotificationBarAlert>{[error, ...unmatched.map((e) => e.message)].filter(Boolean).join(" ")}</NotificationBarAlert>
        )}
        {hides.map((guid, index) => {
          const path = `actions.hides[${index}]`;
          return (
            <div key={`${guid}-${index}`}>
              <Row spacingX={Spacing.M}>
                <Column cols={editable ? Cols.Col9 : Cols.Col12}>
                  <ItemSummary guid={guid} item={title(guid)} />
                  {byPath.has(path) && (
                    <span role="alert" style={{ color: "var(--color-alert-text)" }}>
                      {byPath.get(path)}
                    </span>
                  )}
                </Column>
                {editable && (
                  <Column cols={Cols.Col3}>
                    <Button
                      label="Remove"
                      title={`Remove hidden item ${index + 1}`}
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
              label="Add hidden items"
              icon="xp-eye-slash"
              color={ButtonColor.Secondary}
              disabled={!canAdd(hides.length, props.maxHides)}
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
          headline="Choose items to hide"
          added={new Set(hides.map(sameGuid))}
          excluded={pinned}
          excludedNote="Pinned by this rule"
          onAdd={(items) => {
            remember(items);
            update(addHides(hides, items.map((item) => item.itemGuid)));
            closePicker();
          }}
          onClose={closePicker}
        />
      )}
    </FormItemWrapper>
  );
};
