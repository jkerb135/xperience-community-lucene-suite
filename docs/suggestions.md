# Query suggestions

As-you-type suggestions built from what visitors already search successfully on an index. They steer visitors to queries that work. A per-index blocklist keeps unwanted texts out of the suggestions; reports are unaffected.

- **C# API:** `ISuiteSuggestions` (core package, registered by `AddLuceneSuite`). This is the primary API.
- **HTTP endpoint:** `MapLuceneSuiteSuggestions` (Widgets package), a thin, opt-in adapter over the API that returns strings only.

Neither records analytics or runs a Lucene search: suggestions come from the daily rollup table `LuceneSuite_SearchDaily`.

## Setup

```csharp
builder.Services.AddKenticoLucene(/* ... */);
builder.Services.AddLuceneSuite();
builder.Services.AddLuceneSuiteWidgets(o => o.SuggestionIndexes = ["DancingGoatSample"]);

// ...
app.MapLuceneSuiteSuggestions(); // GET /lucenesuite/suggest?index=DancingGoatSample&q=coff
```

Only the indexes in `SuggestionIndexes` are served; with the list empty (the default) the endpoint answers 404 to everything. The C# API serves every Kentico Lucene index.

### Settings

Core settings, bound from `appsettings.json` and validated on start (an invalid value stops the application with a message naming the key):

| Key | Default | Range | Meaning |
|---|---|---|---|
| `LuceneSuite:Suggestions:WindowDays` | 90 | 1 – `Analytics:Retention:DailyRetention` days | Complete UTC days, ending yesterday, whose searches feed suggestions |
| `LuceneSuite:Suggestions:MaxNoResultRate` | 0.5 | 0 – 1 | A query whose no-result searches are at least this share of its searches is not suggested. 1 drops only queries that never found anything; 0 drops everything. |
| `LuceneSuite:Suggestions:MinSearches` | 5 | ≥ 1 | Fewest searches inside the window |
| `LuceneSuite:Suggestions:MaxCandidates` | 2 000 | 1 – 20 000 | Most query texts loaded per index, the most searched first |
| `LuceneSuite:Suggestions:MinInputLength` | 2 | 1 – 20 | Shortest input (after normalization) that gets suggestions |
| `LuceneSuite:Suggestions:MaxResults` | 8 | 1 – 20 | Suggestions returned when the caller does not ask for a number |
| `LuceneSuite:Suggestions:CacheMinutes` | 60 | 0 – 1 440 | How long the candidates of an index are cached; 0 disables caching |

Widgets settings, set in the `AddLuceneSuiteWidgets` delegate:

| Option | Default | Range | Meaning |
|---|---|---|---|
| `SuggestionIndexes` | empty | index names, case-insensitive | Indexes the endpoint serves |
| `SuggestionsPerMinute` | 300 | 0 – 10 000 | Requests per client IP per minute; 0 turns the limit off. Separate from `EventsPerMinute`. |
| `SuggestionsMaxAgeSeconds` | 60 | 0 – 3 600 | `Cache-Control: public, max-age=N` on answers; 0 sends `no-store` |

## Which queries are suggested

A query text is suggested only when all of these hold:

1. **It is surfaced:** it meets the report threshold `Analytics:Surfacing` (default 5 searches on 2 distinct days, over all kept days). This is the same SQL that decides which queries the reports name.
2. **It was searched at least `MinSearches` times inside the window.** Suggestions show query text to every visitor, while reports show it only to admins, so public exposure has its own floor.
3. **It mostly finds something:** its no-result rate inside the window is below `MaxNoResultRate`.
4. **It is not browse** (the empty query) **or "(other)"** (texts below the threshold that the rollup folded together).
5. **It contains no `{email}` or `{number}` placeholder.** Capture replaces emails and long digit runs with these, so such texts are never shown.
6. **No blocklist entry of the index matches it.**
7. **It is among the `MaxCandidates` most searched texts** that pass 1–6.

So a popular query may be missing because:

- it is new and not surfaced yet, or its searches fall outside the window;
- most of its searches returned nothing;
- it is blocked;
- it changed less than `CacheMinutes` ago (see below).

### Matching and order

Input and query texts are compared by their match key: lower-cased, whitespace collapsed, punctuation trimmed from the ends of each word (the same key synonyms use). Accents are not folded: `creme` does not match `crème`.

1. First, queries that start with the input (`coff` → `coffee`, `coffee grinder`).
2. Then, queries where a later word starts the match: the input's earlier words must match whole, in order, and its last word is a prefix (`coffee gr` → `best coffee grinder`).

Each group is ordered by searches, then text. Texts that differ only in punctuation are one suggestion, shown as their most searched form.

## Blocklist

Entries are stored per index in module class `LuceneSuite.BlockedSuggestion` (installer version 6). An admin editor follows in a later release.

- **Exact** (`MatchMode = "exact"`): removes the query whose match key equals the entry's (`Coffee Grinder!` blocks `coffee grinder`, not `coffee grinders`).
- **Word** (`MatchMode = "word"`): removes every query that contains the entry as whole words (`acme` blocks `acme grinder` and `grinder acme deal`, not `acmeco`).

### How fast a block applies

- **On the server: at once.** The blocklist is cached with the `lucenesuite.blockedsuggestion|all` dependency, so a change clears it on every web-farm node.
- **In browsers and CDNs: up to `SuggestionsMaxAgeSeconds` later.** Endpoint answers are publicly cacheable for that long. A site that needs instant removal sets it to 0.

Query candidates themselves (new popular queries, changed no-result rates) refresh at most every `CacheMinutes`.

## C# API

```csharp
public interface ISuiteSuggestions
{
    const int MaxInputLength = 100;
    const int MaxResultsLimit = 20;

    Task<IReadOnlyList<string>> SuggestAsync(string indexName, string? input, int? maxResults = null, CancellationToken cancellationToken = default);
}
```

- **An unknown index** returns an empty list. The name is checked against the Kentico Lucene indexes before anything is queried or cached.
- **Null input, or input longer than 100 characters or shorter than `MinInputLength`,** returns an empty list.
- **`maxResults`** outside 1–20 throws `ArgumentOutOfRangeException`; null uses `MaxResults`.

Use it to map your own endpoint, or to render suggestions on the server.

## Endpoint contract

`GET {pattern}?index={index}&q={input}`, pattern default `/lucenesuite/suggest`.

| Case | Status | Body | Cache-Control |
|---|---|---|---|
| Suggestions (possibly none) | 200 | JSON array of strings | `public, max-age={SuggestionsMaxAgeSeconds}` (`no-store` when 0) |
| `q` missing or empty | 200 | `[]` | as above |
| `index` not in `SuggestionIndexes` | 404 | | `no-store` |
| `q` longer than 100 characters | 400 | | `no-store` |
| Over `SuggestionsPerMinute` for the client IP | 429 | | `no-store` |

The endpoint never changes the host's rate limiter configuration, logs nothing per request (a rejected request logs a warning at most once a minute), and writes no analytics rows.

### Behind a proxy or CDN

The per-IP limit partitions on `HttpContext.Connection.RemoteIpAddress`. Behind a reverse proxy, load balancer or CDN, configure ASP.NET Core forwarded headers (`UseForwardedHeaders` with your proxy's addresses trusted). Otherwise every visitor shares the proxy's IP and one budget, and real visitors get 429. If the proxy rate-limits on its own, set `SuggestionsPerMinute` to 0.

### Rendering

Suggestion strings are past visitor input: **untrusted text**. Render them with `textContent` (or an HTML-encoding template), never `innerHTML`.

## Privacy and residual risk

Suggestions are not personalized and identify no one. Every suggestion is a query that many searches typed (surfacing threshold plus `MinSearches`), with emails and long numbers already replaced at capture.

The residual risk: personal or sensitive text that is typed repeatedly (a name, an address fragment, a short ID) can still pass the thresholds and be shown to every visitor.

Mitigations:

- raise `MinSearches` or `Analytics:Surfacing`;
- shorten `WindowDays`;
- block the text (exact) or a word of it;
- keep suggestions off an index by leaving it out of `SuggestionIndexes`.
