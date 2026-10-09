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
    private const string EntriesCaption = "Entries";

    /// <inheritdoc />
    protected override string ObjectType => LuceneIndexItemInfo.OBJECT_TYPE;

    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        PageConfiguration.ColumnConfigurations
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemIndexName), "Name", sortable: true, searchable: true, defaultSortDirection: SortTypeEnum.Asc)
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemStrategyName), "Index strategy", sortable: true, searchable: true)
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemAnalyzerName), "Analyzer", sortable: true, searchable: true)
            // Placeholder column; LoadData replaces its value with the live document count.
            .AddColumn(nameof(LuceneIndexItemInfo.LuceneIndexItemId), EntriesCaption);

        PageConfiguration.AddEditRowAction<SearchTuningSectionPage>();
        await base.ConfigurePage();
    }

    /// <inheritdoc />
    protected override async Task<LoadDataResult> LoadData(LoadDataSettings settings, CancellationToken cancellationToken)
    {
        var result = await base.LoadData(settings, cancellationToken);
        if (PageConfiguration.ColumnConfigurations is not List<ColumnConfiguration> columns)
        {
            return result;
        }

        var entries = await GetEntries(cancellationToken);
        int entriesColumn = columns.FindIndex(c => c.Caption == EntriesCaption);
        foreach (var row in result.Rows)
        {
            if (row.Cells is List<Cell> cells && cells[entriesColumn] is StringCell cell)
            {
                string? name = int.TryParse(row.Identifier?.ToString(), out int id) ? indexManager.GetIndex(id)?.IndexName : null;
                cell.Value = name is not null && entries.TryGetValue(name, out int count) ? count.ToString() : string.Empty;
            }
        }

        return result;
    }

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
