import { useEffect, useMemo, useRef, useState } from "react";
import { useFormComponentCommandProvider, type FormComponentProps } from "@kentico/xperience-admin-base";
import { FormItemWrapper, Spacing, Spinner } from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state";
import type { RuleStatus } from "./listing";
import { StatusText } from "./RuleListingTemplate";
import { statusArguments } from "./state";

interface StatusResult {
  readonly status: RuleStatus;
  readonly detail: string | null;
}

/** Display only: the status of the form's current Enabled and validity values, worked out by the server like the rules listing. Never calls onChange. */
export const RuleStatusFormComponent = (props: FormComponentProps<string>) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const request = useMemo(() => latestRequest(), []);
  const propsRef = useRef(props);
  propsRef.current = props;
  // undefined while the first answer is pending, null when the command failed.
  const [result, setResult] = useState<StatusResult | null>();
  const key = JSON.stringify(statusArguments(props.fieldValues as Record<string, unknown>));

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
