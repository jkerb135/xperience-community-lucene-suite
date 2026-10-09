# Analytics client contract

How a custom search UI (or your own endpoint) feeds Lucene Suite analytics. The browser decides where one logical search ends and the next begins, by minting a `searchId`. The server stores one raw row per answered request and keeps no state between requests. The rollup then merges rows into logical searches. This works on any web farm, and nothing about the visitor is stored.

The executable form of these rules is [`analytics-test-vectors.json`](analytics-test-vectors.json). The Suite tests, the rollup and the Widgets JS client all check against it, so if this page and the file disagree, the file wins.

## 1. Search requests

Send these on every `SuiteSearchRequest`:

| Field | Value |
|---|---|
| `SearchId` | The current `searchId` (see §2). Leave it null only for server-rendered or no-JS requests; the server then mints one and returns it in `SuiteSearchResponse.SearchId`. Adopt it and continue at `Sequence = 2`. |
| `Sequence` | 1-based counter within the `searchId`, incremented on every request you send. |
| `IsSubmit` | `true` only for an explicit submit (Enter or the search button). Typing, paging, filtering and sorting send `false`. |
| `TrackAnalytics` | `false` for anything that is not a visitor's search: count probes ("Show 12 results"), previews, admin tools, health checks. |

Only the results instance logs. As-you-type dropdowns that preview results should send `TrackAnalytics = false`; otherwise a header box plus the results page count the same search twice.

## 2. When to mint a new `searchId`

Keep the state per search-UI instance in **page memory only**: no cookie, no `localStorage`. Normalize with `norm(q)` = trim, collapse whitespace, lower-case.

Before sending a debounced search with `q = norm(input)`, mint a new id (16 random bytes from `crypto.getRandomValues`, formatted as a GUID) and reset `seq = 0` when **any** of these holds:

1. There is no `searchId` yet (new page, new tab, reload).
2. `q ≠ lastQ` and more than **30 s** have passed since the last request. A page, filter or sort change with an unchanged `q` never times out.
3. The search was submitted and (`q ≠ lastQ` or this request is itself a submit). After Enter, paging and filtering keep the id; new text or a re-submit starts a new one.
4. `q` is empty and `lastQ` is not, or the other way round. An empty (match-all) query never continues anything and is never continued.
5. `q ≠ lastQ` and `q` is not a continuation of `lastQ`. A continuation is either string being a prefix of the other, or a typo:
   - under 4 characters (the shorter string): no typo tolerance (`tv` → `pc` is two searches);
   - 4–7 characters: at most 1 edit (`ipad` → `ipod` continues, `ipad` → `imac` does not);
   - 8 or more: at most 2 edits.
   Edits are optimal-string-alignment distance, so a transposition counts as one edit (`grinder` → `grindre`).
6. **Deep backspace:** `q` extends `lastQ` or is a typo of it, the visitor had backspaced to less than half of the text they started from (`2 · minLen < len(anchor)`), and `q` is not a prefix of that text. For example, `laptop` → … → `l` → `la` → `lam` splits at `lam`.

```js
function beforeSend(input, isSubmit, now) {
  const q = norm(input), changed = q !== s.lastQ;
  const mint = s.searchId == null
    || (changed && now - s.lastSentAt > 30_000)
    || (s.submitted && (changed || isSubmit))
    || ((q === "") !== (s.lastQ === ""))
    || (changed && !continues(s.lastQ, q))
    || (changed && (q.startsWith(s.lastQ) || typo(s.lastQ, q))
        && 2 * s.minLen < s.anchor.length && !s.anchor.startsWith(q));
  if (mint) Object.assign(s, { searchId: newGuid(), seq: 0, anchor: q, minLen: q.length, submitted: false });
  if (q.length < s.lastQ.length && s.lastQ.startsWith(q)) s.minLen = Math.min(s.minLen, q.length);
  if (!s.anchor.startsWith(q)) Object.assign(s, { anchor: q, minLen: q.length });
  s.seq++; s.lastQ = q; s.lastSentAt = now; s.submitted ||= isSubmit;
  return { searchId: s.searchId, sequence: s.seq, isSubmit };
}
```

Use a monotonic clock (`performance.now()`) for `now`.

## 3. Clicks and conversions

Call `ISearchInteractionLogger` from your endpoint or controller:

```csharp
logger.LogClick(searchId, sequence, itemGuid, position, eventId);       // the visitor opened a result
logger.LogConversion(searchId, sequence, itemGuid, position, eventId);  // a goal attributed to a result; position optional
```

- `sequence` is the `Sequence` of the request whose **response** listed the item, not the latest one sent.
- `position` is 1-based and absolute: `(page − 1) · pageSize + indexOnPage`, from 1 to 1000.
- `eventId` is a random GUID you mint **once per event** and resend unchanged on retry, so a retried beacon is counted once. If you leave it null, the server mints one, and a retry then counts twice.
- Invalid arguments (empty GUIDs, `sequence < 1`, position out of range) throw `ArgumentException` synchronously. Return HTTP 400 for them. Storage problems are never thrown.
- The logger skips bots, prefetch/prerender requests and disabled analytics on its own. It does **not** check whether the search or position really existed. The rollup checks that, and it only counts an interaction that arrives within **1 hour** of the response it refers to.
- Send clicks with `navigator.sendBeacon`, with a `fetch(..., { keepalive: true })` fallback, so that navigating away does not cancel them.
- **Rate limiting is yours.** A custom endpoint must apply its own rate limit (for example ASP.NET Core's rate limiter, keyed in memory by IP, which is never stored). The optional Widgets package adds one to its endpoint.

### Conversions on a later page

A conversion usually happens after the visitor has left the results page (a product page, the cart, a form). The Suite adds no device storage, so the ids have to travel with the visitor:

- **On result links:** put the ids in the URL, for example `/products/grinder?sr=<searchId>.<sequence>.<position>`. The parameter holds only random ids, nothing about the visitor. On the target page, parse and validate it, and pass it to `LogConversion` when the goal completes. Validate it like any other input; tampered values only produce an unattributed or rejected event.
- **Or in server-side state the site already has** (for example a cart line or an existing session) that is carried to the point of conversion.

If neither is available, conversions cannot be attributed. That is the cost of storing no identity.

## 4. Sessions and privacy

- No contact id, IP, user agent or hash is ever stored.
- When the Kentico cookie level is Visitor or All, the Suite sets a random `lucenesuite.session` cookie (30-minute sliding expiry, HttpOnly, no contact link) and stores its value with searches and interactions. Below Visitor, events carry no session id. You need to do nothing for this.
- Calls made outside a request (background jobs) are recorded without a session id.

## 5. Known gaps

- The server trusts the client's boundary decisions. A client that reuses one id across unrelated queries under-counts searches; one that mints an id on every keystroke over-counts them.
- The deep-backspace rule is a half-length heuristic. Because trailing spaces are trimmed, `coffee grinder` → `coffee ` → `coffee beans` is two searches (see the vectors).
- Without JavaScript, every paginated link is a new server-minted search.
