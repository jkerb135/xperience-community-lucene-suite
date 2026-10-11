using Kentico.Xperience.Admin.Base.FormAnnotations;
using Kentico.Xperience.Lucene.Core.Indexing;

namespace XperienceCommunity.Lucene.Suite.Widgets.PageBuilder;

/// <summary>Kentico Lucene index names for widget drop-downs, sorted.</summary>
public sealed class LuceneIndexOptionsProvider(ILuceneIndexManager indexes) : IDropDownOptionsProvider
{
    /// <inheritdoc />
    public Task<IEnumerable<DropDownOptionItem>> GetOptionItems() =>
        Task.FromResult(indexes.GetAllIndices()
            .Select(index => index.IndexName)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new DropDownOptionItem { Value = name, Text = name }));
}
