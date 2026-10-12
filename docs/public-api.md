---
title: Public API
nav_order: 85
---

# Public API

What the three packages promise to site developers, what is public only because Kentico needs it, and how the boundary is kept.

Core plus Admin are enough for a custom search UI; the Widgets package is optional and can be customized. Build against the **Supported** types below. Every other public type is plumbing.

## Core: `XperienceCommunity.Lucene.Suite`

| Type | Namespace | Use |
|---|---|---|
| `LuceneSuiteServiceCollectionExtensions` | `XperienceCommunity.Lucene.Suite` | `AddLuceneSuite` |
| `ISuiteSearchService` | `….Search` | Run a search |
| `SuiteSearchRequest`, `SuiteSearchResponse`, `SuiteSearchHit`, `SuiteFacet`, `SuiteFacetValue`, `SuiteAppliedSynonym`, `SuiteRuleUserData` | `….Search` | Search request and response |
| `ISearchQueryFactory`, `DefaultSearchQueryFactory` | `….Search` | Custom query parsing |
| `SuiteSearchOptions`, `SuiteRulesOptions`, `SuiteSynonymsOptions`, `SuiteSuggestionOptions`, `SuiteAnalyticsOptions`, `SuiteRetentionOptions`, `SuiteJobOptions`, `SuiteAttributionOptions`, `SuiteMergeOptions`, `SuiteSurfacingOptions`, `SuiteInsightsOptions` | `….Search` | Settings, bound from the `LuceneSuite` section |
| `ISuiteSuggestions` | `….Suggestions` | Query suggestions |
| `ISearchInteractionLogger`, `SearchClick` | `….Analytics` | Log clicks and conversions from a custom UI |
| `SuiteActivityTypes` | `….Activities` | Activity type code names |

Public only for Kentico: the Info classes (`SearchEventInfo`, `SearchInteractionInfo`, `SearchDailyInfo`, `RuleInfo`, `SynonymInfo`, `BlockedSuggestionInfo`), their enums `SearchEventOrigin` and `SearchInteractionType`, and `RuleError`. Kentico registers the object types and the admin client receives `RuleError`; their tables and shapes may change in any release.

## Admin: `XperienceCommunity.Lucene.Suite.Admin`

| Type | Namespace | Use |
|---|---|---|
| `SiteSearchPermissions` | `XperienceCommunity.Lucene.Suite.Admin` | Permission code names, for your own admin pages that check them |

Everything else in the package is public only because Kentico's admin discovers or serializes it: application and pages, page extender, form components and their attributes, validation rules, edit models, client properties and page-command arguments and results. Do not reference these types; they follow the admin screens.

## Widgets: `XperienceCommunity.Lucene.Suite.Widgets`

| Type | Namespace | Use |
|---|---|---|
| `LuceneSuiteWidgetsServiceCollectionExtensions` | `XperienceCommunity.Lucene.Suite.Widgets` | `AddLuceneSuiteWidgets` |
| `SuiteEventsEndpoints`, `SuiteSuggestEndpoints` | `XperienceCommunity.Lucene.Suite.Widgets` | `MapLuceneSuiteEvents`, `MapLuceneSuiteSuggestions` |
| `SuiteWidgetsOptions` | `XperienceCommunity.Lucene.Suite.Widgets` | Settings, bound from `LuceneSuite:Widgets` |
| `LuceneSuiteSearchBoxViewComponent`, `LuceneSuiteResultsViewComponent`, `LuceneSuiteFacetsViewComponent` | `….Components` | `<vc:lucene-suite-*>` components |
| `LuceneSuiteSearchBoxModel`, `LuceneSuiteResultsModel`, `LuceneSuiteResultHit`, `LuceneSuitePageLink`, `LuceneSuiteFacetsModel`, `LuceneSuiteFacetGroup`, `LuceneSuiteFacetOption` | `….Components` | Models of the views you can [override](widgets#overriding-the-markup) |
| `SuitePageSearch`, `SuitePageSearchResult`, `SuitePageSearchStatus`, `SuiteSearchUrl` | `….Search` | The shared per-request search the views read |
| `LuceneSuiteSearchBoxWidgetProperties`, `LuceneSuiteResultsWidgetProperties`, `LuceneSuiteFiltersWidgetProperties` | `….PageBuilder` | Page Builder widget properties |
| `SuiteResultReference` | `….Events` | Parse the `sr` parameter for conversions |

Public only for Kentico: the widget classes (`LuceneSuiteSearchBoxWidget`, `LuceneSuiteResultsWidget`, `LuceneSuiteFiltersWidget`) and `LuceneIndexOptionsProvider`. Place the widgets in Page Builder; do not reference the classes.

## Internal by design

These stay `internal`, on purpose:

- search reports;
- search insights;
- rule, synonym and blocklist management, including the rule and synonym models and serializers;
- event capture and the daily rollup.

They serve the admin screens only and change with them. Use the admin UI for them. A custom UI needs only the Supported types above and the analytics contract.

## Other public surfaces

The analyzer below only guards C# types. These surfaces are public too, and changing them breaks sites just the same:

| Surface | Specified in |
|---|---|
| HTTP endpoints: `POST /lucenesuite/events`, `GET /lucenesuite/events/settings`, `GET /lucenesuite/suggest` | [Widgets › Endpoints](widgets#endpoints), [Query suggestions › Endpoint contract](suggestions#endpoint-contract) |
| Analytics client contract (search ids, sequences, beacon bodies, test vectors) | [Analytics client contract](analytics-client-contract) |
| JS module exports of `lucene-suite.js`, `lucene-suite-results.js`, `lucene-suite-suggest.js` | [Widgets › Scripts and styles](widgets#scripts-and-styles), [Query suggestions › JS API](suggestions#js-api) |
| Search page URL parameters (`q`, `page`, `f.{dimension}`, `sid`, `seq`, `sr`) | [Widgets › How the page search works](widgets#how-the-page-search-works) |
| `data-lucene-suite-*` attributes | [Widgets › Overriding the markup](widgets#overriding-the-markup), [Query suggestions › Search box autocomplete](suggestions#search-box-autocomplete) |
| CSS class names and custom properties | [Widgets › Class names](widgets#class-names), [Query suggestions › Classes and theming](suggestions#classes-and-theming) |
| `appsettings.json` keys under `LuceneSuite` | [Configuration](configuration) |

## Changing the API

Each package has `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` next to its project file, checked by [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md). Together they list every public member, the plumbing included.

- **Adding a public member** without listing it fails the build with RS0016. Add it to `PublicAPI.Unshipped.txt`, either with the IDE code fix or with `dotnet format analyzers <project>.csproj --diagnostics RS0016 --severity info`.
- **Removing or changing a member** fails the build with RS0017. Delete or edit its line. If the line is in `PublicAPI.Shipped.txt`, the change is breaking: leave Shipped as it is and add the line to Unshipped with a `*REMOVED*` prefix.
- **Making a type public** needs a decision: Supported or plumbing. A Supported type also goes into the tables above.
- **At release**, move the Unshipped lines into Shipped. Both files keep `#nullable enable` as their first line.

The lists are part of the diff, so every API change is visible in review.

## Building your own UI

- [Samples](samples): your own search endpoint, controller, query factory, suggestions and front end with analytics.
- [Widgets](widgets): overriding the views, class names, scripts and endpoints.
- [Search insights](insights) and [Search activities](activities): what the analytics feed in the admin and in contact activities.
