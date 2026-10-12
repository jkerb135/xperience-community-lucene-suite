import assert from "node:assert/strict";
import test from "node:test";

import { createSuggestState, enhanceSearchBox, fetchSuggestions } from "../wwwroot/lucene-suite-suggest.js";

/** Minimal DOM: only the members the module uses. Records every innerHTML write. */
class FakeDocument {
  innerHTMLWrites = [];
  createElement(tag) { return new FakeElement(tag, this); }
  createTextNode(text) { return { nodeType: 3, textContent: text, parentNode: null }; }
}

class FakeElement {
  constructor(tag, doc) {
    this.tagName = tag.toUpperCase();
    this.ownerDocument = doc;
    this.attrs = new Map();
    this.children = [];
    this.childNodes = [];
    this.listeners = {};
    this.hidden = false;
    this.value = "";
    this.id = "";
    this.parentNode = null;
  }
  setAttribute(name, value) { if (name === "id") this.id = String(value); else this.attrs.set(name, String(value)); }
  getAttribute(name) { return name === "id" ? this.id || null : this.attrs.get(name) ?? null; }
  removeAttribute(name) { this.attrs.delete(name); }
  appendChild(node) {
    node.parentNode = this;
    this.childNodes.push(node);
    if (node instanceof FakeElement) this.children.push(node);
    return node;
  }
  get textContent() { return this.childNodes.map((n) => n.textContent).join(""); }
  set textContent(value) {
    this.children = [];
    this.childNodes = value === "" ? [] : [this.ownerDocument.createTextNode(String(value))];
  }
  set innerHTML(value) { this.ownerDocument.innerHTMLWrites.push(value); }
  addEventListener(type, fn) { (this.listeners[type] ??= []).push(fn); }
  closest(selector) {
    const role = /^\[role="(\w+)"\]$/.exec(selector)?.[1];
    for (let el = this; el instanceof FakeElement; el = el.parentNode) if (el.getAttribute("role") === role) return el;
    return null;
  }
  fire(type, props = {}) {
    const event = { type, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...props };
    for (const fn of this.listeners[type] ?? []) fn(event);
    return event;
  }
}

/** A server-rendered enhanced search box plus controllable timers and fetcher. */
function setup({ delayMs = 150, minLength } = {}) {
  const doc = new FakeDocument();
  const form = new FakeElement("form", doc);
  form.attrs.set("data-lucene-suite-suggest", "/lucenesuite/suggest");
  form.attrs.set("data-lucene-suite-index", "Shop");
  form.attrs.set("data-lucene-suite-min-length", "2");
  form.submits = 0;
  form.requestSubmit = () => form.submits++;
  const input = new FakeElement("input", doc);
  input.id = "lucene-suite-q-1";
  const listbox = new FakeElement("ul", doc);
  listbox.id = "lucene-suite-q-1-listbox";
  listbox.setAttribute("role", "listbox");
  listbox.hidden = true;
  const status = new FakeElement("div", doc);
  status.setAttribute("role", "status");
  form.querySelector = (selector) =>
    selector.includes("input") ? input : selector === '[role="listbox"]' ? listbox : selector === '[role="status"]' ? status : null;

  let clock = 0;
  const timers = new Map();
  let nextTimer = 1;
  const requests = [];
  const fetcher = (url, index, q, { signal }) => {
    let resolve, reject;
    const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
    requests.push({ url, index, q, signal, resolve, reject });
    return promise;
  };
  const enhanced = enhanceSearchBox(form, {
    delayMs,
    minLength,
    fetcher,
    now: () => clock,
    setTimeout: (fn, ms) => { const id = nextTimer++; timers.set(id, { fn, at: clock + ms }); return id; },
    clearTimeout: (id) => timers.delete(id),
  });
  const advance = (ms) => {
    clock += ms;
    for (const [id, t] of [...timers]) if (t.at <= clock) { timers.delete(id); t.fn(); }
  };
  const type = (value) => { input.value = value; input.fire("input"); };
  const key = (k) => input.fire("keydown", { key: k });
  const flush = () => new Promise((r) => setImmediate(r));
  const texts = () => listbox.children.map((li) => li.textContent);
  return { doc, form, input, listbox, status, requests, enhanced, advance, type, key, flush, texts };
}

/** Types q, lets the debounce pass and answers the request with items. */
async function show(box, q, items) {
  box.type(q);
  box.advance(150);
  box.requests.at(-1).resolve(items);
  await box.flush();
}

test("state: arrows open and wrap, Enter picks the active option or submits, Escape closes then clears", () => {
  const s = createSuggestState();
  assert.equal(s.key("ArrowDown").type, "none");
  s.setItems(["a", "b", "c"]);
  assert.equal(s.open, true);
  assert.equal(s.active, -1);
  assert.deepEqual(s.key("Enter"), { type: "submit" });
  s.key("ArrowDown");
  s.key("ArrowDown");
  s.key("ArrowDown");
  assert.equal(s.active, 2);
  s.key("ArrowDown");
  assert.equal(s.active, 0);
  s.key("ArrowUp");
  assert.equal(s.active, 2);
  assert.deepEqual(s.key("Enter"), { type: "select", value: "c" });
  assert.equal(s.open, false);
  s.key("ArrowUp");
  assert.equal(s.open, true);
  assert.equal(s.active, 2);
  assert.deepEqual(s.key("Escape"), { type: "close" });
  assert.equal(s.open, false);
  assert.deepEqual(s.key("Escape"), { type: "clear" });
  s.key("ArrowDown");
  s.key("Tab");
  assert.equal(s.open, false);
});

test("enhance sets the combobox attributes; the status announces the count", async () => {
  const box = setup();
  assert.equal(box.enhanced, true);
  assert.equal(box.input.getAttribute("role"), "combobox");
  assert.equal(box.input.getAttribute("aria-autocomplete"), "list");
  assert.equal(box.input.getAttribute("aria-expanded"), "false");
  assert.equal(box.input.getAttribute("aria-controls"), "lucene-suite-q-1-listbox");

  await show(box, "co", ["coffee", "cocoa"]);
  assert.equal(box.input.getAttribute("aria-expanded"), "true");
  assert.equal(box.listbox.hidden, false);
  assert.equal(box.status.textContent, "2 suggestions available");
  const [first, second] = box.listbox.children;
  assert.equal(first.getAttribute("role"), "option");
  assert.equal(first.id, "lucene-suite-q-1-listbox-0");
  assert.equal(first.getAttribute("aria-selected"), "false");
  assert.equal(box.input.getAttribute("aria-activedescendant"), null);

  assert.equal(box.key("ArrowDown").defaultPrevented, true);
  assert.equal(box.key("ArrowDown").defaultPrevented, true);
  assert.equal(second.getAttribute("aria-selected"), "true");
  assert.equal(box.input.getAttribute("aria-activedescendant"), "lucene-suite-q-1-listbox-1");

  await show(box, "cof", ["coffee"]);
  assert.equal(box.status.textContent, "1 suggestion available");
  await show(box, "cofx", []);
  assert.equal(box.status.textContent, "");
  assert.equal(box.listbox.hidden, true);
});

test("Enter on an active option fills the input and submits; Enter without one submits as typed", async () => {
  const box = setup();
  await show(box, "co", ["coffee", "cocoa"]);
  assert.equal(box.key("Enter").defaultPrevented, false);
  assert.equal(box.form.submits, 0);

  await show(box, "coc", ["coffee", "cocoa"]);
  box.key("ArrowUp");
  assert.equal(box.key("Enter").defaultPrevented, true);
  assert.equal(box.input.value, "cocoa");
  assert.equal(box.form.submits, 1);
  assert.equal(box.input.getAttribute("aria-expanded"), "false");
});

test("Escape closes the list, a second Escape clears the input; blur closes; pointerdown selects", async () => {
  const box = setup();
  await show(box, "co", ["coffee", "cocoa"]);
  box.key("Escape");
  assert.equal(box.listbox.hidden, true);
  assert.equal(box.input.value, "co");
  box.key("Escape");
  assert.equal(box.input.value, "");

  await show(box, "co", ["coffee", "cocoa"]);
  box.input.fire("blur");
  assert.equal(box.listbox.hidden, true);

  await show(box, "coc", ["coffee", "cocoa"]);
  const mark = box.listbox.children[1].children[0];
  const down = box.listbox.fire("pointerdown", { target: mark });
  assert.equal(down.defaultPrevented, true);
  assert.equal(box.input.value, "cocoa");
  assert.equal(box.form.submits, 1);
});

test("rapid input collapses into one request after 150 ms of quiet; short input does not request", () => {
  const box = setup();
  box.type("c");
  box.advance(500);
  assert.equal(box.requests.length, 0);
  box.type("co");
  box.advance(100);
  box.type("cof");
  box.advance(100);
  box.type("coff");
  assert.equal(box.requests.length, 0);
  box.advance(150);
  assert.equal(box.requests.length, 1);
  assert.deepEqual([box.requests[0].url, box.requests[0].index, box.requests[0].q], ["/lucenesuite/suggest", "Shop", "coff"]);
});

test("composition input is ignored until compositionend", () => {
  const box = setup();
  box.input.fire("compositionstart");
  box.input.value = "ko";
  box.input.fire("input", { isComposing: true });
  box.advance(200);
  assert.equal(box.requests.length, 0);
  box.input.fire("compositionend");
  box.advance(150);
  assert.equal(box.requests.length, 1);
});

test("a stale response is ignored after a newer request; the older one is aborted", async () => {
  const box = setup();
  box.type("co");
  box.advance(150);
  box.type("cof");
  box.advance(150);
  assert.equal(box.requests[0].signal.aborted, true);
  box.requests[1].resolve(["coffee"]);
  box.requests[0].resolve(["cocoa", "cola"]);
  await box.flush();
  assert.deepEqual(box.texts(), ["coffee"]);
});

test("a cached input is shown without a request", async () => {
  const box = setup();
  await show(box, "co", ["coffee"]);
  await show(box, "cof", ["coffee"]);
  box.type("CO ");
  assert.equal(box.requests.length, 2);
  assert.deepEqual(box.texts(), ["coffee"]);
  assert.equal(box.listbox.hidden, false);
});

test("an error hides the list silently and warns once", async () => {
  const box = setup();
  const warn = console.warn;
  let warnings = 0;
  console.warn = () => warnings++;
  try {
    await show(box, "co", ["coffee"]);
    box.type("cox");
    box.advance(150);
    box.requests.at(-1).reject(Object.assign(new Error("404"), { status: 404 }));
    await box.flush();
    assert.equal(box.listbox.hidden, true);
    assert.equal(box.input.getAttribute("aria-expanded"), "false");
    box.type("coy");
    box.advance(150);
    box.requests.at(-1).reject(new TypeError("network"));
    await box.flush();
    assert.equal(warnings, 1);
  } finally {
    console.warn = warn;
  }
});

test("after a 429 no request is made for 30 s, then requests resume", async () => {
  const box = setup();
  const warn = console.warn;
  console.warn = () => {};
  try {
    box.type("co");
    box.advance(150);
    box.requests[0].reject(Object.assign(new Error("429"), { status: 429 }));
    await box.flush();
    box.type("cof");
    box.advance(150);
    box.advance(29000);
    box.type("coff");
    box.advance(150);
    assert.equal(box.requests.length, 1);
    box.advance(1000);
    box.type("coffe");
    box.advance(150);
    assert.equal(box.requests.length, 2);
  } finally {
    console.warn = warn;
  }
});

test("input over 100 characters never requests", () => {
  const box = setup();
  box.type("a".repeat(101));
  box.advance(1000);
  assert.equal(box.requests.length, 0);
  box.type("a".repeat(100));
  box.advance(150);
  assert.equal(box.requests.length, 1);
});

test("suggestions render as text: HTML-looking text stays literal, the match is a separate mark", async () => {
  const box = setup();
  const evil = "<img src=x onerror=alert(1)>";
  await show(box, "<im", [evil, "best <img"]);
  const [first, second] = box.listbox.children;
  assert.equal(first.textContent, evil);
  assert.equal(first.children.length, 1);
  assert.equal(first.children[0].tagName, "MARK");
  assert.equal(first.children[0].textContent, "<im");
  assert.equal(first.childNodes[1].textContent, "g src=x onerror=alert(1)>");
  assert.deepEqual(second.childNodes.map((n) => n.textContent), ["best ", "<im", "g"]);
  assert.deepEqual(box.doc.innerHTMLWrites, []);
});

test("enhance is a no-op without the markup or endpoint", () => {
  const doc = new FakeDocument();
  const form = new FakeElement("form", doc);
  form.querySelector = () => null;
  assert.equal(enhanceSearchBox(form), false);
});

test("fetchSuggestions encodes the query, throws with the status on non-2xx", async () => {
  const fetch = globalThis.fetch;
  const calls = [];
  try {
    globalThis.fetch = async (url) => { calls.push(url); return { ok: true, json: async () => ["a", 1, "b"] }; };
    assert.deepEqual(await fetchSuggestions("/s", "My Index", "a&b c"), ["a", "b"]);
    assert.equal(calls[0], "/s?index=My%20Index&q=a%26b%20c");
    globalThis.fetch = async () => ({ ok: false, status: 429 });
    await assert.rejects(fetchSuggestions("/s?x=1", "i", "q"), (e) => e.status === 429);
  } finally {
    globalThis.fetch = fetch;
  }
});
