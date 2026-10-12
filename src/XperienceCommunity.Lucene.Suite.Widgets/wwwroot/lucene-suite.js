// Lucene Suite analytics client. Implements docs/analytics-client-contract.md; keeps state in memory only.

/**
 * @typedef {object} SuiteClientSettings
 * @property {number} textChangeTimeoutMs
 * @property {number} typoOneEditMinLength
 * @property {number} typoTwoEditMinLength
 * @property {number} deepBackspaceRatio
 * @property {number} [attributionWindowMs]
 * @property {string} [eventsUrl]
 */

/**
 * @typedef {object} SuiteEvent
 * @property {string} searchId
 * @property {number} sequence Sequence of the request whose response listed the item.
 * @property {string} itemGuid
 * @property {number} [position] 1-based absolute position; required for clicks.
 * @property {string} [eventId] Resend the returned id when retrying.
 * @property {string} [query] Clicks only: query of the result list, at most 100 characters. Untrusted; used only for the contact activity.
 * @property {string} [title] Clicks only: text of the clicked result, at most 100 characters. Untrusted; used only for the contact activity.
 */

/** @param {string} q */
export const norm = (q) => q.trim().replace(/\s+/g, " ").toLowerCase();

/** Optimal-string-alignment distance (a transposition is one edit). */
export function osa(/** @type {string} */ a, /** @type {string} */ b) {
  const d = Array.from({ length: a.length + 1 }, (_, i) => [i, ...Array(b.length).fill(0)]);
  for (let j = 1; j <= b.length; j++) d[0][j] = j;
  for (let i = 1; i <= a.length; i++) {
    for (let j = 1; j <= b.length; j++) {
      d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
      if (i > 1 && j > 1 && a[i - 1] === b[j - 2] && a[i - 2] === b[j - 1]) d[i][j] = Math.min(d[i][j], d[i - 2][j - 2] + 1);
    }
  }
  return d[a.length][b.length];
}

/** Whether b is a typo of a; the allowed edits depend on the shorter string's length. */
export function typo(/** @type {string} */ a, /** @type {string} */ b, /** @type {SuiteClientSettings} */ cfg) {
  const shorter = Math.min(a.length, b.length);
  const allowed = shorter >= cfg.typoTwoEditMinLength ? 2 : shorter >= cfg.typoOneEditMinLength ? 1 : 0;
  return allowed > 0 && osa(a, b) <= allowed;
}

/**
 * One tracker per search UI instance. Call beforeSend for every search request you are about to send.
 * @param {SuiteClientSettings} cfg The server's settings (see loadSettings).
 */
export function createSearchTracker(cfg) {
  const s = { searchId: /** @type {string | null} */ (null), seq: 0, lastQ: "", lastSentAt: 0, anchor: "", minLen: 0, submitted: false };
  const continues = (/** @type {string} */ a, /** @type {string} */ b) => a.startsWith(b) || b.startsWith(a) || typo(a, b, cfg);

  return {
    /**
     * @param {string} input Raw query text.
     * @param {boolean} [isSubmit] True only for Enter or the search button.
     * @param {number} [now] Monotonic milliseconds.
     * @returns {{ searchId: string, sequence: number, isSubmit: boolean }}
     */
    beforeSend(input, isSubmit = false, now = performance.now()) {
      const q = norm(input), changed = q !== s.lastQ;
      const mint = s.searchId == null
        || (changed && now - s.lastSentAt > cfg.textChangeTimeoutMs)
        || (s.submitted && (changed || isSubmit))
        || ((q === "") !== (s.lastQ === ""))
        || (changed && !continues(s.lastQ, q))
        || (changed && (q.startsWith(s.lastQ) || typo(s.lastQ, q, cfg))
            && s.minLen < cfg.deepBackspaceRatio * s.anchor.length && !s.anchor.startsWith(q));
      if (mint) Object.assign(s, { searchId: newGuid(), seq: 0, anchor: q, minLen: q.length, submitted: false });
      if (q.length < s.lastQ.length && s.lastQ.startsWith(q)) s.minLen = Math.min(s.minLen, q.length);
      if (!s.anchor.startsWith(q)) Object.assign(s, { anchor: q, minLen: q.length });
      s.seq++; s.lastQ = q; s.lastSentAt = now; s.submitted ||= isSubmit;
      return { searchId: /** @type {string} */ (s.searchId), sequence: s.seq, isSubmit };
    },
  };
}

/** Random RFC 4122 version 4 GUID. */
export function newGuid() {
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = Array.from(b, (x) => x.toString(16).padStart(2, "0")).join("");
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

/**
 * @param {string} url
 * @param {SuiteEvent & { type: string }} payload
 */
function send(url, payload) {
  const event = { ...payload, eventId: payload.eventId ?? newGuid() };
  const body = JSON.stringify(event);
  // sendBeacon survives navigation; it returns false when the browser refuses to queue the body.
  if (!globalThis.navigator?.sendBeacon?.(url, body)) {
    fetch(url, { method: "POST", body, keepalive: true, headers: { "Content-Type": "application/json" } }).catch(() => {});
  }
  return event.eventId;
}

/** Sends a click on a search result; returns the eventId. */
export const sendClick = (/** @type {string} */ eventsUrl, /** @type {SuiteEvent} */ e) => send(eventsUrl, { ...e, type: "click" });

/** Sends a conversion attributed to a search result; returns the eventId. */
export const sendConversion = (/** @type {string} */ eventsUrl, /** @type {SuiteEvent} */ e) => send(eventsUrl, { ...e, type: "conversion" });

/**
 * Fetches the server's settings, e.g. from "/lucenesuite/events/settings".
 * @param {string} settingsUrl
 * @returns {Promise<SuiteClientSettings>}
 */
export async function loadSettings(settingsUrl) {
  const response = await fetch(settingsUrl);
  if (!response.ok) throw new Error(`Lucene Suite settings: HTTP ${response.status}`);
  return response.json();
}

/** The sr= link parameter: searchId without dashes, sequence and position. */
export const formatResultReference = (/** @type {string} */ searchId, /** @type {number} */ sequence, /** @type {number} */ position) =>
  `${searchId.replace(/-/g, "").toLowerCase()}.${sequence}.${position}`;

/**
 * Parses an sr= value with the server's rules; null when invalid.
 * @param {string | null | undefined} value
 * @returns {{ searchId: string, sequence: number, position: number } | null}
 */
export function parseResultReference(value) {
  const m = value != null && value.length <= 64 ? /^([0-9a-f]{32})\.(\d+)\.(\d+)$/i.exec(value) : null;
  if (!m || /^0+$/.test(m[1])) return null;
  const sequence = Number(m[2]), position = Number(m[3]);
  if (sequence < 1 || sequence > 2147483647 || position < 1 || position > 1000) return null;
  const h = m[1].toLowerCase();
  return { searchId: `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`, sequence, position };
}
