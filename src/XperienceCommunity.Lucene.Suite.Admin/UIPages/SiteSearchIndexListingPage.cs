using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

[assembly: UIPage(
    parentType: typeof(SiteSearchApplication),
    slug: "indexes",
    uiPageType: typeof(SiteSearchIndexListingPage),
    name: "Indexes",
    templateName: TemplateNames.LISTING,
    order: UIPageOrder.First)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Read-only list of the Lucene indexes; a row opens that index's Search tuning section.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class SiteSearchIndexListingPage(
    ILuceneClient luceneClient,
    ILuceneIndexManager indexManager,
    ILogger<SiteSearchIndexListingPage> logger) : ListingPage
{
    internal const string NoIndexesHeadline = "No Lucene indexes registered";

    private Dictionary<string, int> entries = [];

    /// <inheritdoc />
    protected override string ObjectType => LuceneIndexItemInfo.OBJECT_TYPE;

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        PageConfiguration.ColumnConfigurations
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemIndexName), "Name", sortable: true, searchable: true, defaultSortDirection: SortTypeEnum.Asc)
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemStrategyName), "Index strategy", sortable: true, searchable: true)
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemAnalyzerName), "Analyzer", sortable: true, searchable: true)
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemId), "Entries", formatter: (id, _) => FormatEntries(entries, int.TryParse(id?.ToString(), out int i) ? indexManager.GetIndex(i)?.IndexName : null));

        if (!indexManager.GetAllIndices().Any())
        {
            PageConfiguration.Callouts = [NoIndexesCallout()];
        }

        PageConfiguration.AddEditRowAction<SearchTuningSectionPage>();
        await base.ConfigurePage();
    }

    /// <inheritdoc />
    protected override async Task<LoadDataResult> LoadData(LoadDataSettings settings, CancellationToken cancellationToken)
    {
        entries = await GetEntries(cancellationToken);
        return await base.LoadData(settings, cancellationToken);
    }

    /// <summary>The live document count of the named index, or empty when statistics are unavailable.</summary>
    internal static string FormatEntries(IReadOnlyDictionary<string, int> entries, string? indexName) =>
        indexName is not null && entries.TryGetValue(indexName, out int count) ? count.ToString() : string.Empty;

    /// <summary>Shown when no index is registered, so an empty install does not render a bare table.</summary>
    internal static CalloutConfiguration NoIndexesCallout() => new()
    {
        Headline = NoIndexesHeadline,
        Content = "Register a Lucene index in the Lucene Search application; it then appears here for search tuning.",
        Type = CalloutType.QuickTip,
        Placement = CalloutPlacement.OnDesk,
    };

    private async Task<Dictionary<string, int>> GetEntries(CancellationToken cancellationToken)
    {
        try
        {
            return (await luceneClient.GetStatistics(cancellationToken))
                .Where(s => s.Name is not null)
                .DistinctBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(s => s.Name!, s => s.Entries, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An index that was never built throws here; the listing still renders, with empty counts.
            logger.LogWarning(exception, "Lucene index statistics are unavailable.");
            return [];
        }
    }
}
