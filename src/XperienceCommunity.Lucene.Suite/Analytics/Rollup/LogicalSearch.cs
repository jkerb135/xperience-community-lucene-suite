namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>One logical search: the deduplicated rows of one SearchId between two boundaries, ordered by Sequence.</summary>
internal sealed record LogicalSearch(IReadOnlyList<RawSearchRow> Rows)
{
    /// <summary>The row that decides the day, index and duration cap.</summary>
    public RawSearchRow First => Rows[0];
}
