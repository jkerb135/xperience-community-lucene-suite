---
title: Search reports
nav_order: 70
---

# Search reports

How the Site Search report numbers are defined. All of them are computed from the daily rollup tables, never from raw events, so a report shows only what the rollup kept: `LuceneSuite_SearchDaily` for searches and queries, and `LuceneSuite_ItemDaily` for the [top clicked items](#top-clicked-items).

## Days and ranges

- A report covers one index and an inclusive range of **UTC days**. The UTC day is the one on which a logical search started.
- `To` may not be after today (UTC).
- The range may not be longer than `Retention:DailyRetention` (default 25 months).
- **Previous period:** the range of equal length that ends the day before `From`. For example, 1–31 March compares with 29 January – 28 February.
- **Completeness:**
  - `CompleteThrough` is the last day the rollup job has finished, also called the watermark. It is null before the first rollup.
  - Days after it, including today, are not final yet: the trend marks them `IsComplete = false`.
- **Partial previous period:**
  - `FirstDataDay` is the earliest day with any data for the index.
  - When the previous period starts before that day, `PreviousIsPartial` is true and every delta is null. A half-covered period would otherwise show growth that isn't real.

## Counts

Each count is a number of **logical searches**: the as-you-type requests that the rollup merged into one search (see [analytics-client-contract.md](analytics-client-contract)).

| Metric | Meaning |
|---|---|
| Searches | Logical searches |
| No-result searches | Searches whose results were empty |
| Submitted searches | Searches the visitor explicitly submitted |
| Clicked searches | Searches with at least one attributed click |
| Clicks | Attributed result clicks |
| Converted searches / Conversions | Searches with at least one attributed conversion / attributed conversions |
| Session-days | The sum of each day's distinct sessions. A session active on two days counts twice, so this is not a count of unique sessions. |

## Rates and deltas

| Rate | Formula |
|---|---|
| No-result rate | No-result searches ÷ Searches |
| CTR | Clicked searches ÷ Searches |
| Conversion rate | Converted searches ÷ Searches |
| Average click position | Sum of click positions (1-based) ÷ Clicks |

- A rate with a zero denominator is shown as empty, never as 0.
- **Delta:** (current − previous) ÷ previous, for counts and rates alike. It is empty when the previous value is empty or 0, or when the previous period is partial.

## Queries and "(other)"

- **Surfacing rule:** a query is named only when, over **all retained history** of the index, it has at least `Surfacing:MinSearches` searches (default 5) on at least `Surfacing:MinDistinctDays` distinct days (default 2).
  - The browse query (empty text, flagged as browse) is treated like any other text.
  - Because the rule looks at all history, a query can appear by name in a range where it had fewer searches than the threshold.
- Everything else in the range is summed into one **(other)** row. That includes queries below the threshold and rows the retention job has already folded into "(other)". The (other) row is never sorted or paged together with the named rows.
- **Text equality:** whether two texts are the same query is decided by the database collation. On the default case-insensitive collation, "Coffee" and "coffee" are one query.

**Sort orders** (ties are broken by query text, ordinal ascending):

- **Searches:** most first.
- **No results:** most first; only queries with at least one no-result search.
- **Lowest CTR:** lowest first.
- **Slowest:** highest p95 latency first.

## Latency percentiles

Each search's latency is stored in a fixed histogram:

| Bucket | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Upper bound | 10 ms | 25 ms | 50 ms | 100 ms | 250 ms | 500 ms | 1 s | 2.5 s | 5 s | overflow |

- **Percentile p:** take the histogram summed over the range, then report the upper bound of the first bucket whose cumulative count reaches ⌈p × total⌉. A p95 of "250 ms" therefore means "95 % of searches took 250 ms or less".
- **Overflow:** a percentile that lands in bucket 9 is shown as "over 5 s".
- **Empty histogram:** no percentile is reported.
- **Resolution:** results are limited to the bucket edges.

## On the report page

**Site search → an index → Search reports** shows these numbers for the chosen period.

- **Metric tiles:** each tile shows a metric, its change from the previous period and a sparkline of its daily values in the period. Select a tile to chart that metric.
- **Search terms:** the query table. **Top**, **No results**, **Lowest CTR** and **Slowest** choose the sort order (Top is the *Searches* order above). The Searches, No results and CTR columns show a data bar next to the number: Searches is relative to the largest count on the page; No results and CTR are rates from 0 to 100 %.
- **Rows per page:** 10, 25 (the default) or 50. The choice is kept per index, with the period, sort and page, for the browser tab's session.
- **No searches:** when the period has no searches, the page shows **No searches in this period** with an **Open query tester** button. Searches made in the query tester are not tracked, so they never fill a report.
- **Load failures:** when the report cannot be loaded, the last loaded report stays on screen, with its charts and bars faded and a **Retry** button. When only the query table fails, the last loaded table stays; change the sort or period to retry.

### Top clicked items

The **Top clicked items** card shows what visitors open from the results: the 10 items with the most clicked searches in the period. Above the table, a summary line gives the number of items and their clicked searches and conversions in the period.

| Column | Meaning |
|---|---|
| Item | The item's title. An item that is no longer in the index shows its item GUID and a **Not in this index** tag; it stays in the list, so the totals still add up. |
| Content type | The item's content type, or **—** when it is no longer in the index |
| Clicked searches | Logical searches with a counted click on the item, with a data bar relative to the largest value in the table |
| Avg. position | Sum of the item's click positions (1-based) ÷ its clicked searches, to one decimal; **—** when it has no clicked searches |
| Conversions | Counted conversions on the item, whether or not it was clicked |
| Actions | **Pin in a new rule** and **Hide in a new rule** (see below) |

- **What counts:** clicks and conversions are attributed exactly as for the query table, so a search counts once per item however many times that item was clicked. Summed over all items, clicked searches equal the period's Clicks and conversions equal its Conversions (see [Counts](#counts)). Items with neither a click nor a conversion in the period are left out.
- **When the data starts:** per-item rows are recorded from analytics schema version 8 on. Days rolled up before the upgrade have no item rows and are not backfilled, so a period reaching back before the upgrade shows only the items clicked since. Retention removes item rows on the same schedule as the search rows.
- **Sorting:** select the **Clicked searches**, **Conversions** or **Avg. position** header to sort by it. Clicked searches and conversions sort most first, average position lowest first with empty positions last; ties are broken by item GUID. Clicked searches is the default.
- **Show all** opens the **All clicked items** side panel with the same columns for every item in the period, starting from the card's sort. Show 10, 25 (the default) or 50 items per page. Closing the panel returns focus to **Show all**.
- **No data:** when no item was clicked or converted on in the period, the card shows **No clicked items in this period.** and no **Show all** button.
- **Load failures:** when the items cannot be loaded, the card says **The clicked items could not be loaded. The last loaded items are shown.**, keeps the last loaded rows faded, and offers **Retry**.

**Pin in a new rule** and **Hide in a new rule** open **Create rule** for the same index with the item already added under **Pinned items**, at position 1, or under **Hidden items**. Nothing is saved: choose the rule's conditions and save it yourself (see [Rules and synonyms](rules-and-synonyms#the-rule-form)). The actions are disabled, with the reason in their tooltip, without Site search → **Manage rules** (*Requires Site Search → Manage rules*) or for an item no longer in the index (*The item is no longer in this index*).

The **Fix these first** card on the same page is described in [Search insights](insights).

## Seeding demo history

The test project contains a dev-only seed runner that fills `LuceneSuite_SearchDaily` and `LuceneSuite_ItemDaily` with plausible history, so reports can be built and demonstrated before real traffic exists. Nothing from it ships in a package.

**How it builds the data:**

- It generates raw searches the way the client contract describes them: as-you-type rows, paging, sessions, clicks weighted toward the top results, conversions, typos, off-topic queries and log-normal latency.
- It rolls each day with the real rollup engine, so the stored numbers are what the same traffic would have produced live.
- Search texts and clicked items come from the stored titles of a Lucene index, or from a built-in coffee-shop list when no index path is given.

**What it writes:**

- Only `SearchDaily` and `ItemDaily` rows for the given index, and only on days that are empty and that the rollup job will never roll. The range is the N days ending before the earliest of: today (UTC), watermark + 1, the earliest raw search day, and the index's first `SearchDaily` day.
- It never touches the watermark, the raw tables or other indexes.
- It holds the rollup lock while it runs and writes in one serializable transaction.
- It does not initialize Kentico against the target database; it uses plain SQL.

| Variable | Meaning |
|---|---|
| `LUCENESUITE_SEED_CONNECTIONSTRING` | Target database (the runner is skipped without it) |
| `LUCENESUITE_SEED_CONFIRM` | Must equal the target's database name, for seed and clear alike |
| `LUCENESUITE_SEED_INDEX` | Index name the rows are written for |
| `LUCENESUITE_SEED_INDEXPATH` | Optional: the index folder (e.g. `App_Data/LuceneSearch/<index>`); the newest `i-g*` generation is read, read-only |
| `LUCENESUITE_SEED_TITLEFIELD` | Stored title field (default `Title`) |
| `LUCENESUITE_SEED_DAYS` / `_PERDAY` / `_RANDOM` | Days (60, max 400), searches per weekday (150; weekends half), random seed (2026) |
| `LUCENESUITE_SEED_CLEAR` | `yyyy-MM-dd..yyyy-MM-dd`: delete the index's rows in that range instead of seeding |

Run it with:

```bash
dotnet test tests/XperienceCommunity.Lucene.Suite.Tests --filter SeedRunner --logger "console;verbosity=detailed"
```

The output names the seeded range and the exact `LUCENESUITE_SEED_CLEAR` value.

**Use it on development and demo databases only:**

- Seeded rows carry no marker. Clear deletes everything in the range you give it, including "(other)" rows that retention later folds into those days.
- Seeded history counts toward the surfacing threshold of real queries with the same text, so real queries can surface, and later feed suggestions, on less real evidence.

Never seed a production database.
