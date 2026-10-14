import { useEffect, useMemo, useState } from "react";
import type { FormComponentProps } from "@kentico/xperience-admin-base";
import {
  BorderRadius,
  Button,
  ButtonColor,
  FormItemWrapper,
  Icon,
  Input,
  NotificationBarAlert,
  Spacing,
  Stack,
  Tag,
} from "@kentico/xperience-admin-components";
import { clear, peek } from "../reports/handoff";
import { ItemPicker, useItemTitles } from "./ItemPicker";
import { InlineError, RemoveButton, isEditable, useFocusTargets, useServerErrors, useTouched } from "./shared";
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

/** A stored item's title; null is an item no longer in this index, undefined one still loading. */
export const itemLabel = (item: { readonly title: string } | null | undefined) => item?.title ?? (item === null ? "Unknown item" : "Loading…");

export const ITEM_ERROR_STYLE = { color: "var(--color-alert-text)" } as const;

export const RulePinsFormComponent = (props: RuleItemsFormComponentProps) => {
  const rows = useMemo(() => parsePins(props.value), [props.value]);
  const json = useMemo(() => serializePins(rows), [rows]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json, "Pins");
  const { byPath, unmatched } = partitionErrors(errors, pinControlPaths(rows), pinAlias);
  const { ref, focus } = useFocusTargets();
  const { touch, visible, leaveGroup } = useTouched(props);
  const { title, error, remember } = useItemTitles(props, rows.map((row) => row.itemGuid));
  const [picking, setPicking] = useState(false);

  const hidden = new Set(parseHides(fieldValue(props.fieldValues as Record<string, unknown>, "Hides")).map(sameGuid));
  const update = (next: readonly PinRow[]) => props.onChange?.(serializePins(next));
  // An item handed over by a Search reports row action becomes the first pin; only an empty list on Create rule takes it.
  const [handedItem] = useState(() => (editable && rows.length === 0 ? peek("rulePin", window.location.pathname) : null));
  useEffect(() => {
    clear("rulePin", window.location.pathname);
    if (handedItem !== null) {
      update([{ itemGuid: handedItem, position: "1" }]);
    }
    // Runs once on mount: the handoff is a one-time prefill.
  }, []);
  const remove = (index: number) => {
    update(removeAt(rows, index));
    focus(index > 0 ? `remove-${index - 1}` : "add");
  };
  // npm Input takes no aria-label; the row's position input is named after its item.
  useEffect(() => {
    rows.forEach((row, index) => (ref(`position-${index}`).current as HTMLInputElement | null)?.setAttribute("aria-label", `Position of ${itemLabel(title(row.itemGuid))}`));
  });
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
      <div onBlur={leaveGroup("")} aria-describedby={`${props.name}-errors`}>
        <Stack spacing={Spacing.M}>
          {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
          <InlineError id={`${props.name}-errors`}>{visible("") ? unmatched.map((e) => e.message).join(" ") : ""}</InlineError>
          {rows.map((row, index) => {
            const positionPath = `actions.pins[${index}].position`;
            const itemPath = `actions.pins[${index}].itemGuid`;
            const item = title(row.itemGuid);
            const rowErrors = [positionPath, itemPath].filter((path) => visible(path) && byPath.has(path)).map((path) => byPath.get(path));
            return (
              <div key={`${row.itemGuid}-${index}`}>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: Spacing.M,
                    border: `${Spacing.Micro} solid var(--color-paper-border)`,
                    borderRadius: BorderRadius.RadiusM,
                    padding: `${Spacing.S} ${Spacing.S} ${Spacing.S} ${Spacing.L}`,
                  }}
                >
                  <span aria-hidden="true" style={{ display: "inline-flex", color: "var(--color-text-default-on-light)" }}>
                    <Icon name="xp-pin" />
                  </span>
                  <span
                    title={row.itemGuid}
                    style={{ flex: 1, minWidth: 0, fontWeight: 600, color: "var(--color-text-default-on-light)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
                  >
                    {itemLabel(item)}
                  </span>
                  {item === null ? <Tag label="Not in this index" /> : item?.contentType && <Tag label={item.contentType} />}
                  <div style={{ width: "96px", flexShrink: 0 }}>
                    <Input
                      id={`${props.name}-pin-${index}-position`}
                      type="number"
                      min={1}
                      max={props.maxPosition}
                      value={row.position}
                      readOnly={!editable}
                      invalid={visible(positionPath) && byPath.has(positionPath)}
                      inputRef={ref(`position-${index}`) as React.RefObject<HTMLInputElement>}
                      onBlur={() => touch(positionPath)}
                      onChange={(event) => update(replaceAt(rows, index, { ...row, position: event.target.value }))}
                    />
                  </div>
                  {editable && (
                    <RemoveButton label={`Remove pin ${index + 1}`} buttonRef={ref(`remove-${index}`) as React.RefObject<HTMLButtonElement>} onClick={() => remove(index)} />
                  )}
                </div>
                {rowErrors.length > 0 && (
                  <span role="alert" style={ITEM_ERROR_STYLE}>
                    {rowErrors.join(" ")}
                  </span>
                )}
              </div>
            );
          })}
          {editable && (
            <div>
              <Button
                label="Pin items"
                icon="xp-plus"
                color={ButtonColor.Secondary}
                disabled={!canAdd(rows.length, props.maxPins)}
                buttonRef={ref("add") as React.RefObject<HTMLButtonElement>}
                onClick={() => setPicking(true)}
              />
            </div>
          )}
        </Stack>
      </div>
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
