using System.Globalization;

using Kentico.Xperience.Lucene.Core;

using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Facet;
using Lucene.Net.Index;
using Lucene.Net.Queries;
using Lucene.Net.Search;
using Lucene.Net.Util;

using XperienceCommunity.Lucene.Suite.Rules;

namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Runs one search; <paramref name="outcome"/> (fired rules) adds hides and boosts to the query.</summary>
internal sealed class SearchExecutor(ISearchQueryFactory queryFactory, SuiteSearchOptions options, RuleOutcome? outcome = null)
{
    internal const int MaxPageSize = 100;
    internal const int MaxResultWindow = 1000;
    internal const int MaxTextLength = 256;
    internal const int MaxFacetDimensions = 20;
    internal const int MaxFacetValues = 50;
    internal const int MaxContexts = 50;
    internal const int MaxContextLength = 100;

    /// <summary>Most boosts applied to one query; each adds a nesting level, so the strongest are kept.</summary>
    internal const int MaxBoosts = 200;

    /// <summary>True when more than <see cref="MaxBoosts"/> boosts fired and the weakest were left out.</summary>
    public bool BoostsTruncated => outcome?.Boosts.Count > MaxBoosts;

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
        if (request.SearchId == Guid.Empty)
        {
            throw new ArgumentException("SearchId must not be an empty GUID.", nameof(request));
        }
        if (request.Sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Sequence, "Sequence must be 1 or greater.");
        }
        if (request.Facets is null)
        {
            throw new ArgumentException("Facets must not be null.", nameof(request));
        }
        if (request.Facets.Count > MaxFacetDimensions)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Facets.Count, $"At most {MaxFacetDimensions} facet dimensions are allowed.");
        }

        if (request.Contexts is null)
        {
            throw new ArgumentException("Contexts must not be null.", nameof(request));
        }
        if (request.Contexts.Count > MaxContexts)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Contexts.Count, $"At most {MaxContexts} contexts are allowed.");
        }
        foreach (string context in request.Contexts)
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                throw new ArgumentException("Contexts must not be empty.", nameof(request));
            }
            if (context.Length > MaxContextLength)
            {
                throw new ArgumentOutOfRangeException(nameof(request), context.Length, $"Contexts must not exceed {MaxContextLength} characters.");
            }
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

    private Query CreateQuery(SuiteSearchRequest request, Analyzer analyzer)
    {
        var query = string.IsNullOrWhiteSpace(request.Query)
            ? new MatchAllDocsQuery()
            : queryFactory.Create(request.IndexName, request.Query, analyzer);
        if (outcome is null)
        {
            return query;
        }

        // Boosts arrive ordered by field and value; nesting multiplies, so every matching boost's factor applies.
        var builder = new QueryBuilder(analyzer);
        foreach (var boost in outcome.Boosts.OrderByDescending(boost => Math.Abs(boost.Strength)).Take(MaxBoosts))
        {
            float factor = (float)Math.Pow(options.Rules.MaxBoostFactor, boost.Strength / 100.0);
            query = new BoostingQuery(query, BoostMatch(boost, builder), factor);
        }

        if (outcome.Hides.Count == 0)
        {
            return query;
        }

        var hidden = new TermsFilter(outcome.Hides.Select(guid => new Term(BaseDocumentProperties.ITEM_GUID, guid.ToString())).ToList());
        return new BooleanQuery
        {
            { query, Occur.MUST },
            { new ConstantScoreQuery(hidden), Occur.MUST_NOT },
        };
    }

    // A document matches a boost when the field holds the exact value or the analyzed value as a phrase.
    private static BooleanQuery BoostMatch(RuleBoost boost, QueryBuilder builder)
    {
        var match = new BooleanQuery { { new TermQuery(new Term(boost.Field, boost.Value)), Occur.SHOULD } };
        if (builder.CreatePhraseQuery(boost.Field, boost.Value) is { } phrase)
        {
            match.Add(phrase, Occur.SHOULD);
        }

        return match;
    }

    private static SuiteSearchResponse ToResponse(SuiteSearchRequest request, IndexSearcher searcher, TopDocs topDocs, IReadOnlyList<SuiteFacet> facets)
    {
        var hits = topDocs.ScoreDocs
            .Skip((request.Page - 1) * request.PageSize)
            .Select(scoreDoc => ToHit(searcher.Doc(scoreDoc.Doc), scoreDoc.Score))
            .ToArray();

        return new SuiteSearchResponse(hits, topDocs.TotalHits, facets, TimeSpan.Zero) { IndexDocumentCount = searcher.IndexReader.NumDocs };
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
