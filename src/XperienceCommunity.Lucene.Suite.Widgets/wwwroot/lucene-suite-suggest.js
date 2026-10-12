// Lucene Suite search suggestions: an ARIA 1.2 combobox over the suggest endpoint. Suggestions are untrusted text: textContent only.

/** Longest input the endpoint accepts (ISuiteSuggestions.MaxInputLength). */
export const MAX_INPUT_LENGTH = 100;
const CACHE_SIZE = 50;
const BACKOFF_MS = 30000;

/**
 * Fetches suggestions for q from the suggest endpoint.
 * @param {string} url Endpoint URL, e.g. "/lucenesuite/suggest".
 * @param {string} index Index name (must be in SuggestionIndexes).
 * @param {string} q What the visitor typed.
 * @param {{ signal?: AbortSignal }} [options]
 * @returns {Promise<string[]>}
 * @throws {Error & { status?: number }} On a non-2xx answer (status set) or a network error / abort.
 */
export async function fetchSuggestions(url, index, q, { signal } = {}) {
  const sep = url.includes("?") ? "&" : "?";
  const response = await fetch(`${url}${sep}index=${encodeURIComponent(index)}&q=${encodeURIComponent(q)}`, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw Object.assign(new Error(`Suggest request failed: ${response.status}`), { status: response.status });
  const body = await response.json();
  return Array.isArray(body) ? body.filter((s) => typeof s === "string") : [];
}

/**
 * @typedef {{ type: "none" } | { type: "move" } | { type: "close" } | { type: "clear" } | { type: "submit" } | { type: "select", value: string }} SuggestAction
 * "none": let the browser handle the key; "submit": Enter without an active option, let the form submit as typed.
 */

/**
 * The keyboard/selection state of one combobox, without DOM.
 * @returns {{ readonly items: string[], readonly active: number, readonly open: boolean,
 *   setItems(items: string[]): void, close(): void, key(key: string): SuggestAction, pick(index: number): SuggestAction }}
 */
export function createSuggestState() {
  /** @type {string[]} */
  let items = [];
  let active = -1;
  let open = false;
  const close = () => { open = false; active = -1; };
  /** @param {number} index @returns {SuggestAction} */
  const pick = (index) => {
    const value = items[index];
    if (value === undefined) return { type: "none" };
    close();
    return { type: "select", value };
  };
  return {
    get items() { return items; },
    get active() { return active; },
    get open() { return open; },
    setItems(next) { items = next; active = -1; open = next.length > 0; },
    close,
    pick,
    key(key) {
      const n = items.length;
      switch (key) {
        case "ArrowDown":
        case "ArrowUp": {
          if (n === 0) return { type: "none" };
          const down = key === "ArrowDown";
          active = !open || active < 0 ? (down ? 0 : n - 1) : (active + (down ? 1 : n - 1)) % n;
          open = true;
          return { type: "move" };
        }
        case "Enter":
          return open && active >= 0 ? pick(active) : { type: "submit" };
        case "Escape":
          if (open) { close(); return { type: "close" }; }
          return { type: "clear" };
        case "Tab":
          close();
          return { type: "none" };
        default:
          return { type: "none" };
      }
    },
  };
}

/** @param {string} q */
const normalize = (q) => q.trim().replace(/\s+/g, " ");
let warned = false;
let ids = 0;

/**
 * Appends text to parent with the first word-start match of q wrapped in a mark element; text nodes only.
 * @param {Element} parent @param {string} text @param {string} q
 */
function appendHighlighted(parent, text, q) {
  const doc = /** @type {Document} */ (parent.ownerDocument);
  const lower = text.toLowerCase();
  const needle = q.toLowerCase();
  // Lower-casing can change the length of some characters; highlight only when offsets still line up.
  let at = lower.length === text.length && needle.length > 0 ? (lower.startsWith(needle) ? 0 : lower.indexOf(" " + needle)) : -1;
  if (at > 0) at += 1;
  if (at < 0) {
    parent.appendChild(doc.createTextNode(text));
    return;
  }
  if (at > 0) parent.appendChild(doc.createTextNode(text.slice(0, at)));
  const mark = doc.createElement("mark");
  mark.setAttribute("class", "lucene-suite-suggest__match");
  mark.textContent = text.slice(at, at + needle.length);
  parent.appendChild(mark);
  if (at + needle.length < text.length) parent.appendChild(doc.createTextNode(text.slice(at + needle.length)));
}

/**
 * @typedef {object} EnhanceOptions
 * @property {number} [delayMs] Quiet time before a request (default 150).
 * @property {number} [minLength] Shortest input that requests (default: data-lucene-suite-min-length, else 2).
 * @property {string} [url] Endpoint (default: data-lucene-suite-suggest).
 * @property {string} [index] Index (default: data-lucene-suite-index).
 * @property {typeof fetchSuggestions} [fetcher]
 * @property {() => number} [now]
 * @property {(fn: () => void, ms: number) => unknown} [setTimeout]
 * @property {(handle: unknown) => void} [clearTimeout]
 */

/**
 * Turns a search form into an ARIA 1.2 combobox. The form must contain the search input
 * (input[type="search"] or input[name="q"]), an empty [role="listbox"] and a [role="status"] element.
 * @param {HTMLFormElement} form
 * @param {EnhanceOptions} [options]
 * @returns {boolean} Whether the form was enhanced (false when the markup, URL or index is missing).
 */
export function enhanceSearchBox(form, options = {}) {
  const input = /** @type {HTMLInputElement | null} */ (form.querySelector('input[type="search"], input[name="q"]'));
  const listbox = /** @type {HTMLElement | null} */ (form.querySelector('[role="listbox"]'));
  const status = /** @type {HTMLElement | null} */ (form.querySelector('[role="status"]'));
  const url = options.url ?? form.getAttribute("data-lucene-suite-suggest");
  const index = options.index ?? form.getAttribute("data-lucene-suite-index");
  if (!input || !listbox || !status || !url || !index) return false;

  const doc = /** @type {Document} */ (form.ownerDocument);
  const delayMs = options.delayMs ?? 150;
  const attrMin = Number.parseInt(form.getAttribute("data-lucene-suite-min-length") ?? "", 10);
  const minLength = options.minLength ?? (attrMin > 0 ? attrMin : 2);
  const fetcher = options.fetcher ?? fetchSuggestions;
  const now = options.now ?? (() => Date.now());
  const schedule = options.setTimeout ?? ((fn, ms) => setTimeout(fn, ms));
  const unschedule = options.clearTimeout ?? ((handle) => clearTimeout(/** @type {number} */ (handle)));

  const state = createSuggestState();
  /** @type {Map<string, string[]>} */
  const cache = new Map();
  /** @type {unknown} */
  let timer = null;
  /** @type {AbortController | null} */
  let controller = null;
  let seq = 0;
  let backoffUntil = 0;
  let composing = false;

  if (!listbox.id) listbox.id = (input.id || `lucene-suite-suggest-${++ids}`) + "-listbox";
  input.setAttribute("role", "combobox");
  input.setAttribute("aria-autocomplete", "list");
  input.setAttribute("aria-expanded", "false");
  input.setAttribute("aria-controls", listbox.id);
  listbox.hidden = true;

  /** Cancels the pending and in-flight request; late answers are ignored. */
  function cancel() {
    if (timer !== null) unschedule(timer);
    timer = null;
    controller?.abort();
    controller = null;
    seq++;
  }

  function sync() {
    const children = listbox.children;
    for (let i = 0; i < children.length; i++) children[i].setAttribute("aria-selected", String(i === state.active));
    const active = state.active >= 0 ? children[state.active] : null;
    if (active) {
      input.setAttribute("aria-activedescendant", active.id);
      active.scrollIntoView?.({ block: "nearest" });
    } else {
      input.removeAttribute("aria-activedescendant");
    }
    listbox.hidden = !state.open;
    input.setAttribute("aria-expanded", String(state.open));
  }

  /** @param {string[]} items @param {string} q */
  function render(items, q) {
    state.setItems(items);
    listbox.textContent = "";
    items.forEach((text, i) => {
      const option = doc.createElement("li");
      option.id = `${listbox.id}-${i}`;
      option.setAttribute("role", "option");
      option.setAttribute("class", "lucene-suite-suggest__option");
      appendHighlighted(option, text, q);
      listbox.appendChild(option);
    });
    status.textContent = items.length === 0 ? "" : `${items.length} suggestion${items.length === 1 ? "" : "s"} available`;
    sync();
  }

  function hide() {
    state.close();
    sync();
  }

  function update() {
    const q = normalize(input.value);
    const key = q.toLowerCase();
    cancel();
    if (q.length < minLength || q.length > MAX_INPUT_LENGTH || now() < backoffUntil) {
      render([], q);
      return;
    }
    const cached = cache.get(key);
    if (cached) {
      render(cached, q);
      return;
    }
    const mine = seq;
    timer = schedule(() => {
      timer = null;
      controller = typeof AbortController === "undefined" ? null : new AbortController();
      fetcher(url, index, q, { signal: controller?.signal }).then(
        (items) => {
          if (mine !== seq) return;
          if (cache.size >= CACHE_SIZE) cache.delete(/** @type {string} */ (cache.keys().next().value));
          cache.set(key, items);
          render(items, q);
        },
        (error) => {
          if (mine !== seq) return;
          if (error?.status === 429) backoffUntil = now() + BACKOFF_MS;
          if (!warned && error?.name !== "AbortError") {
            warned = true;
            console.warn("Lucene Suite suggestions unavailable:", error);
          }
          render([], q);
        });
    }, delayMs);
  }

  /** @param {string} value */
  function choose(value) {
    cancel();
    input.value = value;
    hide();
    if (typeof form.requestSubmit === "function") form.requestSubmit();
    else form.submit();
  }

  input.addEventListener("compositionstart", () => { composing = true; });
  input.addEventListener("compositionend", () => { composing = false; update(); });
  input.addEventListener("input", (/** @type {InputEvent} */ event) => {
    if (!composing && !event.isComposing) update();
  });
  input.addEventListener("keydown", (/** @type {KeyboardEvent} */ event) => {
    if (event.isComposing || composing) return;
    const action = state.key(event.key);
    switch (action.type) {
      case "move":
        event.preventDefault();
        sync();
        break;
      case "select":
        event.preventDefault();
        choose(action.value);
        break;
      case "close":
        event.preventDefault();
        cancel();
        sync();
        break;
      case "clear":
        if (input.value === "") break;
        event.preventDefault();
        cancel();
        input.value = "";
        render([], "");
        break;
      case "submit":
        cancel();
        hide();
        break;
      default:
        if (event.key === "Tab") sync();
    }
  });
  input.addEventListener("blur", () => { cancel(); hide(); });
  listbox.addEventListener("pointerdown", (/** @type {PointerEvent} */ event) => {
    const target = /** @type {Element | null} */ (event.target);
    const option = target?.closest?.('[role="option"]');
    if (!option) return;
    // Keeps focus in the input, so blur does not close the list before the choice is made.
    event.preventDefault();
    const action = state.pick(Array.prototype.indexOf.call(listbox.children, option));
    if (action.type === "select") choose(action.value);
  });
  return true;
}

/** @type {WeakSet<Element>} */
const enhanced = new WeakSet();

/** Enhances every form[data-lucene-suite-suggest] on the page once; a no-op without a DOM. */
export function autoInit() {
  if (typeof document === "undefined") return;
  const run = () => {
    for (const form of document.querySelectorAll("form[data-lucene-suite-suggest]")) {
      if (!enhanced.has(form) && enhanceSearchBox(/** @type {HTMLFormElement} */ (form))) enhanced.add(form);
    }
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", run, { once: true });
  else run();
}

if (typeof document !== "undefined") autoInit();
