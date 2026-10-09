namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>The suite's single search entry point over Kentico Lucene indexes.</summary>
public interface ISuiteSearchService
{
    /// <summary>Runs <paramref name="request"/> and returns one page of hits with facet counts and timing.</summary>
    SuiteSearchResponse Search(SuiteSearchRequest request);
}
