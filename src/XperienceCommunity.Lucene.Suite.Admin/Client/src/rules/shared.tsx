import { createRef, useCallback, useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { useFormComponentCommandProvider, type FormComponentProps, type ValidationRule, type ValidationRuleProps } from "@kentico/xperience-admin-base";
import { FormEditMode } from "@kentico/xperience-admin-components";
import { latestRequest } from "../query-tester/state";
import { debounce, type RuleError } from "./state";

/** Props the server sends to the rule JSON components (RuleJsonClientProperties). */
export interface RuleJsonFormComponentProps extends FormComponentProps<string> {
  readonly maxConditions: number;
  readonly maxFacets: number;
  readonly maxContexts: number;
  readonly maxBoosts: number;
}

export const isEditable = (props: FormComponentProps<string>) => (props.editMode ?? FormEditMode.Default) === FormEditMode.Default && !props.disabled;

/** Asks the component's server Validate command for errors 400 ms after the last change; stale answers are dropped. */
export const useServerErrors = (props: FormComponentProps<string>, json: string) => {
  const { executeCommand } = useFormComponentCommandProvider();
  const [errors, setErrors] = useState<readonly RuleError[]>([]);
  const request = useMemo(() => latestRequest(), []);
  const propsRef = useRef(props);
  propsRef.current = props;

  const validate = useMemo(
    () =>
      debounce(async (value: string) => {
        const { latest, value: result } = await request((abort) =>
          executeCommand<{ errors: RuleError[] }, { value: string }>(propsRef.current, "Validate", { value }, undefined, abort),
        );
        if (latest) {
          setErrors(result?.errors ?? []);
        }
      }, 400),
    [executeCommand, request],
  );

  useEffect(() => {
    if (isEditable(propsRef.current)) {
      validate(json);
    }
  }, [json, validate]);

  return errors;
};

/** Stable refs per control key, and a focus request applied after the next render. */
export const useFocusTargets = <T extends HTMLElement = HTMLElement>() => {
  const refs = useRef(new Map<string, RefObject<T>>());
  const [pending, setPending] = useState<string>();
  const ref = useCallback((key: string) => {
    let existing = refs.current.get(key);
    if (!existing) {
      existing = createRef<T>();
      refs.current.set(key, existing);
    }
    return existing;
  }, []);

  useEffect(() => {
    if (pending !== undefined) {
      refs.current.get(pending)?.current?.focus();
      setPending(undefined);
    }
  }, [pending]);

  return { ref, focus: setPending };
};

/**
 * Server errors show on a control only after the user left it, or once a save failed (the field turns invalid),
 * so a freshly added row is not flagged before anything was typed.
 */
export const useTouched = (props: FormComponentProps<string>) => {
  const [touched, setTouched] = useState<ReadonlySet<string>>(new Set());
  const touch = useCallback((path: string) => setTouched((current) => (current.has(path) ? current : new Set(current).add(path))), []);
  const visible = useCallback((path: string) => props.invalid === true || touched.has(path), [props.invalid, touched]);
  return { touch, visible };
};

/** Carries a value through the form without rendering anything (RuleHiddenFormComponent). */
export const RuleHiddenFormComponent = () => null;

/** The server evaluates these rules; the client always passes (ServerValidatedValidationRule). */
export const ServerValidatedValidationRule: ValidationRule<ValidationRuleProps, string> = () => ({ isValid: true, errorMessage: "" });
