---
title: Search insights
nav_order: 75
---

# Search insights

Reports show what visitors searched for. Insights tell you **what to fix first**. Each insight (a *finding*) names one search query, the problem with it, the numbers behind it and how many searches it affected. Findings are ranked by searches affected, so the top of the list is where a fix helps the most visitors.

Insights use the same data and the same date range as the [search reports](search-reports), so their numbers always match the query table.

## Where you see them

Open **Site search → an index → Search reports**. The **Fix these first** card lists the five findings that affect the most searches. Each shows:

- the kind of finding;
- the query, with its number of searches in the range ("{n} searches");
- a bar with the figure the finding is about: the no-result rate (content gap), the CTR (relevance miss), the average click position on a scale of 10 (buried answer), the share of slow searches (slow query), or, for a regression, whichever of the no-result rate and the CTR got worse by more points;
- one line with the numbers behind it;
- any fixes already in place, after **Already applies:**;
- buttons for the fixes that suit it.

| Finding | Buttons |
|---|---|
| Content gap ("No results") | Add synonym · Create rule · Open in query tester |
| Relevance miss ("Results not clicked") | Create rule · Open in query tester · Block suggestion |
| Buried answer ("Answer buried") | Create rule · Open in query tester |
| Slow query ("Slow") | Open in query tester |
| Regression ("Got worse") | Open in query tester · Create rule · Add synonym |

The buttons work like the ones in the query table. *Add synonym* and *Create rule* open the editor with the query already filled in; nothing is saved until you save it there. A button you lack the permission for is shown disabled, with the reason.

**Show all insights** opens a side panel with every finding, grouped by kind.

When no query reaches the thresholds, for example on a small site or a short date range, the card shows the queries with the most searches without results instead, as it did before insights existed.

## What insights look for

A query is checked only when it is shown by name in the reports, and only when it had at least `MinSearches` searches in the selected range (default 20). Browsing without a search term and the "(other)" row are never checked.

| Finding | What it means | Default threshold | Searches affected | Usual fix |
|---|---|---|---|---|
| **Content gap** | Visitors search for something the site doesn't seem to have. | At least 30 % of the searches found nothing (`ContentGapMinNoResultRate` 0.3). | The searches that found nothing. | Add a synonym that maps the visitors' words to yours, or create the missing content. |
| **Relevance miss** | Searches find results, but visitors don't click any of them. | Not a content gap, and 10 % or fewer of the searches got a click (`RelevanceMissMaxCtr` 0.1). | The searches with results but no click. | Pin the page visitors are looking for to the top, or boost the right content with a rule. |
| **Buried answer** | Visitors find what they want, but only far down the list. | At least 5 searches with a click (`BuriedMinClickedSearches`), and the average clicked position is 5 or lower down (`BuriedMinAvgPosition` 5). | The searches with a click. | Pin the most-clicked result, or boost it with a rule. |
| **Slow query** | At least 5 % of these searches take too long. | The 95th-percentile response time is certainly 1 second or more (`SlowMinP95Ms` 1000). Response times are stored in ranges (for example 500 ms–1 s), so a query counts as slow only when its whole range is at or above the setting. | The searches that took at least that long. | Ask a developer to look at the query; very broad queries are often the cause. |
| **Regression** | The query got worse than in the period before. | Compared with the previous period of the same length: the share of searches that found nothing rose by 10 points or more, or the share that got a click fell by 10 points or more (`RegressionMinPoints` 0.10). | The searches lost to the change: searches in the range × the change (120 searches and a 15-point drop in clicks = 18). | Check what changed: content that was removed or renamed, or a rule or synonym that was edited. |

A query can have several findings at once, for example a content gap that is also slow. It is never both a content gap and a relevance miss: when many searches find nothing, that is the problem to fix first.

### Small numbers are ignored

A finding needs at least `MinAffected` affected searches (default 5). This keeps chance results off the list: going from 0 to 2 no-result searches out of 20 is a 10-point rise, but it is 2 searches, so it is not reported.

For a regression, the previous period must also have at least `MinSearches` searches of the query. The searches behind the change must add up to at least `MinAffected` over both periods. Regressions are not reported when the previous period starts before the first day with data (the reports call this a *partial* previous period).

## Fixes already in place

Each finding lists the synonyms that already change the query and the rules that already fire for it, so you can see whether something has been tried.

- Rules are checked against the query text alone, at the current time. A rule whose conditions also need a facet or a visitor context (such as a contact group) is not shown, even if it fires for some visitors.
- If the rules or synonyms cannot be loaded, the findings are still shown, without this list, and the page says so.

## Insights come from history

Insights are calculated from past searches each time you open them; nothing is stored. A query you have just fixed stays on the list until the date range moves past the searches from before the fix. The fixes already in place are shown next to it, so you can tell it has been handled.

## Settings

All settings are under `LuceneSuite:Analytics:Insights` in `appsettings.json`. They apply to the whole site: every index uses the same thresholds.

| Setting | Default | Allowed |
|---|---|---|
| `MinSearches` | 20 | ≥ 1 |
| `ContentGapMinNoResultRate` | 0.3 | 0–1 |
| `RelevanceMissMaxCtr` | 0.1 | 0–1 |
| `BuriedMinClickedSearches` | 5 | ≥ 1 |
| `BuriedMinAvgPosition` | 5 | > 1 |
| `SlowMinP95Ms` | 1000 | ≥ 1 |
| `RegressionMinPoints` | 0.10 | 0–1 (absolute change of a rate) |
| `MinAffected` | 5 | ≥ 1 |
| `MaxQueries` | 1000 | 1–10 000 (most searched queries checked per range) |
| `MaxFindings` | 200 | 1–1 000 (findings listed; the rest are counted) |

An invalid value stops the site at startup with a message naming the setting.

### A small site

Few searches per query, so lower the bar for checking a query, but keep `MinAffected` so single searches don't create findings:

```json
{
  "LuceneSuite": {
    "Analytics": {
      "Insights": {
        "MinSearches": 5,
        "MinAffected": 3
      }
    }
  }
}
```

### A large site

Thousands of searches per query, so raise the bar to keep the list short and focused on what matters:

```json
{
  "LuceneSuite": {
    "Analytics": {
      "Insights": {
        "MinSearches": 200,
        "MinAffected": 50,
        "RegressionMinPoints": 0.05,
        "MaxQueries": 5000
      }
    }
  }
}
```

With many searches, a 5-point change is already a real trend, so `RegressionMinPoints` can be lower than on a small site.
