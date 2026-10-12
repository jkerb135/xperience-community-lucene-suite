import { test } from "node:test";
import assert from "node:assert/strict";
import { openerOrFallback, returnFocus } from "./focus.ts";

// A fake page: elements are plain objects; "inPanel" marks panel elements. Frames run synchronously on demand.
const page = () => {
  const body = { name: "body", isConnected: true } as unknown as Element;
  const doc = { activeElement: body as Element | null, body };
  const make = (name: string, inPanel = false) => {
    const element = { name, inPanel, isConnected: true, focus: () => void (doc.activeElement = element as unknown as Element) };
    return element;
  };
  let clock = 0;
  const queue: Array<() => void> = [];
  const frame = (ms = 16) => {
    clock += ms;
    const callbacks = queue.splice(0);
    callbacks.forEach((callback) => callback());
    return callbacks.length;
  };
  const run = (target: () => ReturnType<typeof make> | null) =>
    returnFocus(target, (cb) => queue.push(cb), () => clock, doc, (e) => (e as unknown as { inPanel: boolean }).inPanel);
  return { doc, body, make, frame, run };
};

test("focus comes back even when the panel steals it for a few frames", () => {
  const { doc, make, frame, run } = page();
  const opener = make("Filter");
  const panelButton = make("Cancel", true);
  run(() => opener);
  for (let i = 0; i < 3; i++) {
    frame();
    panelButton.focus(); // the SidePanel moves focus after our attempt
  }
  frame();
  assert.equal(doc.activeElement, opener);
});

test("it keeps the opener focused and stops when the budget ends", () => {
  const { doc, make, frame, run } = page();
  const opener = make("Filter");
  run(() => opener);
  frame();
  assert.equal(doc.activeElement, opener);
  let frames = 0;
  while (frame(100) > 0) frames++;
  assert.ok(frames <= 11, `stopped after ${frames} frames`);
});

test("focus the user moved elsewhere is never taken back", () => {
  const { doc, make, frame, run } = page();
  const opener = make("Filter");
  const other = make("Apply rules switch");
  run(() => opener);
  other.focus(); // the user tabbed on before the first frame
  frame();
  frame();
  assert.equal(doc.activeElement, other);
});

test("gives up after the budget when the panel never lets go", () => {
  const { doc, make, frame, run } = page();
  const opener = make("Filter");
  const panelContent = make("content", true);
  run(() => opener);
  for (let i = 0; i < 100 && frame(50) > 0; i++) panelContent.focus();
  assert.equal(frame(), 0);
  assert.equal(doc.activeElement, panelContent);
});

test("a missing or throwing target is ignored", () => {
  const { doc, body, frame, run } = page();
  run(() => null);
  assert.doesNotThrow(() => frame());
  run(() => {
    throw new Error("unmounted");
  });
  assert.doesNotThrow(() => frame());
  assert.equal(doc.activeElement, body);
});

test("the chip that opened the panel wins while it exists, else the Filter button", () => {
  const chip = { isConnected: true };
  const filter = { isConnected: true };
  assert.equal(openerOrFallback(chip, filter), chip);
  assert.equal(openerOrFallback({ isConnected: false }, filter), filter);
  assert.equal(openerOrFallback(null, filter), filter);
});
