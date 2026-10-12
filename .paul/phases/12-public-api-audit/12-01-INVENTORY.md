# 12-01 Public API inventory

Source: `Assembly.GetExportedTypes()` on the Debug builds (includes nested public types; there are none), before and after 12-01.

- **Supported**: documented for site developers; frozen by PublicAPI.*.txt.
- **Framework-required**: public only because Kentico or ASP.NET Core needs it; not supported for direct use.
- **Internalize**: no external use; made `internal` in 12-01.

## Counts

| Package | Before | Supported | Framework-required | Internalized | Added | After |
|---|---|---|---|---|---|---|
| XperienceCommunity.Lucene.Suite | 50 | 25 | 9 | 16 | 1 | 35 |
| XperienceCommunity.Lucene.Suite.Admin | 86 | 1 | 85 | 0 | 0 | 86 |
| XperienceCommunity.Lucene.Suite.Widgets | 27 | 22 | 4 | 1 | 0 | 26 |
| **Total** | **163** | **48** | **98** | **17** | **1** | **147** |

Supported/Framework-required/Internalized classify the *before* types (they sum to Before); After = Before − Internalized + Added, and equals the reflected count after the changes.

Other surface changes:

- Added: `SearchClick` (Core, Supported); `ISearchInteractionLogger.LogClick(SearchClick)` replaces the 7-parameter overload.
- Namespace moves (not additions): `Widgets.Events.SuiteEventsEndpoints` → `Widgets.SuiteEventsEndpoints`, `Widgets.Suggestions.SuiteSuggestEndpoints` → `Widgets.SuiteSuggestEndpoints`.
- Members made internal on public types: `RuleInfo.ToDefinition()`, `SynonymInfo.ToDefinition()` (they returned the internalized parse results).

## XperienceCommunity.Lucene.Suite

| Type (before) | Kind | Class | Reason |
|---|---|---|---|
| `XperienceCommunity.Lucene.Suite.Activities.SuiteActivityTypes` | static class | Supported | Activity type code names, docs/activities.md |
| `XperienceCommunity.Lucene.Suite.Analytics.ISearchInteractionLogger` | interface | Supported | Custom-UI analytics API, analytics-client-contract.md §3 |
| `XperienceCommunity.Lucene.Suite.Analytics.SearchDailyInfo` | class | Framework-required | Kentico object type ([assembly: RegisterObjectType]) |
| `XperienceCommunity.Lucene.Suite.Analytics.SearchEventInfo` | class | Framework-required | Kentico object type ([assembly: RegisterObjectType]) |
| `XperienceCommunity.Lucene.Suite.Analytics.SearchEventOrigin` | enum | Framework-required | Property type of public SearchEventInfo |
| `XperienceCommunity.Lucene.Suite.Analytics.SearchInteractionInfo` | class | Framework-required | Kentico object type ([assembly: RegisterObjectType]) |
| `XperienceCommunity.Lucene.Suite.Analytics.SearchInteractionType` | enum | Framework-required | Property type of public SearchInteractionInfo |
| `XperienceCommunity.Lucene.Suite.LuceneSuiteServiceCollectionExtensions` | static class | Supported | AddLuceneSuite, getting-started.md |
| `XperienceCommunity.Lucene.Suite.Rules.RuleActions` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleAnchoring` | enum | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleBoost` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleCondition` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleDefinition` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleError` | record | Framework-required | Element of RuleJsonValidationResult, a page-command result serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Rules.RuleFacet` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleInfo` | class | Framework-required | Kentico object type ([assembly: RegisterObjectType]) |
| `XperienceCommunity.Lucene.Suite.Rules.RuleLimits` | static class | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleMetadata` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleParseResult` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RulePin` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Rules.RuleSerializer` | static class | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Search.DefaultSearchQueryFactory` | class | Supported | Default/fallback for a custom query factory, samples.md (c) |
| `XperienceCommunity.Lucene.Suite.Search.ISearchQueryFactory` | interface | Supported | Extension point, samples.md (c) |
| `XperienceCommunity.Lucene.Suite.Search.ISuiteSearchService` | interface | Supported | Search API, samples.md (b) |
| `XperienceCommunity.Lucene.Suite.Search.SuiteAnalyticsOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteAppliedSynonym` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteAttributionOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteFacet` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteFacetValue` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteInsightsOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteJobOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteMergeOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteRetentionOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteRuleUserData` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteRulesOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSearchHit` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSearchOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSearchRequest` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSearchResponse` | record | Supported | Search request/response model, samples.md / rules-and-synonyms.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSuggestionOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSurfacingOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Search.SuiteSynonymsOptions` | class | Supported | Options bound from the LuceneSuite appsettings section, configuration.md |
| `XperienceCommunity.Lucene.Suite.Suggestions.BlockedSuggestionInfo` | class | Framework-required | Kentico object type ([assembly: RegisterObjectType]) |
| `XperienceCommunity.Lucene.Suite.Suggestions.ISuiteSuggestions` | interface | Supported | Suggestions API, suggestions.md |
| `XperienceCommunity.Lucene.Suite.Synonyms.SynonymDefinition` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Synonyms.SynonymInfo` | class | Framework-required | Kentico object type ([assembly: RegisterObjectType]) |
| `XperienceCommunity.Lucene.Suite.Synonyms.SynonymKind` | enum | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Synonyms.SynonymLimits` | static class | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Synonyms.SynonymParseResult` | record | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Synonyms.SynonymSerializer` | static class | Internalize | Rule/synonym model or serializer: used only inside the Suite and its admin (InternalsVisibleTo); not documented |
| `XperienceCommunity.Lucene.Suite.Analytics.SearchClick` (added) | record | Supported | LogClick argument, replaces the 7 parameters |

## XperienceCommunity.Lucene.Suite.Admin

| Type (before) | Kind | Class | Reason |
|---|---|---|---|
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleBoostsComponentAttribute` | class | Framework-required | Form component attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleBoostsFormComponent` | class | Framework-required | Form component, discovered by [ComponentAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleConditionsComponentAttribute` | class | Framework-required | Form component attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleConditionsFormComponent` | class | Framework-required | Form component, discovered by [ComponentAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleEditModel` | class | Framework-required | ModelEditPage model (generic argument of a public page) |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHiddenClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHiddenComponentAttribute` | class | Framework-required | Form component attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHiddenFormComponent` | class | Framework-required | Form component, discovered by [ComponentAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHidesComponentAttribute` | class | Framework-required | Form component attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHidesConflictValidationRule` | class | Framework-required | Validation rule, discovered by [ValidationRuleAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHidesConflictValidationRuleAttribute` | class | Framework-required | Validation rule attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHidesConflictValidationRuleProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleHidesFormComponent` | class | Framework-required | Form component, discovered by [ComponentAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleItem` | record | Framework-required | Element of RuleItemsResult (page-command result) |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleItemDescribeArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleItemSearchArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleItemsClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleItemsFormComponent` | class | Framework-required | Base class of the registered pins/hides form components |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleItemsResult` | record | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonSection` | enum | Framework-required | Parameter/property type of the public RuleJson validation attribute and properties |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonValidationArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonValidationResult` | record | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonValidationRule` | class | Framework-required | Validation rule, discovered by [ValidationRuleAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonValidationRuleAttribute` | class | Framework-required | Validation rule attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleJsonValidationRuleProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RulePinsComponentAttribute` | class | Framework-required | Form component attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RulePinsFormComponent` | class | Framework-required | Form component, discovered by [ComponentAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleValidityValidationRule` | class | Framework-required | Validation rule, discovered by [ValidationRuleAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleValidityValidationRuleAttribute` | class | Framework-required | Validation rule attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.RuleValidityValidationRuleProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Rules.ServerValidatedClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.SiteSearchPermissions` | static class | Supported | Permission code names (getting-started.md), for host admin pages that check them |
| `XperienceCommunity.Lucene.Suite.Admin.Suggestions.BlockedSuggestionEditModel` | class | Framework-required | ModelEditPage model (generic argument of a public page) |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymEditModel` | class | Framework-required | ModelEditPage model (generic argument of a public page) |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymInputValidationRule` | class | Framework-required | Validation rule, discovered by [ValidationRuleAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymInputValidationRuleAttribute` | class | Framework-required | Validation rule attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymKindValidationRule` | class | Framework-required | Validation rule, discovered by [ValidationRuleAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymKindValidationRuleAttribute` | class | Framework-required | Validation rule attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymTermsClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymTermsComponentAttribute` | class | Framework-required | Form component attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymTermsFormComponent` | class | Framework-required | Form component, discovered by [ComponentAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymTermsValidationArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymTermsValidationRule` | class | Framework-required | Validation rule, discovered by [ValidationRuleAttribute] |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymTermsValidationRuleAttribute` | class | Framework-required | Validation rule attribute on a public ModelEditPage model property |
| `XperienceCommunity.Lucene.Suite.Admin.Synonyms.SynonymValidationRuleProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.BlockSuggestionArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.BlockSuggestionResult` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.BlockedSuggestionCreatePage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.BlockedSuggestionListingPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.IndexListingPageExtender` | class | Framework-required | Registered with [assembly: PageExtender] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterAppliedSynonym` | record | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterContactGroup` | record | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterContactGroups` | record | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterFiredRule` | record | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.QueryTesterResult` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.RuleCreatePage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.RuleEditPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.RuleEditPageBase` | class | Framework-required | Base class of registered admin pages |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.RuleListingPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchInsightRow` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchInsightRule` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchInsightSynonym` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchInsightsResult` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsClientProperties` | class | Framework-required | Client/validation properties serialized to the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsDay` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsDeltas` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsLatency` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsPercentile` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsQueriesArguments` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsQueriesResult` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsQueryRow` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsResult` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchReportsTotals` | class | Framework-required | Page-command argument/result DTO serialized to/from the admin client |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SearchTuningSectionPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SiteSearchApplication` | class | Framework-required | Admin application, [assembly: UIApplication] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SiteSearchIndexListingPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SynonymCreatePage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SynonymEditPage` | class | Framework-required | Admin page, [assembly: UIPage] |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SynonymEditPageBase` | class | Framework-required | Base class of registered admin pages |
| `XperienceCommunity.Lucene.Suite.Admin.UIPages.SynonymListingPage` | class | Framework-required | Admin page, [assembly: UIPage] |

## XperienceCommunity.Lucene.Suite.Widgets

| Type (before) | Kind | Class | Reason |
|---|---|---|---|
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteFacetGroup` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteFacetOption` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteFacetsModel` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteFacetsViewComponent` | class | Supported | Used as <vc:lucene-suite-*> (widgets.md); must be public to be discovered |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuitePageLink` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteResultHit` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteResultsModel` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteResultsViewComponent` | class | Supported | Used as <vc:lucene-suite-*> (widgets.md); must be public to be discovered |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteSearchBoxModel` | record | Supported | View model of an overridable view (widgets.md "Overriding the markup") |
| `XperienceCommunity.Lucene.Suite.Widgets.Components.LuceneSuiteSearchBoxViewComponent` | class | Supported | Used as <vc:lucene-suite-*> (widgets.md); must be public to be discovered |
| `XperienceCommunity.Lucene.Suite.Widgets.Events.SuiteClientSettings` | record | Internalize | Only serialized by GET {events}/settings; the JSON shape (lucene-suite.js typedef) is the contract, not the C# type |
| `XperienceCommunity.Lucene.Suite.Widgets.Events.SuiteEventsEndpoints` | static class | Supported | MapLuceneSuiteEvents, widgets.md; namespace moved to ...Widgets |
| `XperienceCommunity.Lucene.Suite.Widgets.Events.SuiteResultReference` | struct | Supported | Conversions on a later page, samples.md (e) |
| `XperienceCommunity.Lucene.Suite.Widgets.LuceneSuiteWidgetsServiceCollectionExtensions` | static class | Supported | AddLuceneSuiteWidgets, getting-started.md |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneIndexOptionsProvider` | class | Framework-required | Drop-down options provider referenced by the widget properties (activated by Kentico) |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneSuiteFiltersWidget` | class | Framework-required | Page Builder widget ([assembly: RegisterWidget]); view components must be public to be discovered |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneSuiteFiltersWidgetProperties` | class | Supported | Widget properties (customization contract) |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneSuiteResultsWidget` | class | Framework-required | Page Builder widget ([assembly: RegisterWidget]); view components must be public to be discovered |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneSuiteResultsWidgetProperties` | class | Supported | Widget properties (customization contract) |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneSuiteSearchBoxWidget` | class | Framework-required | Page Builder widget ([assembly: RegisterWidget]); view components must be public to be discovered |
| `XperienceCommunity.Lucene.Suite.Widgets.PageBuilder.LuceneSuiteSearchBoxWidgetProperties` | class | Supported | Widget properties (customization contract) |
| `XperienceCommunity.Lucene.Suite.Widgets.Search.SuitePageSearch` | class | Supported | Public constructor parameter of the view components and widgets; the per-request search (analytics-client-contract.md) |
| `XperienceCommunity.Lucene.Suite.Widgets.Search.SuitePageSearchResult` | record | Supported | Search property of the overridable views' models |
| `XperienceCommunity.Lucene.Suite.Widgets.Search.SuitePageSearchStatus` | enum | Supported | Used by the overridable results view |
| `XperienceCommunity.Lucene.Suite.Widgets.Search.SuiteSearchUrl` | record | Supported | SuitePageSearchResult.Url; used by the overridable search-box view |
| `XperienceCommunity.Lucene.Suite.Widgets.Suggestions.SuiteSuggestEndpoints` | static class | Supported | MapLuceneSuiteSuggestions, widgets.md; namespace moved to ...Widgets |
| `XperienceCommunity.Lucene.Suite.Widgets.SuiteWidgetsOptions` | class | Supported | Options bound from LuceneSuite:Widgets, configuration.md |

