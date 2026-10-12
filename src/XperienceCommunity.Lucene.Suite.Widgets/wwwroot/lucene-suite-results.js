// Click beacons for the server-rendered Lucene Suite results: reads the data attributes the results view renders.
import { sendClick } from "./lucene-suite.js";

const guid = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
const int = (/** @type {string | null} */ value, /** @type {number} */ min, /** @type {number} */ max) =>
  value != null && /^\d{1,10}$/.test(value) && +value >= min && +value <= max ? +value : null;

/**
 * Beacon text: whitespace collapsed, control characters and lone surrogates removed, cut to 100 UTF-16 units without
 * splitting a pair, so a click carrying both texts stays under the 1 KB body limit.
 * @param {string | null | undefined} value
 */
export function clip(value) {
  const text = (value ?? "").replace(/\s+/g, " ").trim()
    .replace(/[\u0000-\u001f\u007f-\u009f]|[\ud800-\udbff](?![\udc00-\udfff])|(?<![\ud800-\udbff])[\udc00-\udfff]/g, "")
    .slice(0, 100);
  return /[\ud800-\udbff]$/.test(text) ? text.slice(0, -1) : text;
}

/**
 * The click event of a tracked result link, or null when its attributes are missing or invalid.
 * Adds the list's query and the link text when present; the server uses them only for the contact activity.
 * @param {Element} link
 * @returns {{ searchId: string, sequence: number, itemGuid: string, position: number, query?: string, title?: string } | null}
 */
export function clickPayload(link) {
  const list = link.closest("[data-lucene-suite-results]");
  const searchId = list?.getAttribute("data-lucene-suite-search") ?? "";
  const itemGuid = link.getAttribute("data-lucene-suite-item") ?? "";
  const sequence = int(list?.getAttribute("data-lucene-suite-sequence") ?? null, 1, 2147483647);
  const position = int(link.getAttribute("data-lucene-suite-position"), 1, 1000);
  const query = clip(list?.getAttribute("data-lucene-suite-query"));
  const title = clip(link.textContent);
  return guid.test(searchId) && guid.test(itemGuid) && sequence && position
    ? { searchId, sequence, itemGuid, position, ...(query && { query }), ...(title && { title }) }
    : null;
}

/** @param {MouseEvent} event */
function onActivate(event) {
  if (event.type === "auxclick" && event.button !== 1) return;
  const link = event.target instanceof Element ? event.target.closest("a[data-lucene-suite-item]") : null;
  const eventsUrl = link?.closest("[data-lucene-suite-results]")?.getAttribute("data-lucene-suite-events");
  const payload = link && eventsUrl ? clickPayload(link) : null;
  if (payload) sendClick(/** @type {string} */ (eventsUrl), payload);
}

if (typeof document !== "undefined") {
  document.addEventListener("click", onActivate);
  document.addEventListener("auxclick", onActivate);
}
