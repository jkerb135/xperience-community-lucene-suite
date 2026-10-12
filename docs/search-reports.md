---
title: Search reports
nav_order: 70
---

# Search reports

How the Site Search report numbers are defined. All of them are computed from the daily rollup table `LuceneSuite_SearchDaily`, never from raw events, so a report shows only what the rollup kept.

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

Each count is a number of **logical searches**: the as-you-type requests that the rollup merged into one search (see [analytics-client-contract.md](analytics-client-contract.md)).

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

## Seeding demo history

The test project contains a dev-only seed runner that fills `LuceneSuite_SearchDaily` with plausible history, so reports can be built and demonstrated before real traffic exists. Nothing from it ships in a package.

**How it builds the data:**

- It generates raw searches the way the client contract describes them: as-you-type rows, paging, sessions, clicks weighted toward the top results, conversions, typos, off-topic queries and log-normal latency.
- It rolls each day with the real rollup engine, so the stored numbers are what the same traffic would have produced live.
- Search texts and clicked items come from the stored titles of a Lucene index, or from a built-in coffee-shop list when no index path is given.

**What it writes:**

- Only `SearchDaily` rows for the given index, and only on days that are empty and that the rollup job will never roll. The range is the N days ending before the earliest of: today (UTC), watermark + 1, the earliest raw search day, and the index's first `SearchDaily` day.
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
