using System.Globalization;

using Kentico.Xperience.Lucene.Core;

using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Facet;
using Lucene.Net.Search;

namespace XperienceCommunity.Lucene.Suite.Search;

internal sealed class SearchExecutor(ISearchQueryFactory queryFactory, SuiteSearchOptions options)
{
    internal const int MaxPageSize = 100;
    internal const int MaxResultWindow = 1000;
    internal const int MaxTextLength = 256;
    internal const int MaxFacetDimensions = 20;
    internal const int MaxFacetValues = 50;

    public static void Validate(SuiteSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            throw new ArgumentException("IndexName is required.", nameof(request));
        }
        if (request.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Page, "Page must be 1 or greater.");
        }
        if (request.PageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.PageSize, $"PageSize must be between 1 and {MaxPageSize}.");
        }
        if ((long)request.Page * request.PageSize > MaxResultWindow)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Page, $"Page × PageSize must not exceed {MaxResultWindow}.");
        }
        if (request.Query?.Length > MaxTextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Query.Length, $"Query must not exceed {MaxTextLength} characters.");
        }
        if (request.Facets is null)
        {
            throw new ArgumentException("Facets must not be null.", nameof(request));
        }
        if (request.Facets.Count > MaxFacetDimensions)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Facets.Count, $"At most {MaxFacetDimensions} facet dimensions are allowed.");
        }

        int valueCount = 0;
        foreach (var (dimension, values) in request.Facets)
        {
            if (string.IsNullOrWhiteSpace(dimension))
            {
                throw new ArgumentException("Facet dimensions must not be empty.", nameof(request));
            }
            if (values is null || values.Count == 0)
            {
                throw new ArgumentException($"Facet dimension '{dimension}' needs at least one value.", nameof(request));
            }
            valueCount += values.Count;
            if (valueCount > MaxFacetValues)
            {
                throw new ArgumentOutOfRangeException(nameof(request), valueCount, $"At most {MaxFacetValues} facet values are allowed.");
            }
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Facet dimension '{dimension}' has an empty value.", nameof(request));
                }
                if (value.Length > MaxTextLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(request), value.Length, $"Facet values must not exceed {MaxTextLength} characters.");
                }
            }
        }
    }

    public SuiteSearchResponse Search(SuiteSearchRequest request, Analyzer analyzer, IndexSearcher searcher)
    {
        Validate(request);
        if (request.Facets.Count > 0)
        {
            throw new ArgumentException($"Index '{request.IndexName}' has no facets, so facet filters cannot be applied.", nameof(request));
        }

        var topDocs = searcher.Search(CreateQuery(request, analyzer), request.Page * request.PageSize);
        return ToResponse(request, searcher, topDocs, []);
    }

    public SuiteSearchResponse Search(SuiteSearchRequest request, Analyzer analyzer, IndexSearcher searcher, DrillSideways drillSideways, FacetsConfig facetsConfig)
    {
        Validate(request);

        var query = new DrillDownQuery(facetsConfig, CreateQuery(request, analyzer));
        foreach (var (dimension, values) in request.Facets)
        {
            foreach (string value in values)
            {
                query.Add(dimension, value);
            }
        }

        var result = drillSideways.Search(query, request.Page * request.PageSize);
        var facets = result.Facets.GetAllDims(options.FacetValuesPerDimension)
            .Where(facet => facet is not null)
            .Select(facet => new SuiteFacet(
                facet.Dim,
                facet.LabelValues.Select(lv => new SuiteFacetValue(lv.Label, (int)lv.Value)).ToArray()))
            .ToArray();

        return ToResponse(request, searcher, result.Hits, facets);
    }

    private Query CreateQuery(SuiteSearchRequest request, Analyzer analyzer) =>
        string.IsNullOrWhiteSpace(request.Query)
            ? new MatchAllDocsQuery()
            : queryFactory.Create(request.IndexName, request.Query, analyzer);

    private static SuiteSearchResponse ToResponse(SuiteSearchRequest request, IndexSearcher searcher, TopDocs topDocs, IReadOnlyList<SuiteFacet> facets)
    {
        var hits = topDocs.ScoreDocs
            .Skip((request.Page - 1) * request.PageSize)
            .Select(scoreDoc => ToHit(searcher.Doc(scoreDoc.Doc), scoreDoc.Score))
            .ToArray();

        return new SuiteSearchResponse(hits, topDocs.TotalHits, facets, TimeSpan.Zero);
    }

    private static SuiteSearchHit ToHit(Document document, float score)
    {
        var fields = document.Fields
            .Select(field => (field.Name, Value: field.GetStringValue(CultureInfo.InvariantCulture)))
            .Where(field => field.Value is not null)
            .GroupBy(field => field.Name)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(field => field.Value!).ToArray());

        Guid? itemGuid = Guid.TryParse(document.Get(BaseDocumentProperties.ITEM_GUID), out var guid) ? guid : null;

        return new SuiteSearchHit(itemGuid, score, fields);
    }
}
