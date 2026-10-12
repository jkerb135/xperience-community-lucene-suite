using System.Globalization;

using CMS.DataEngine;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Surfaced membership in SQL, shared by reports and suggestions so both name exactly the same queries.</summary>
internal static class Surfacing
{
    /// <summary>The QueryText of every query of <paramref name="indexName"/> that meets the surfacing threshold over all kept days, for use in a WhereIn.</summary>
    public static ObjectQuery<SearchDailyInfo> Query(IInfoProvider<SearchDailyInfo> daily, string indexName, SuiteSurfacingOptions surfacing) =>
        daily.Get()
            .Column(nameof(SearchDailyInfo.QueryText))
            .WhereEquals(nameof(SearchDailyInfo.IndexName), indexName)
            .WhereNotNull(nameof(SearchDailyInfo.QueryText))
            .GroupBy(nameof(SearchDailyInfo.QueryText))
            // Rows are unique per (Day, IndexName, QueryText) under the collation, so COUNT(*) is the number of distinct days. Values are validated ints.
            .Having(string.Create(CultureInfo.InvariantCulture, $"SUM([Searches]) >= {surfacing.MinSearches} AND COUNT(*) >= {surfacing.MinDistinctDays}"));
}
