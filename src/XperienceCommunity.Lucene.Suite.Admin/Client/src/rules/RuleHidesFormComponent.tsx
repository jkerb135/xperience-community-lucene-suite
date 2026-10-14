import { useEffect, useMemo, useState } from "react";
import { Button, ButtonColor, FormItemWrapper, NotificationBarAlert, Spacing, Stack, Tag } from "@kentico/xperience-admin-components";
import { clear, peek } from "../reports/handoff";
import { ItemPicker, useItemTitles } from "./ItemPicker";
import { ITEM_ERROR_STYLE, itemLabel, type RuleItemsFormComponentProps } from "./RulePinsFormComponent";
import { InlineError, isEditable, useFocusTargets, useServerErrors, useTouched } from "./shared";
import { addHides, canAdd, fieldValue, hideControlPaths, parseHides, parsePins, partitionErrors, removeAt, sameGuid, serializeHides } from "./state";

export const RuleHidesFormComponent = (props: RuleItemsFormComponentProps) => {
  const hides = useMemo(() => parseHides(props.value), [props.value]);
  const json = useMemo(() => serializeHides(hides), [hides]);
  const editable = isEditable(props);
  const errors = useServerErrors(props, json, "Hides");
  const { visible, leaveGroup } = useTouched(props);
  const { byPath, unmatched } = partitionErrors(errors, hideControlPaths(hides));
  const { ref, focus } = useFocusTargets();
  const { title, error, remember } = useItemTitles(props, hides);
  const [picking, setPicking] = useState(false);

  const pinned = new Set(parsePins(fieldValue(props.fieldValues as Record<string, unknown>, "Pins")).map((pin) => sameGuid(pin.itemGuid)));
  const update = (next: readonly string[]) => props.onChange?.(serializeHides(next));
  // An item handed over by a Search reports row action becomes the hidden item; only an empty list on Create rule takes it.
  const [handedItem] = useState(() => (editable && hides.length === 0 ? peek("ruleHide", window.location.pathname) : null));
  useEffect(() => {
    clear("ruleHide", window.location.pathname);
    if (handedItem !== null) {
      update([handedItem]);
    }
    // Runs once on mount: the handoff is a one-time prefill.
  }, []);
  // The tags' own remove buttons take no ref, so focus goes to the button that adds items.
  const remove = (index: number) => {
    update(removeAt(hides, index));
    focus("add");
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
      <div onBlur={leaveGroup("")} aria-describedby={`${props.name}-errors`}>
        <Stack spacing={Spacing.M}>
          {error && <NotificationBarAlert>{error}</NotificationBarAlert>}
          <InlineError id={`${props.name}-errors`}>{visible("") ? unmatched.map((e) => e.message).join(" ") : ""}</InlineError>
          {hides.length > 0 && (
            <div style={{ display: "flex", flexWrap: "wrap", gap: Spacing.S }}>
              {hides.map((guid, index) => {
                const item = title(guid);
                return (
                  <Tag
                    key={`${guid}-${index}`}
                    label={item === null ? `${itemLabel(item)} (not in this index)` : itemLabel(item)}
                    tooltipText={guid}
                    readOnly={!editable}
                    onRemoveClick={editable ? () => remove(index) : undefined}
                  />
                );
              })}
            </div>
          )}
          {hides.map((guid, index) => {
            const path = `actions.hides[${index}]`;
            return (
              visible(path) &&
              byPath.has(path) && (
                <span key={`error-${guid}-${index}`} role="alert" style={ITEM_ERROR_STYLE}>
                  {`${itemLabel(title(guid))}: ${byPath.get(path)}`}
                </span>
              )
            );
          })}
          {editable && (
            <div>
              <Button
                label="Hide items"
                icon="xp-plus"
                color={ButtonColor.Secondary}
                disabled={!canAdd(hides.length, props.maxHides)}
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
