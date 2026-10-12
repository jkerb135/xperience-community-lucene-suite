---
title: Search activities
nav_order: 60
---

# Search activities for Kentico contacts

The Suite can log what tracked contacts search for and click as Kentico **custom activities**. Marketers see them in a contact's activity log and can use them in contact-group conditions and personalization.

Suite analytics stay identity-free. Activities are a separate, opt-in channel that exists only through Kentico's own contact tracking.

## Enabling

Installing or upgrading the Suite (schema version 7) adds three custom activity types, all **disabled**:

| Activity type | Code name (`SuiteActivityTypes`) | Logged when |
|---|---|---|
| Search performed | `lucenesuite_search` (`Search`) | A search is submitted with a non-empty query |
| Search without results | `lucenesuite_search_noresults` (`SearchNoResults`) | In addition to *Search performed*, when that search returned 0 hits |
| Search result clicked | `lucenesuite_resultclick` (`ResultClick`) | A tracked result link is clicked |

To start logging, open **Contact management → Activity types**, open a type, check **Enabled** and save. This takes effect at once on every web-farm node, with no restart. Clear the box to stop.

The Suite creates each type once and never changes it again, so your Enabled choice, names and description survive upgrades.

## Who is tracked

Kentico decides, not the Suite:
- Activities are logged only for the **current contact**, when the visitor has consented to tracking and the cookie level is *Visitor* or *All* (Kentico's [custom activity rules](https://docs.kentico.com/documentation/developers-and-admins/digital-marketing-setup/set-up-activities/custom-activities)).
- The Suite adds no gate of its own.
- It logs nothing for:
  - bots, prefetch/prerender requests and Page Builder preview;
  - searches with analytics tracking turned off;
  - calls without an HTTP request.

## What is logged

| Type | Title | Value | Comment | URL |
|---|---|---|---|---|
| Search performed | `Searched for "coffee grinder"` | `coffee grinder` | index name | results page, without its query string |
| Search without results | `Search without results: "decaf pods"` | `decaf pods` | index name | as above |
| Search result clicked | `Search result clicked: "Hario V60" (position 11)` | the search query | – | results page, without its query string; the clicked page is linked when it is a web page |

- **Query text is scrubbed** exactly as for analytics. E-mail addresses and long numbers become `{email}` / `{number}`, for example `mail {email}`.
- **Only submitted searches count.** Paging, facet changes (continuations of the same search) and empty "browse" searches log nothing.
- **Conversions** log no activity.
- **Texts** are cut to 250 characters, with control characters removed.

## Click text (events contract v2)

The click beacon can send the search `query` and the result `title` (optional, at most 100 characters each). The Suite widgets send both; custom UIs can add them to `ISearchInteractionLogger.LogClick(new SearchClick(..., queryText, resultTitle))` or to the `/lucenesuite/events` body. See [analytics-client-contract.md](analytics-client-contract) §3.

- Both values come from the browser and are **untrusted**. They are used only for the activity title and value and are never stored in Suite analytics. A forged beacon can only write text into the activities of the forging visitor's own contact.
- Without them, the click activity title falls back to the item GUID and has no value.

## Privacy note: Kentico's own page visits

Kentico's built-in **Page visit** and **Landing page** activities record the full URL of the results page, so `/site-search?q=…` is stored **with the raw query**, unscrubbed. That happens outside the Suite.

If visitors might type personal data into the search box, either exclude the results page from page-visit logging or customize Kentico's activity URL handling for it.

## Contact groups

Use Kentico's standard activity conditions (for example *Contact has performed activity* with type *Search performed*) or the `SuiteActivityTypes` constants in custom macro conditions.
