---
title: Getting started
nav_order: 10
---

# Getting started

Add search rules, synonyms, analytics, suggestions and ready-made search widgets to a site that already uses [Kentico.Xperience.Lucene](https://github.com/Kentico/xperience-by-kentico-lucene). The Suite searches the Kentico Lucene indexes you already have; it does not index anything itself.

## Prerequisites

| Requirement | Version the Suite is built against |
|---|---|
| .NET | 10 (`net10.0`) |
| Xperience by Kentico (`Kentico.Xperience.WebApp`, `Kentico.Xperience.Admin`, `Kentico.Xperience.Core`) | 31.9.1 |
| Kentico Lucene (`Kentico.Xperience.Lucene.Core`, `Kentico.Xperience.Lucene.Admin`) | 15.0.5 |

You need at least one Kentico Lucene index with an indexing strategy. The Suite reads documents through `ItemGuid` (which Kentico Lucene stores on every document) and, for the widgets, the `Url` field.

## Packages

| Package | Install into | Adds |
|---|---|---|
| `XperienceCommunity.Lucene.Suite` | the live site | `ISuiteSearchService`, rules, synonyms, analytics capture and rollup, `ISearchInteractionLogger`, `ISuiteSuggestions`, contact activities |
| `XperienceCommunity.Lucene.Suite.Widgets` | the live site | Search box, results and filters as view components and Page Builder widgets; the events and suggest endpoints; JS/CSS assets |
| `XperienceCommunity.Lucene.Suite.Admin` | the project that hosts the administration | The **Site search** application: query tester, rules, synonyms, blocked suggestions, search reports |

Widgets and Admin both depend on the core package.

```bash
dotnet add package XperienceCommunity.Lucene.Suite
dotnet add package XperienceCommunity.Lucene.Suite.Widgets
dotnet add package XperienceCommunity.Lucene.Suite.Admin
```

## Register the services

In `Program.cs`, register Kentico Lucene first, then the Suite, then the Widgets:

```csharp
using XperienceCommunity.Lucene.Suite;
using XperienceCommunity.Lucene.Suite.Widgets;

builder.Services.AddKenticoLucene(lucene => lucene.RegisterStrategy<SiteSearchIndexingStrategy>("SiteSearch"), builder.Configuration);

builder.Services.AddLuceneSuite(options => options.SearchFields["SiteSearch"] = new()
{
    ["Title"] = 3f,
    ["Body"] = 1f,
});

builder.Services.AddLuceneSuiteWidgets(options => options.SuggestionIndexes = ["SiteSearch"]);

// ...
var app = builder.Build();
// ... app.InitKentico(), app.UseKentico(), app.Kentico().MapRoutes() ...

app.MapLuceneSuiteEvents();       // POST /lucenesuite/events, GET /lucenesuite/events/settings
app.MapLuceneSuiteSuggestions();  // GET /lucenesuite/suggest?index=SiteSearch&q=...
```

The full, compiling file is the first walkthrough in [Samples](samples).

| Call | Package | What it does |
|---|---|---|
| `AddLuceneSuite(Action<SuiteSearchOptions>? configure = null)` | core | Registers every core service. Binds options from the `LuceneSuite` configuration section, then applies `configure`, then validates them on start. |
| `AddLuceneSuiteWidgets(Action<SuiteWidgetsOptions>? configure = null)` | Widgets | Registers the services behind the components and endpoints. Throws `InvalidOperationException` when `AddLuceneSuite` has not been called first. |
| `MapLuceneSuiteEvents(string pattern = "/lucenesuite/events")` | Widgets | Maps the click/conversion beacon and the client settings. Without it, the results component sends no click beacons (links still carry `sr=`). |
| `MapLuceneSuiteSuggestions(string pattern = "/lucenesuite/suggest")` | Widgets | Maps the suggest endpoint. Without it, the search box shows no suggestions. |

The Admin package needs no registration: its module and pages are discovered when the assembly is referenced.

### Search fields are required

`SearchFields` tells the default query builder which index fields the query text is matched against, and their boosts. A search with query text on an index that has no entry throws:

```
System.InvalidOperationException: No search fields are configured for index 'SiteSearch'. Set SuiteSearchOptions.SearchFields["SiteSearch"].
```

Searches without query text (browse) work without it. Index names are case-insensitive. You can set the fields in `appsettings.json` instead of code; see [Configuration](configuration).

## What happens on first start

- **Database objects are installed automatically.** On application start the Suite creates or upgrades its module, its classes (rules, synonyms, blocked suggestions, raw search events, raw interactions, daily rollup) and three disabled custom activity types. This runs under a farm-wide SQL lock, so several instances may start at once. The installed schema version is 7.
- **Two background services start:** the event writer, which bulk-inserts captured searches, and the rollup job, which builds the daily table every 15 minutes and applies retention.
- **A session cookie is registered.** `lucenesuite.session` is a random id, set only when the Kentico cookie level is *Visitor* or higher. See [Analytics client contract](analytics-client-contract) §4.

## Configuration overview

All core settings live under one `LuceneSuite` section:

```json
{
  "LuceneSuite": {
    "SearchFields": { "SiteSearch": { "Title": 3, "Body": 1 } },
    "FacetValuesPerDimension": 10,
    "Rules": { },
    "Synonyms": { },
    "Suggestions": { },
    "Analytics": { "Merge": { }, "Attribution": { }, "Retention": { }, "Surfacing": { }, "Job": { } }
  }
}
```

Every key has a default, so only `SearchFields` is needed. An invalid value stops the application at startup with a message that names the key. The Widgets options (`SuiteWidgetsOptions`) are set only in the `AddLuceneSuiteWidgets` delegate. Every option is listed in [Configuration](configuration).

## What the Admin package adds

A **Site search** application in the *Digital marketing* category:

- **Indexes**: the Kentico Lucene indexes. Each opens a **Search tuning** section with:
  - **Search reports**: KPIs, trends and query tables; see [Search reports](search-reports).
  - **Query tester**: runs searches through `ISuiteSearchService` without recording analytics. You can switch rules and synonyms on or off, simulate visitor contexts, and compare two settings side by side.
  - **Rules** and **Synonyms**: see [Rules and synonyms](rules-and-synonyms).
  - **Blocked suggestions**: see [Query suggestions](suggestions).
- A **Search tuning** row action on Kentico's own Lucene index listing, shown to users who have Site search → View.

Permissions, assignable per role in **Roles**:

| Permission | Code name | Allows |
|---|---|---|
| View | (system) | Open every page read-only, run the query tester, view reports |
| Manage rules | `LuceneSuite.ManageRules` | Create, edit and delete rules |
| Manage synonyms | `LuceneSuite.ManageSynonyms` | Create, edit and delete synonyms; block and unblock suggestions |
| Manage experiments | `LuceneSuite.ManageExperiments` | Declared, but no page uses it yet |

## Next steps

- Put search on a page: [Widgets](widgets).
- Call search from your own code: [Samples](samples).
- Tune results: [Rules and synonyms](rules-and-synonyms).

### Related pages

- [Configuration](configuration)
- [Samples](samples)
- [Widgets](widgets)
- [Analytics client contract](analytics-client-contract)
