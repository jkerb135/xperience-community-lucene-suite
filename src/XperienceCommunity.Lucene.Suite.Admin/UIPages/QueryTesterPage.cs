using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Search;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "query-tester",
    uiPageType: typeof(QueryTesterPage),
    name: "Query tester",
    templateName: "@xperience-community/lucene-suite/QueryTester",
    order: 100)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Runs ad-hoc searches against one index through <see cref="ISuiteSearchService"/>.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class QueryTesterPage(
    ILuceneIndexManager indexManager,
    IServiceProvider serviceProvider,
    ISuiteSearchService searchService,
    IOptions<SuiteSearchOptions> options) : Page<QueryTesterClientProperties>
{
    // Mirror SearchExecutor's limits so the pager never requests a rejected window.
    private const int PageSize = 10;
    private const int MaxResultWindow = 1000;

    /// <summary>Identifier of the searched Lucene index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <summary>Repeats the section's index check: Kentico validates only the requested page, never its parents, for page loads and commands alike.</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override Task<QueryTesterClientProperties> ConfigureTemplateProperties(QueryTesterClientProperties properties)
    {
        var index = GetIndex();
        var strategy = (ILuceneIndexingStrategy)serviceProvider.GetRequiredService(index.LuceneIndexingStrategyType);

        properties.IndexName = index.IndexName;
        properties.IsFaceted = strategy.FacetsConfigFactory() is not null;
        properties.PageSize = PageSize;
        properties.MaxResultWindow = MaxResultWindow;
        properties.KeyFields = options.Value.SearchFields.TryGetValue(index.IndexName, out var fields) ? [.. fields.Keys] : [];
        return Task.FromResult(properties);
    }

    /// <summary>Searches the index; invalid input comes back as <see cref="QueryTesterResult.Error"/> instead of a server error.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public Task<ICommandResponse<QueryTesterResult>> Search(QueryTesterArguments arguments)
    {
        try
        {
            var response = searchService.Search(new SuiteSearchRequest
            {
                IndexName = GetIndex().IndexName,
                Query = arguments.Query,
                Facets = (arguments.Facets ?? []).ToDictionary(f => f.Key, f => (IReadOnlyList<string>)(f.Value ?? [])),
                Page = arguments.Page,
                PageSize = PageSize,
                TrackAnalytics = false,
            });

            return Task.FromResult(ResponseFrom(new QueryTesterResult
            {
                Hits = response.Hits,
                TotalHits = response.TotalHits,
                Facets = response.Facets,
                ElapsedMilliseconds = response.Elapsed.TotalMilliseconds,
            }));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            var message = ValidationMessage(exception);
            return Task.FromResult(ResponseFrom(new QueryTesterResult { Error = message }).AddErrorMessage(message));
        }
    }

    /// <summary>The exception's own message, without the runtime-appended parameter name and actual value.</summary>
    internal static string ValidationMessage(Exception exception)
    {
        // Rebuilding the suffix from an empty-message twin keeps this correct under any runtime resource language.
        var suffix = exception switch
        {
            ArgumentOutOfRangeException e => new ArgumentOutOfRangeException(e.ParamName, e.ActualValue, string.Empty).Message,
            ArgumentException e => new ArgumentException(string.Empty, e.ParamName).Message,
            _ => string.Empty,
        };

        return suffix.Length > 0 && exception.Message.EndsWith(suffix, StringComparison.Ordinal)
            ? exception.Message[..^suffix.Length]
            : exception.Message;
    }

    private LuceneIndex GetIndex() =>
        indexManager.GetIndex(IndexIdentifier) ?? throw new InvalidOperationException($"Lucene index {IndexIdentifier} does not exist.");
}

/// <summary>Template properties of the query tester.</summary>
public sealed class QueryTesterClientProperties : TemplateClientProperties
{
    /// <summary>Name of the searched index.</summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>Whether the index's strategy defines facets.</summary>
    public bool IsFaceted { get; set; }

    /// <summary>Hits per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Highest reachable hit position (page × page size).</summary>
    public int MaxResultWindow { get; set; }

    /// <summary>Configured searchable fields, shown as result columns.</summary>
    public IReadOnlyList<string> KeyFields { get; set; } = [];
}

/// <summary>Arguments of the query tester's Search command.</summary>
public sealed class QueryTesterArguments
{
    /// <summary>Query text; empty lists every document.</summary>
    public string? Query { get; set; }

    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Selected facet values per dimension.</summary>
    public Dictionary<string, List<string>>? Facets { get; set; }
}

/// <summary>Result of the query tester's Search command.</summary>
public sealed class QueryTesterResult
{
    /// <summary>Hits on the requested page.</summary>
    public IReadOnlyList<SuiteSearchHit> Hits { get; init; } = [];

    /// <summary>Number of matching documents.</summary>
    public int TotalHits { get; init; }

    /// <summary>Facet counts; empty for non-faceted indexes.</summary>
    public IReadOnlyList<SuiteFacet> Facets { get; init; } = [];

    /// <summary>Search time in milliseconds.</summary>
    public double ElapsedMilliseconds { get; init; }

    /// <summary>Validation message when the request was rejected; otherwise null.</summary>
    public string? Error { get; init; }
}
