import { useEffect, useMemo, useRef, useState } from "react";
import { useFormComponentCommandProvider, type FormComponentProps } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  FormItemWrapper,
  Input,
  Spacing,
  Stack,
  Tag,
} from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state";
import { InlineError, isEditable, useFocusTargets, useTouched } from "../rules/shared";
import { debounce, fieldValue, removeAt, type RuleError } from "../rules/state";
import { commitPending, focusAfterRemove, parseTerms, serializeTerms, termErrors } from "./state";
import { clear, peek } from "../reports/handoff";

/** Props the server sends to the terms component (SynonymTermsClientProperties). */
export interface SynonymTermsFormComponentProps extends FormComponentProps<string> {
  readonly maxTerms: number;
  readonly maxTermLength: number;
}

/** Asks the component's Validate command for errors 400 ms after the last change, with the form's Kind and Input; stale answers are dropped. */
const useTermErrors = (props: SynonymTermsFormComponentProps, json: string, kind: string | undefined, input: string | undefined) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const [errors, setErrors] = useState<readonly RuleError[]>([]);
  const request = useMemo(() => latestRequest(), []);
  const propsRef = useRef(props);
  propsRef.current = props;

  const validate = useMemo(
    () =>
      debounce(async (data: { value: string; kind?: string; input?: string }) => {
        const { latest, value } = await request((abort) =>
          executeCommand<{ errors: RuleError[] }, typeof data>(propsRef.current, "Validate", data, undefined, abort),
        );
        if (latest) {
          setErrors(value?.errors ?? []);
        }
      }, 400),
    [executeCommand, request],
  );

  useEffect(() => {
    if (isEditable(propsRef.current)) {
      validate({ value: json, kind, input });
    }
  }, [json, kind, input, validate]);

  return errors;
};

export const SynonymTermsFormComponent = (props: SynonymTermsFormComponentProps) => {
  const terms = useMemo(() => parseTerms(props.value), [props.value]);
  const json = useMemo(() => serializeTerms(terms), [terms]);
  const values = props.fieldValues as Record<string, unknown>;
  const editable = isEditable(props);
  const errors = useTermErrors(props, json, fieldValue(values, "Kind"), fieldValue(values, "Input"));
  // A query handed over by a Search reports row action becomes the first term; only an empty editor on Create synonym takes it.
  const [handedQuery] = useState(() => (editable && terms.length === 0 ? peek("synonymCreate", window.location.pathname) : null));
  useEffect(() => {
    clear("synonymCreate", window.location.pathname);
    if (handedQuery !== null) {
      props.onChange?.(serializeTerms([handedQuery]));
    }
    // Runs once on mount: the handoff is a one-time prefill.
  }, []);
  const { byIndex, unmatched } = termErrors(errors);
  const { ref, focus } = useFocusTargets();
  const { visible, leaveGroup } = useTouched(props);
  const [text, setText] = useState("");
  const [inputError, setInputError] = useState<string>();
  const addInput = ref("input") as React.RefObject<HTMLInputElement>;

  useEffect(() => {
    if (addInput.current) {
      addInput.current.maxLength = props.maxTermLength;
    }
  }, [addInput, props.maxTermLength]);

  // Errors wait until focus leaves the terms group (or a save failed): adding a term never flags it on its own.
  const update = (next: readonly string[]) => props.onChange?.(serializeTerms(next));

  const commit = () => {
    const result = commitPending(terms, text, props.maxTerms, props.maxTermLength);
    setText(result.text);
    setInputError(result.error);
    if (result.terms.length !== terms.length) {
      update(result.terms);
    }
  };

  const remove = (index: number) => {
    update(removeAt(terms, index));
    const next = focusAfterRemove(index, terms.length);
    focus(next === "input" ? "input" : `remove-${next}`);
  };

  // Tag renders its remove button inside the chip; focus targets find it by the chip's wrapper.
  const chipRef = (index: number) => (element: HTMLLIElement | null) => {
    const target = ref(`remove-${index}`) as React.MutableRefObject<HTMLElement | null>;
    target.current = element?.querySelector<HTMLElement>('button[aria-label^="remove "]') ?? null;
  };

  return (
    <FormItemWrapper
      label={props.label}
      explanationText="Two-way: searching for any term also finds the others. One-way: searching for the input term also finds these terms."
      invalid={props.invalid}
      validationMessage={props.validationMessage}
      editMode={props.editMode}
    >
      <div onBlur={leaveGroup("terms")} aria-describedby={`${props.name}-term-errors`}>
        <Stack spacing={Spacing.M}>
          {terms.length > 0 ? (
            <ul aria-label="Terms" style={{ display: "flex", flexWrap: "wrap", gap: Spacing.S, listStyle: "none", margin: 0, padding: 0 }}>
              {terms.map((term, index) => (
                <li key={`${term}-${index}`} ref={chipRef(index)}>
                  <Tag label={term} readOnly={!editable} onRemoveClick={editable ? () => remove(index) : undefined} />
                </li>
              ))}
            </ul>
          ) : (
            <span style={{ color: "var(--color-text-low-emphasis)" }}>No terms yet.</span>
          )}
          <InlineError id={`${props.name}-term-errors`}>
            {visible("terms")
              ? [
                  // When the field itself is invalid, its own message already says what the unmatched errors say.
                  ...(props.invalid ? [] : unmatched.map((error) => error.message)),
                  ...[...byIndex].map(([index, message]) => `"${terms[index] ?? ""}": ${message}`),
                ].join(" ")
              : ""}
          </InlineError>
          {editable && (
            <Stack spacing={Spacing.S}>
              <Input
                id={`${props.name}-add-term`}
                label="Add a term"
                value={text}
                inputRef={addInput}
                invalid={inputError !== undefined}
                validationMessage={inputError}
                onChange={(event) => {
                  setText(event.target.value);
                  setInputError(undefined);
                }}
                onKeyPress={(event) => {
                  if (event.key === "Enter") {
                    event.preventDefault();
                    commit();
                  }
                }}
                onBlur={commit}
              />
              <div>
                <Button
                  label="Add"
                  icon="xp-plus"
                  color={ButtonColor.Secondary}
                  disabled={!text.trim() || terms.length >= props.maxTerms}
                  onClick={commit}
                />
              </div>
            </Stack>
          )}
        </Stack>
      </div>
    </FormItemWrapper>
  );
};
