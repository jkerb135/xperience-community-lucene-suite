import { useRef, useState } from "react";
import {
  Button,
  ButtonColor,
  ButtonSize,
  Checkbox,
  Inline,
  Input,
  NotificationBarAlert,
  SidePanel,
  SidePanelSize,
  Spacing,
  Spinner,
  Stack,
  Tag,
} from "@kentico/xperience-admin-components";
import { addContext, contextLabel, withGroups, type ContactGroup } from "./compare";

/** The ContactGroups command's answer. */
export interface ContactGroupsState {
  readonly allowed: boolean;
  readonly truncated: boolean;
  readonly groups: ContactGroup[];
}

interface ContextPickerProps {
  readonly id: string;
  readonly columnName: string;
  readonly contexts: readonly string[];
  readonly onChange: (contexts: string[]) => void;
  /** undefined until loaded; null when loading failed. */
  readonly groups: ContactGroupsState | null | undefined;
  readonly onLoadGroups: () => void;
  readonly maxContexts: number;
  readonly maxContextLength: number;
}

// Module-level so the callback ref is stable: focuses the panel content once, when it mounts.
const focusOnMount = (element: HTMLElement | null) => element?.focus();

/** Simulated contexts of one compare column: chips, a contact-group panel and free text. */
export const ContextPicker = ({
  id,
  columnName,
  contexts,
  onChange,
  groups,
  onLoadGroups,
  maxContexts,
  maxContextLength,
}: ContextPickerProps) => {
  const [text, setText] = useState("");
  const [error, setError] = useState<string>();
  const [draft, setDraft] = useState<ReadonlySet<string>>();
  const opener = useRef<HTMLButtonElement>(null);
  const knownGroups = groups?.groups ?? [];

  const commit = () => {
    if (!text.trim()) {
      return;
    }
    const result = addContext(contexts, text, maxContexts, maxContextLength);
    if ("error" in result) {
      setError(result.error);
      return;
    }
    setText("");
    setError(undefined);
    onChange(result.contexts);
  };

  const openGroups = () => {
    if (groups === undefined || groups === null) {
      onLoadGroups();
    }
    // Groups may still be loading; any context equal to a group's code name shows as chosen once they arrive.
    setDraft(new Set(contexts));
  };

  const closeGroups = (apply: boolean) => {
    if (apply && draft) {
      onChange(
        withGroups(
          contexts,
          knownGroups,
          [...draft].filter((name) => knownGroups.some((group) => group.name === name)),
        ),
      );
    }
    setDraft(undefined);
    // Deferred so it runs after the SidePanel has finished moving focus itself.
    requestAnimationFrame(() => opener.current?.focus());
  };

  return (
    <Stack spacing={Spacing.S}>
      <span id={`${id}-label`} style={{ color: "var(--color-text-default-on-light)" }}>
        Simulated contexts
      </span>
      {contexts.length > 0 ? (
        <ul
          aria-labelledby={`${id}-label`}
          style={{ display: "flex", flexWrap: "wrap", gap: Spacing.S, listStyle: "none", margin: 0, padding: 0 }}
        >
          {contexts.map((context) => (
            <li key={context}>
              <Tag label={contextLabel(context, knownGroups)} onRemoveClick={() => onChange(contexts.filter((c) => c !== context))} />
            </li>
          ))}
        </ul>
      ) : (
        <span style={{ color: "var(--color-text-low-emphasis)" }}>None — searched as an anonymous visitor.</span>
      )}
      {groups?.allowed !== false && (
        <div>
          <Button label="Contact groups…" size={ButtonSize.S} color={ButtonColor.Secondary} buttonRef={opener} onClick={openGroups} />
        </div>
      )}
      <Input
        id={`${id}-add-context`}
        label="Add a context"
        value={text}
        invalid={error !== undefined}
        validationMessage={error}
        onChange={(event) => {
          setText(event.target.value);
          setError(undefined);
        }}
        onKeyPress={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            commit();
          }
        }}
        onBlur={commit}
      />
      <SidePanel
        headline={<span role="heading" aria-level={2}>{`Contact groups for ${columnName}`}</span>}
        size={SidePanelSize.Stackable}
        isVisible={draft !== undefined}
        tooltips={{ close: "Close" }}
        onClose={() => closeGroups(false)}
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end", width: "100%" }}>
            <Inline spacingX={Spacing.S}>
              <Button label="Cancel" color={ButtonColor.Secondary} onClick={() => closeGroups(false)} />
              <Button label={`Apply (${draft?.size ?? 0})`} color={ButtonColor.Primary} onClick={() => closeGroups(true)} />
            </Inline>
          </div>
        }
      >
        {draft && (
          <div ref={focusOnMount} tabIndex={-1} style={{ outline: "none" }}>
            {groups === undefined ? (
              <Spinner />
            ) : groups === null ? (
              <NotificationBarAlert>Contact groups could not be loaded. Close the panel and try again.</NotificationBarAlert>
            ) : groups.groups.length === 0 ? (
              <span style={{ color: "var(--color-text-low-emphasis)" }}>This site has no contact groups.</span>
            ) : (
              <Stack spacing={Spacing.S}>
                {groups.truncated && <span style={{ color: "var(--color-text-low-emphasis)" }}>Showing the first 500 contact groups.</span>}
                <div role="group" aria-label="Contact groups">
                  <Stack spacing={Spacing.S}>
                    {groups.groups.map((group) => (
                      <Checkbox
                        key={group.name}
                        label={`${group.displayName} (${group.name})`}
                        checked={draft.has(group.name)}
                        onChange={(_, checked) => {
                          const next = new Set(draft);
                          if (checked) {
                            next.add(group.name);
                          } else {
                            next.delete(group.name);
                          }
                          setDraft(next);
                        }}
                      />
                    ))}
                  </Stack>
                </div>
              </Stack>
            )}
          </div>
        )}
      </SidePanel>
    </Stack>
  );
};
