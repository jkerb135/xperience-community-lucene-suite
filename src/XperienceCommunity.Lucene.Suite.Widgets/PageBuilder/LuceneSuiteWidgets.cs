using CMS;
using CMS.Websites;

using Kentico.Xperience.Lucene.Core;
using Kentico.PageBuilder.Web.Mvc;
using Kentico.Xperience.Admin.Base.FormAnnotations;
using Kentico.Xperience.Admin.Websites.FormAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Widgets.Components;
using XperienceCommunity.Lucene.Suite.Widgets.PageBuilder;
using XperienceCommunity.Lucene.Suite.Widgets.Search;

[assembly: AssemblyDiscoverable]
[assembly: RegisterWidget(LuceneSuiteSearchBoxWidget.Identifier, typeof(LuceneSuiteSearchBoxWidget), "Lucene Suite search box", typeof(LuceneSuiteSearchBoxWidgetProperties),
    Description = "Search form that sends visitors to a results page.", IconClass = "icon-magnifier", AllowCache = false)]
[assembly: RegisterWidget(LuceneSuiteResultsWidget.Identifier, typeof(LuceneSuiteResultsWidget), "Lucene Suite search results", typeof(LuceneSuiteResultsWidgetProperties),
    Description = "Results of the visitor's search, with paging and click tracking.", IconClass = "icon-list-bullets", AllowCache = false)]
[assembly: RegisterWidget(LuceneSuiteFiltersWidget.Identifier, typeof(LuceneSuiteFiltersWidget), "Lucene Suite search filters", typeof(LuceneSuiteFiltersWidgetProperties),
    Description = "Facet filters for the visitor's search.", IconClass = "icon-funnel", AllowCache = false)]

namespace XperienceCommunity.Lucene.Suite.Widgets.PageBuilder;

/// <summary>Properties of the search box widget.</summary>
public sealed class LuceneSuiteSearchBoxWidgetProperties : IWidgetProperties
{
    /// <summary>Page showing the results; empty submits to the current page.</summary>
    [WebPageSelectorComponent(Label = "Results page", MaximumPages = 1, Order = 1, ExplanationText = "Page with the search results widget. Leave empty to show results on this page.")]
    public IEnumerable<WebPageRelatedItem> ResultsPage { get; set; } = [];

    /// <summary>Input label and button text.</summary>
    [TextInputComponent(Label = "Label", Order = 2)]
    public string Label { get; set; } = "Search";

    /// <summary>Input placeholder.</summary>
    [TextInputComponent(Label = "Placeholder", Order = 3)]
    public string? Placeholder { get; set; }
}

/// <summary>Properties of the search results widget.</summary>
public sealed class LuceneSuiteResultsWidgetProperties : IWidgetProperties
{
    /// <summary>Kentico Lucene index to search.</summary>
    [DropDownComponent(Label = "Index", DataProviderType = typeof(LuceneIndexOptionsProvider), Order = 1)]
    [RequiredValidationRule]
    public string IndexName { get; set; } = string.Empty;

    /// <summary>Stored field holding the hit title.</summary>
    [TextInputComponent(Label = "Title field", Order = 2)]
    public string TitleField { get; set; } = "Title";

    /// <summary>Stored field holding the hit URL.</summary>
    [TextInputComponent(Label = "URL field", Order = 3)]
    public string UrlField { get; set; } = BaseDocumentProperties.URL;

    /// <summary>Stored field holding a summary; empty shows none.</summary>
    [TextInputComponent(Label = "Summary field", Order = 4, ExplanationText = "Optional stored field shown under each title.")]
    public string? SummaryField { get; set; }
}

/// <summary>Properties of the search filters widget.</summary>
public sealed class LuceneSuiteFiltersWidgetProperties : IWidgetProperties
{
    /// <summary>Kentico Lucene index to search.</summary>
    [DropDownComponent(Label = "Index", DataProviderType = typeof(LuceneIndexOptionsProvider), Order = 1)]
    [RequiredValidationRule]
    public string IndexName { get; set; } = string.Empty;

    /// <summary>Heading labels, one "Dimension = Label" per line.</summary>
    [TextAreaComponent(Label = "Dimension labels", Order = 2, ExplanationText = "One per line, for example: ContentType = Content type")]
    public string? DimensionLabels { get; set; }
}

/// <summary>Page Builder widget wrapping <see cref="LuceneSuiteSearchBoxViewComponent"/>.</summary>
public sealed class LuceneSuiteSearchBoxWidget(SuitePageSearch search, IWebPageUrlRetriever urls, ILogger<LuceneSuiteSearchBoxWidget> logger) : ViewComponent
{
    /// <summary>Widget identifier.</summary>
    public const string Identifier = "XperienceCommunity.LuceneSuite.SearchBox";

    private static readonly LogThrottle Failures = new(TimeProvider.System, TimeSpan.FromMinutes(1));

    /// <summary>Renders the search box; the results page URL is resolved in the current page's language.</summary>
    public async Task<IViewComponentResult> InvokeAsync(ComponentViewModel<LuceneSuiteSearchBoxWidgetProperties> widget)
    {
        var properties = widget.Properties;
        string? actionUrl = null;
        if (properties.ResultsPage?.FirstOrDefault() is { } page)
        {
            try
            {
                var url = await urls.Retrieve(page.WebPageGuid, widget.Page.LanguageName, search.Preview(HttpContext).IsPreview, HttpContext.RequestAborted);
                actionUrl = url?.RelativePath is { Length: > 0 } path ? Url.Content(path) : null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (Failures.ShouldLog())
                {
                    logger.LogWarning(exception, "Lucene Suite search box could not resolve its results page {Page}; submitting to the current page.", page.WebPageGuid);
                }
            }
        }

        return View(LuceneSuiteSearchBoxViewComponent.ViewPath, LuceneSuiteSearchBoxViewComponent.Build(
            search, HttpContext, actionUrl, string.IsNullOrWhiteSpace(properties.Label) ? "Search" : properties.Label, properties.Placeholder));
    }
}

/// <summary>Page Builder widget wrapping <see cref="LuceneSuiteResultsViewComponent"/>.</summary>
public sealed class LuceneSuiteResultsWidget(SuitePageSearch search, ILogger<LuceneSuiteResultsWidget> logger) : ViewComponent
{
    /// <summary>Widget identifier.</summary>
    public const string Identifier = "XperienceCommunity.LuceneSuite.SearchResults";

    /// <summary>Renders the page's search results, or a configuration message in edit mode.</summary>
    public IViewComponentResult Invoke(ComponentViewModel<LuceneSuiteResultsWidgetProperties> widget)
    {
        var p = widget.Properties;
        if (!search.IndexExists(p.IndexName))
        {
            return WidgetIndex.Missing(this, search, logger, p.IndexName);
        }

        return View(LuceneSuiteResultsViewComponent.ViewPath, LuceneSuiteResultsViewComponent.Build(
            search, HttpContext, Url, p.IndexName, Or(p.TitleField, "Title"), Or(p.UrlField, BaseDocumentProperties.URL), string.IsNullOrWhiteSpace(p.SummaryField) ? null : p.SummaryField));
    }

    private static string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}

/// <summary>Page Builder widget wrapping <see cref="LuceneSuiteFacetsViewComponent"/>.</summary>
public sealed class LuceneSuiteFiltersWidget(SuitePageSearch search, ILogger<LuceneSuiteFiltersWidget> logger) : ViewComponent
{
    /// <summary>Widget identifier.</summary>
    public const string Identifier = "XperienceCommunity.LuceneSuite.SearchFilters";

    /// <summary>Renders the page's facet filters, or a configuration message in edit mode.</summary>
    public IViewComponentResult Invoke(ComponentViewModel<LuceneSuiteFiltersWidgetProperties> widget)
    {
        var p = widget.Properties;
        return search.IndexExists(p.IndexName)
            ? View(LuceneSuiteFacetsViewComponent.ViewPath, LuceneSuiteFacetsViewComponent.Build(search, HttpContext, p.IndexName, p.DimensionLabels))
            : WidgetIndex.Missing(this, search, logger, p.IndexName);
    }
}

/// <summary>A widget without a usable index: a message for editors, nothing for visitors.</summary>
internal static class WidgetIndex
{
    private static readonly LogThrottle Warnings = new(TimeProvider.System, TimeSpan.FromMinutes(1));

    public static IViewComponentResult Missing(ViewComponent component, SuitePageSearch search, ILogger logger, string? indexName)
    {
        if (Warnings.ShouldLog())
        {
            logger.LogWarning("A Lucene Suite widget refers to Lucene index '{Index}', which does not exist; it renders nothing.", indexName);
        }

        return component.Content(search.Preview(component.HttpContext).IsEditMode
            ? "Lucene Suite: choose an existing index in this widget's properties."
            : string.Empty);
    }
}
