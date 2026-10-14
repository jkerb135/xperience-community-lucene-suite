---
title: Configuration
nav_order: 20
---

# Configuration

Every setting of the Suite, with its default, allowed range and effect.

```json
{
  "LuceneSuite": {
    "SearchFields": {
      "SiteSearch": { "Title": 3, "Body": 1 }
    },
    "FacetValuesPerDimension": 10,
    "Rules": { "MaxBoostFactor": 10, "CacheMinutes": 60 },
    "Synonyms": { "Weight": 0.8, "MaxExpansions": 20, "CacheMinutes": 60 },
    "Suggestions": { "WindowDays": 90, "MinSearches": 5 },
    "Analytics": {
      "Enabled": true,
      "Retention": { "RawRetention": "2.00:00:00", "DailyRetention": "762.00:00:00" },
      "Job": { "RollupInterval": "00:15:00" }
    }
  }
}
```

## How settings are read

- **Core settings** (`SuiteSearchOptions`) are bound from the `LuceneSuite` configuration section (`appsettings.json`, environment variables, any configuration provider), then the `AddLuceneSuite(options => …)` delegate runs. The delegate wins.
- **Validated on start.** An invalid value stops the application with every failure listed, each naming its key, for example:

  ```
  LuceneSuite:Rules:MaxBoostFactor must be greater than 1 and at most 1000 (was 1).
  LuceneSuite:Analytics:Retention:RawRetention must be at least 1 day + 3 × Attribution:AttributionWindow + FlushInterval + 5 min + Job:RollupInterval + 1 h = 1.04:20:02, so every day stays rollable for at least one run (was 1.00:00:00).
  ```

- **Time spans** use the .NET format: `"00:00:30"` (30 s), `"2.00:00:00"` (2 days).
- **Widgets settings** (`SuiteWidgetsOptions`) are **not** read from configuration. Set them in the `AddLuceneSuiteWidgets(options => …)` delegate. They are validated on start too.

## Search

| Key | Default | Range | Meaning |
|---|---|---|---|
| `LuceneSuite:SearchFields:{index}:{field}` | none | float boost | Fields the query text is matched against, per index, with their boosts. Index names are case-insensitive. Needed for any search with query text through the default `ISearchQueryFactory`; see [Getting started](getting-started#search-fields-are-required). |
| `LuceneSuite:FacetValuesPerDimension` | 10 | not validated; use ≥ 1 | Most values returned per facet dimension |

In code:

```csharp
builder.Services.AddLuceneSuite(options =>
{
    options.SearchFields["SiteSearch"] = new() { ["Title"] = 3f, ["Body"] = 1f };
    options.FacetValuesPerDimension = 20;
});
```

## Rules

How rules behave is described in [Rules and synonyms](rules-and-synonyms).

| Key | Default | Range | Meaning |
|---|---|---|---|
| `LuceneSuite:Rules:MaxBoostFactor` | 10 | > 1 and ≤ 1 000 | Score multiplier of a +100 boost. A strength *s* multiplies by MaxBoostFactor^(*s*/100), so +50 multiplies by about 3.16 and −100 divides by 10. |
| `LuceneSuite:Rules:CacheMinutes` | 60 | 0 – 1 440 | How long the rules of an index are cached. Any rule change clears the cache at once on every web-farm node; 0 disables caching. |

## Synonyms

| Key | Default | Range | Meaning |
|---|---|---|---|
| `LuceneSuite:Synonyms:Weight` | 0.8 | > 0 and ≤ 1 | Score weight of a synonym variant relative to the original text. A preference, not a guarantee. |
| `LuceneSuite:Synonyms:MaxExpansions` | 20 | 1 – 50 | Most query variants built from synonyms per search |
| `LuceneSuite:Synonyms:CacheMinutes` | 60 | 0 – 1 440 | How long the synonyms of an index are cached. Any synonym change clears the cache at once; 0 disables caching. |

## Suggestions

`LuceneSuite:Suggestions:WindowDays`, `MaxNoResultRate`, `MinSearches`, `MaxCandidates`, `MinInputLength`, `MaxResults` and `CacheMinutes` are described with their defaults and ranges in [Query suggestions](suggestions#settings).

## Analytics

The client-facing settings (`Merge`, `Attribution`) must match what your search UI does. They are explained in [Analytics client contract](analytics-client-contract#settings).

| Key | Default | Range | Meaning |
|---|---|---|---|
| `LuceneSuite:Analytics:Enabled` | `true` | | Whether searches and interactions are recorded and the rollup job runs. `false` records nothing (no [search activities](activities) either) and stops the rollup job, including retention: rows already stored are kept, not pruned. Reports and suggestions keep using the days already rolled up. |
| `LuceneSuite:Analytics:QueueCapacity` | 10 000 | ≥ 1 | Most events buffered in memory. Further events are dropped until the writer catches up. Read once at startup. |
| `LuceneSuite:Analytics:FlushInterval` | `00:00:02` | > 0 | Longest time an event waits in the buffer before it is written |
| `LuceneSuite:Analytics:BatchSize` | 500 | ≥ 1 | Most events per bulk insert |
| `LuceneSuite:Analytics:Merge:TextChangeWindow` | `00:00:30` | > 0 | A text change after a longer gap starts a new logical search |
| `LuceneSuite:Analytics:Merge:TypoOneEditMinLength` | 4 | ≥ 1 | Shortest query that tolerates a one-edit typo |
| `LuceneSuite:Analytics:Merge:TypoTwoEditMinLength` | 8 | > `TypoOneEditMinLength` | Shortest query that tolerates a two-edit typo |
| `LuceneSuite:Analytics:Merge:DeepBackspaceRatio` | 0.5 | > 0 and < 1 | Backspacing below this share of the original text, then typing something else, starts a new search |
| `LuceneSuite:Analytics:Attribution:AttributionWindow` | `01:00:00` | > 0 | A click or conversion counts only within this time of the response it refers to. Also the longest a logical search lasts. |
| `LuceneSuite:Analytics:Retention:RawRetention` | `2.00:00:00` (48 h) | ≥ 1 day + 3 × `AttributionWindow` + `FlushInterval` + 5 min + `RollupInterval` + 1 h (1.04:20:02 with the defaults) | How long raw search and interaction rows are kept |
| `LuceneSuite:Analytics:Retention:RawHardLimit` | `7.00:00:00` | ≥ `RawRetention` | Raw rows older than this are deleted even when the rollup is stuck, so query text is never kept indefinitely |
| `LuceneSuite:Analytics:Retention:DailyRetention` | `762.00:00:00` (25 months) | ≥ 1 day | How long daily rollup rows are kept, both the per-query rows and the per-item popularity rows. Also the longest report range and the upper bound of `Suggestions:WindowDays`. |
| `LuceneSuite:Analytics:Retention:SubThresholdPruneAfter` | `30.00:00:00` | > 0 | Daily rows of a query still below the surfacing threshold are folded into the day's "(other)" row this long after it was last seen |
| `LuceneSuite:Analytics:Surfacing:MinSearches` | 5 | ≥ 1 | Fewest logical searches before a query is named in reports and can be suggested |
| `LuceneSuite:Analytics:Surfacing:MinDistinctDays` | 2 | ≥ 1 | Fewest distinct UTC days with a search, on top of `MinSearches` |
| `LuceneSuite:Analytics:Job:RollupInterval` | `00:15:00` | > 0 | How often the rollup job runs. The first run is one minute after start. |
| `LuceneSuite:Analytics:Job:MaxDaysPerRun` | 7 | ≥ 1 | Most UTC days one run rolls up, so a catch-up after downtime spreads over several runs |

### Insights

Thresholds for [Search insights](insights); they apply to every index of the site.

| Key | Default | Range | Meaning |
|---|---|---|---|
| `LuceneSuite:Analytics:Insights:MinSearches` | 20 | ≥ 1 | Fewest searches in the range a query needs to be considered |
| `LuceneSuite:Analytics:Insights:ContentGapMinNoResultRate` | 0.3 | 0 – 1 | A query whose no-result rate is at least this is a content gap |
| `LuceneSuite:Analytics:Insights:RelevanceMissMaxCtr` | 0.1 | 0 – 1 | A query that is not a content gap and whose click-through rate is at most this is a relevance miss |
| `LuceneSuite:Analytics:Insights:BuriedMinClickedSearches` | 5 | ≥ 1 | Fewest clicked searches before a buried answer is reported |
| `LuceneSuite:Analytics:Insights:BuriedMinAvgPosition` | 5 | > 1 | A query whose average click position is at least this has a buried answer |
| `LuceneSuite:Analytics:Insights:SlowMinP95Ms` | 1000 | ≥ 1 | A query is slow when its whole 95th-percentile latency range (latency is stored in ranges such as 500 ms–1 s) is at or above this many ms |
| `LuceneSuite:Analytics:Insights:RegressionMinPoints` | 0.10 | 0 – 1 | A rise of the no-result rate, or a fall of the click-through rate, of at least this (absolute) against the previous period is a regression |
| `LuceneSuite:Analytics:Insights:MinAffected` | 5 | ≥ 1 | Fewest affected searches a finding needs, against small-sample noise |
| `LuceneSuite:Analytics:Insights:MaxQueries` | 1000 | 1 – 10 000 | Most queries evaluated per call, the most searched first |
| `LuceneSuite:Analytics:Insights:MaxFindings` | 200 | 1 – 1 000 | Most findings returned per call, the most searches affected first |

What the report numbers mean is described in [Search reports](search-reports).

## Widgets (`SuiteWidgetsOptions`)

Set in code only:

```csharp
builder.Services.AddLuceneSuiteWidgets(options =>
{
    options.PageSize = 20;
    options.SuggestionIndexes = ["SiteSearch"];
    options.EventsPerMinute = 0; // the CDN rate-limits already
});
```

| Option | Default | Range | Meaning |
|---|---|---|---|
| `PageSize` | 10 | 1 – 100 | Results per page for the search components. Site-wide, because all components of one index on a page share one search. |
| `SearchOnEmptyQuery` | `false` | | Whether the components search when the visitor has entered no text and chosen no filter (browse all) |
| `ContactGroupContexts` | `true` | | Whether the current contact's contact group code names are sent as rule contexts. A contact is never created for this. |
| `EventsPerMinute` | 60 | 0 – 10 000 | Click and conversion events accepted per client IP per minute on the events endpoint; 0 turns the limit off |
| `SuggestionIndexes` | empty | index names, case-insensitive; not null | Indexes the suggest endpoint serves. Empty turns the endpoint off. |
| `SuggestionsPerMinute` | 300 | 0 – 10 000 | Suggest requests per client IP per minute; 0 turns the limit off |
| `SuggestionsMaxAgeSeconds` | 60 | 0 – 3 600 | `Cache-Control: public, max-age=N` on suggest answers; 0 sends `no-store` |
| `SuggestionStyles` | `true` | | Whether a search box with suggestions references the shipped `lucene-suite-suggest.css` |

How the components and endpoints use these options is described in [Widgets](widgets).

### Related pages

- [Getting started](getting-started)
- [Rules and synonyms](rules-and-synonyms)
- [Query suggestions](suggestions)
- [Analytics client contract](analytics-client-contract)
