import type { RuleError } from "../rules/state";

const letterOrDigit = /[\p{L}\p{Nd}]/u;

const trimWord = (word: string) => {
  let start = 0;
  let end = word.length;
  while (start < end && !letterOrDigit.test(word[start])) {
    start++;
  }
  while (end > start && !letterOrDigit.test(word[end - 1])) {
    end--;
  }
  return word.slice(start, end);
};

/** Mirrors the server's SynonymEngine.MatchKey, so duplicate checks agree (shared vectors in the tests). */
export const matchKey = (text: string) =>
  text
    .trim()
    .toLowerCase()
    .split(/\s+/)
    .map(trimWord)
    .filter((word) => word.length > 0)
    .join(" ");

export const parseTerms = (json: string | undefined): string[] => {
  try {
    const parsed: unknown = JSON.parse(json?.trim() ? json : "[]");
    return Array.isArray(parsed) ? parsed.map((term) => (typeof term === "string" ? term : "")) : [];
  } catch {
    return [];
  }
};

/** No terms is an empty value, so the server's required check (skipped until save) reports it. */
export const serializeTerms = (terms: readonly string[]) => (terms.length === 0 ? "" : JSON.stringify(terms));

/** Adds the typed text as a term, or explains why not. */
export const addTerm = (terms: readonly string[], text: string, maxTerms: number, maxLength: number): { terms: string[] } | { error: string } => {
  const term = text.trim().replace(/\s+/g, " ");
  if (!term) {
    return { error: "Type a term to add." };
  }
  if (!letterOrDigit.test(term)) {
    return { error: "A term needs a letter or digit." };
  }
  if (term.length > maxLength) {
    return { error: `At most ${maxLength} characters.` };
  }
  if (terms.some((existing) => matchKey(existing) === matchKey(term))) {
    return { error: `"${term}" is already a term.` };
  }
  if (terms.length >= maxTerms) {
    return { error: `At most ${maxTerms} terms.` };
  }
  return { terms: [...terms, term] };
};

/**
 * What to do with the Add input's text when the marketer presses Enter, clicks Add or leaves the input:
 * an empty input does nothing; a valid term is added (input cleared); an invalid one stays with its error.
 */
export const commitPending = (terms: readonly string[], text: string, maxTerms: number, maxLength: number) => {
  if (!text.trim()) {
    return { terms: [...terms], text: "", error: undefined };
  }
  const result = addTerm(terms, text, maxTerms, maxLength);
  return "error" in result ? { terms: [...terms], text, error: result.error } : { terms: result.terms, text: "", error: undefined };
};

/** Focus target after removing chip `index` from `count` chips: the next chip, else the previous, else the Add input. */
export const focusAfterRemove = (index: number, count: number): number | "input" => {
  const remaining = count - 1;
  if (remaining === 0) {
    return "input";
  }
  return index < remaining ? index : index - 1;
};

/** Splits server errors into per-term messages (by index) and the rest. */
export const termErrors = (errors: readonly RuleError[]) => {
  const byIndex = new Map<number, string>();
  const unmatched: RuleError[] = [];
  for (const error of errors) {
    const match = /^terms\[(\d+)\]$/.exec(error.path);
    if (match) {
      const index = Number(match[1]);
      byIndex.set(index, byIndex.has(index) ? `${byIndex.get(index)} ${error.message}` : error.message);
    } else {
      unmatched.push(error);
    }
  }
  return { byIndex, unmatched };
};
