---
title: Rules and synonyms
nav_order: 30
---

# Rules and synonyms

Tune what an index returns without reindexing or deploying:

- **Rules** pin items to fixed positions, hide items, boost or bury items by field value, and return custom data to the site. Each rule fires for the searches its conditions describe.
- **Synonyms** make a query also find other words: `laptop` finds `notebook`.

Both are stored per index in the database, edited in the **Site search** admin application, and applied by `ISuiteSearchService` on every search. They take effect on the next search, with no restart.

```csharp
var response = search.Search(new SuiteSearchRequest
{
    IndexName = "SiteSearch",
    Query = "black friday deals",
    Contexts = ["VipCustomers"],  // visitor contexts that rule conditions can require
    ApplyRules = true,            // default
    ApplySynonyms = true,         // default
});

IReadOnlyList<int> fired = response.FiredRuleIds;                  // rules that fired, in run order
IReadOnlyList<SuiteRuleUserData> data = response.UserData;         // custom data of fired rules: RuleId, Data (JsonElement)
int? pinnedBy = response.Hits.FirstOrDefault()?.PinnedByRuleId;    // the rule that pinned this hit, or null
IReadOnlyList<SuiteAppliedSynonym> synonyms = response.AppliedSynonyms; // SynonymId, Term, Replacement
bool rulesFailed = response.RulesFailed;       // rules could not be loaded or applied; results are unruled
bool synonymsFailed = response.SynonymsFailed; // synonyms could not be loaded or applied; the original text was searched alone
```

## Authoring in the admin

Open **Site search → Indexes → (index) → Rules** or **→ Synonyms**. Kentico's own Lucene index listing also has a **Search tuning** row action that opens the same section.

| Action | Permission |
|---|---|
| See the lists and open a rule or synonym read-only | Site search → View |
| Create, edit and delete rules | Site search → **Manage rules** |
| Create, edit and delete synonyms | Site search → **Manage synonyms** |

Two shortcuts from **Search reports**: in the query table, **Create rule for "…"** opens a new rule whose first condition is *Is* that query, and **Add synonym for "…"** opens a new synonym with that query as its first term.

If someone else saved the same rule or synonym after you opened it, your save is refused, so their change is not lost silently.

## Rules

### The rule form

| Field | Default | Meaning |
|---|---|---|
| Name | | Shown in lists, the query tester and nowhere on the site |
| Description | | Notes |
| Enabled | on | A disabled rule never fires |
| Priority | 10 | ≥ 1. 1 runs first. When two rules pin to the same position, the earlier rule wins. Ties are broken by rule id. |
| Valid from (UTC) | empty | The rule fires from this time on (inclusive). Empty: no start. |
| Valid until (UTC) | empty | The rule stops firing at this time (exclusive). Must be later than Valid from. Empty: no end. |
| Conditions | none | When the rule fires; see below |
| Boosts and buries | none | Field, Value, Strength |
| Pinned items | none | Item and Position |
| Pinned items must match the applied filters | on | See [Pins](#pins) |
| Hidden items | none | Items removed from results |
| Custom data | empty | A JSON object of at most 1 KB, returned to the site in `SuiteSearchResponse.UserData` when the rule fires |

A rule needs at least one action: a pin, a hide, a boost or custom data. Enabled and validity are checked on every search, so a scheduled rule starts and stops on time without a cache clear.

### Conditions

A rule fires when **any** of its conditions matches. A rule with **no** conditions fires on every search of the index. Within one condition, **every** part that is set must match:

| Part | Matches when |
|---|---|
| **Query**: *Any query* | Always (no text part) |
| **Query**: *Is* + Text | The query is exactly these words. *Is* with empty Text matches the empty (browse) query. |
| **Query**: *Starts with* + Text | The query starts with these words |
| **Query**: *Ends with* + Text | The query ends with these words |
| **Query**: *Contains* + Text | These words appear in the query, together and in order |
| **Facet** + Value (repeatable) | Every listed facet value is among the request's applied filters (`SuiteSearchRequest.Facets`) |
| **Visitor context** (repeatable) | At least one listed context is in `SuiteSearchRequest.Contexts` |

How text is compared:

- Both sides are lower-cased, trimmed and have their whitespace collapsed. `Black  Friday deals` contains `black friday`.
- Matching is on **whole words**: *Starts with* `coffee` does not match `coffeemaker`.
- **Punctuation is kept**: *Contains* `black friday` does not match `black friday!`. Synonyms trim punctuation; rules do not.
- Rules see the query **as typed**, never its synonym variants.

Facet dimensions, facet values and contexts are compared exactly (case-sensitive).

#### Visitor contexts

A context is a string your site sends with the search, at most 50 per request and 100 characters each. In a rule it may hold 1–100 letters, digits, `_`, `.` or `-`. The Widgets components send the current contact's **contact group code names** (when `ContactGroupContexts` is on, the default), so *Visitor context* `VipCustomers` targets the contact group with that code name. Your own code can send anything:

```csharp
Contexts = ["VipCustomers", "region.emea"]
```

### Actions

#### Pins

- A pinned item is placed at its 1-based position (1–300) in the full result list, wherever it would otherwise rank. It **does not need to match the query**.
- Pinned items count in `TotalHits` and in facet counts.
- An item already ranked elsewhere moves to its pinned position; it is never listed twice. For multilingual indexes, every language document of the item is one pin.
- A pin past the end of the results is appended.
- A pin is skipped when the item is not in the index, when any fired rule hides it, or, with **Pinned items must match the applied filters** on, when it does not match the request's facet filters.
- `SuiteSearchHit.PinnedByRuleId` names the rule. A pinned hit that did not rank by itself within the fetched window has `Score` 0.

In the editor, **Add pins** searches the index (rules off, no analytics) to pick items. An item that has left the index is shown as **Not in this index**.

#### Hides

Hidden items are removed from results, totals and facet counts. A hide beats any pin, from any rule.

#### Boosts and buries

Each boost is a **Field**, a **Value** and a **Strength** from −100 to 100, not 0. A document matches when that index field holds the exact value, or the value's analyzed words as a phrase. Its score is multiplied by `MaxBoostFactor`^(Strength/100):

| Strength | Factor with the default `MaxBoostFactor` 10 |
|---|---|
| 100 | × 10 |
| 50 | × 3.16 |
| −50 | ÷ 3.16 |
| −100 | ÷ 10 (bury) |

Boost **index fields**, not facets: a facet dimension matches only if the indexing strategy also writes it as a field.

#### Custom data

The JSON object is returned unchanged, so the site can show a banner, a redirect hint or anything else when a rule fires:

```csharp
string? banner = response.UserData
    .Select(data => data.Data.TryGetProperty("banner", out var value) ? value.GetString() : null)
    .FirstOrDefault(text => text is not null);
```

### When several rules fire

All fired rules apply together, in priority order:

- **Pins:** rules place their pins in priority order. If the position is already taken by an earlier rule, the pin moves to the next free position (up to 300). An item pinned by an earlier rule keeps that rule's position.
- **Hides:** the union of all hides.
- **Boosts:** strengths for the same field and value are added, then clamped to ±100. +50 and +80 give +100. Opposite strengths that cancel out to 0 are dropped. At most the 200 strongest boosts apply to one search; the rest are left out and a warning is logged.
- **Custom data:** one entry per fired rule that has it, in run order.

### Limits

| Item | Limit |
|---|---|
| Conditions per rule | 25 |
| Text per condition | 200 characters |
| Facet pairs per condition | 10 |
| Visitor contexts per condition | 20 |
| Pins per rule / highest position | 300 / 300 |
| Hides per rule | 50 |
| Boosts per rule | 50 |
| Boost field name | 100 characters |
| Boost value, facet dimension, facet value | 200 characters |
| Custom data | 1 024 bytes (UTF-8, compact JSON) |

The editor checks these with the same rules the search uses. A stored rule that is no longer valid is skipped at search time, and a warning naming it is logged at most once a minute per index.

## Synonyms

### The synonym form

| Field | Meaning |
|---|---|
| Kind | **Two-way: every term finds the others** (stored as `twoWay`) or **One-way: the input term finds the terms** (`oneWay`) |
| Input term | One-way only: the term that expands. Searches for the terms below do not find it. |
| Terms | Two-way: 2–20 terms. One-way: 1–20 terms. |
| Enabled | Disabled synonyms are not loaded |

Each term or input term: at most 100 characters and 5 words, with at least one letter or digit. Terms must be distinct from each other and from the input term, ignoring case and edge punctuation.

### How a query is expanded

The Suite looks for runs of 1–5 consecutive query words that equal a synonym term, and builds a **variant** of the query with that run replaced. With the synonyms `laptop ⇔ notebook`, `bag ⇔ case` and `tv → television`:

| Query | Variants searched besides the query itself |
|---|---|
| `Laptop bag` | `notebook bag`, `Laptop case` |
| `tv stand` | `television stand` |
| `television stand` | none (one-way) |
| `laptop!` | `notebook` |

- **Comparison:** words are compared lower-cased, with punctuation trimmed from the start and end of each word. Accents are not folded.
- **One substitution per variant, no chaining:** `Laptop bag` does not also search `notebook case`.
- **Variant count:** variants that equal the query or an earlier variant are dropped. At most `Synonyms:MaxExpansions` (default 20) are built, in query word order.
- **Building variants:** each variant goes through the same `ISearchQueryFactory` as the query, with its boost multiplied by `Synonyms:Weight` (default 0.8).
- **Scoring:** a document scores by its best-matching variant, so a document that matches both the query and a synonym is not counted twice. Exact matches of what the visitor typed rank higher by default.
- **Browse:** synonyms never apply to the empty (browse) query.

`SuiteSearchResponse.AppliedSynonyms` lists one `SuiteAppliedSynonym` per variant: `SynonymId`, `Term` (the query words as typed) and `Replacement`.

## Caching and failures

- Rules and synonyms of an index are cached for `Rules:CacheMinutes` / `Synonyms:CacheMinutes` (default 60). Any create, edit or delete clears the cache at once on every web-farm node (cache dependencies `lucenesuite.rule|all` and `lucenesuite.synonym|all`).
- If rules or synonyms cannot be loaded or applied, the search still runs without them. `RulesFailed` / `SynonymsFailed` is set, and a warning is logged at most once a minute.
- Invalid requests (`ArgumentException`) still reach the caller; they are not rule failures.

## Testing in the admin

**Search tuning → Query tester** runs searches through `ISuiteSearchService` without recording analytics. It shows:

- the hits, with a marker on pinned ones;
- **fired rules**, linked to their edit pages, with their custom data;
- **applied synonyms**, as term → replacement.

Use the **Apply rules** and **Apply synonyms** switches to see the untuned results. Visitor contexts can be simulated with free text or, for users who can view Kentico's Contact groups application, by picking contact groups. **Compare** shows a *Baseline* and a *Tuned* column side by side, each with its own switches and contexts.

### Related pages

- [Configuration](configuration)
- [Query suggestions](suggestions)
- [Search reports](search-reports)
- [Samples](samples)
