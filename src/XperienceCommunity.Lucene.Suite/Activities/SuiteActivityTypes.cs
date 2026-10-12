namespace XperienceCommunity.Lucene.Suite.Activities;

/// <summary>Code names of the custom activity types the Suite installs (disabled) and logs for tracked contacts.</summary>
public static class SuiteActivityTypes
{
    /// <summary>A submitted search with a non-empty query.</summary>
    public const string Search = "lucenesuite_search";

    /// <summary>A submitted search that returned no results; logged in addition to <see cref="Search"/>.</summary>
    public const string SearchNoResults = "lucenesuite_search_noresults";

    /// <summary>A click on a search result.</summary>
    public const string ResultClick = "lucenesuite_resultclick";
}
