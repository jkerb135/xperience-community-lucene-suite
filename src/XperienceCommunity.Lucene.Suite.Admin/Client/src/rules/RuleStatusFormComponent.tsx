import { useEffect, useMemo, useRef, useState } from "react";
import { useFormComponentCommandProvider, type FormComponentProps } from "@kentico/xperience-admin-base";
import { Button, ButtonColor, ButtonSize, Dialog, FormItemWrapper, NotificationBarAlert, Spacing, Spinner } from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state";
import type { RuleStatus } from "./listing";
import { StatusText } from "./RuleListingTemplate";
import { statusArguments } from "./state";

interface StatusResult {
  readonly status: RuleStatus;
  readonly detail: string | null;
}

interface RuleStatusFormComponentProps extends FormComponentProps<string> {
  /** Set by the server when the last save was refused because someone else changed the rule; a later successful save clears it. */
  readonly conflict?: boolean;
}

/** Display only: the status of the form's current Enabled and validity values, worked out by the server like the rules listing. Never calls onChange. */
export const RuleStatusFormComponent = (props: RuleStatusFormComponentProps) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const request = useMemo(() => latestRequest(), []);
  const propsRef = useRef(props);
  propsRef.current = props;
  // undefined while the first answer is pending, null when the command failed.
  const [result, setResult] = useState<StatusResult | null>();
  const key = JSON.stringify(statusArguments(props.fieldValues as Record<string, unknown>));
  const [confirming, setConfirming] = useState(false);
  const reloadButton = useRef<HTMLButtonElement>(null);

  // Button has no aria-label prop; the visible "Reload" alone does not say what is reloaded.
  useEffect(() => reloadButton.current?.setAttribute("aria-label", "Reload the rule"), [props.conflict]);

  useEffect(() => {
    void (async () => {
      const { latest, value } = await request((abort) =>
        executeCommand<StatusResult, ReturnType<typeof statusArguments>>(propsRef.current, "Status", JSON.parse(key), undefined, abort),
      );
      if (latest) {
        setResult(value?.status ? value : null);
      }
    })();
  }, [key, executeCommand, request]);

  return (
    <FormItemWrapper label={props.label} editMode={props.editMode}>
      <div aria-live="polite">
        {props.conflict && (
          <div style={{ marginBottom: Spacing.S }}>
            <NotificationBarAlert
              actions={
                <Button label="Reload" size={ButtonSize.S} color={ButtonColor.Secondary} buttonRef={reloadButton} onClick={() => setConfirming(true)} />
              }
            >
              This rule was changed by someone else.
            </NotificationBarAlert>
          </div>
        )}
      </div>
      <Dialog
        isOpen={confirming}
        headline="Reload the rule?"
        headerCloseButton={{ tooltipText: "Close" }}
        isDismissable
        shouldReturnFocusAfterClose
        onClose={() => setConfirming(false)}
        onAfterClose={() => reloadButton.current?.focus()}
        cancelAction={{ label: "Cancel", onClick: () => setConfirming(false) }}
        confirmAction={{ label: "Reload", onClick: () => window.location.reload() }}
      >
        Your unsaved changes are lost.
      </Dialog>
      <div role="status" style={{ display: "flex", alignItems: "center", gap: Spacing.S, color: "var(--color-text-low-emphasis)" }}>
        {result === undefined ? (
          <Spinner />
        ) : result === null ? (
          "The status could not be worked out."
        ) : (
          <>
            <StatusText status={result.status} />
            {result.detail && <span>{`· ${result.detail}`}</span>}
          </>
        )}
      </div>
    </FormItemWrapper>
  );
};
