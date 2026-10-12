using System.Text.Json;

using CMS.ContactManagement;
using CMS.DataEngine;
using CMS.Membership;
using CMS.Membership.Internal;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Synonyms;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "query-tester",
    uiPageType: typeof(QueryTesterPage),
    name: "Query tester",
    templateName: "@xperience-community/lucene-suite/QueryTester",
    order: 100,
    Icon = Icons.Magnifier)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Runs ad-hoc searches against one index through <see cref="ISuiteSearchService"/>.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class QueryTesterPage(
    ILuceneIndexManager indexManager,
    IServiceProvider serviceProvider,
    ISuiteSearchService searchService,
    IOptions<SuiteSearchOptions> options,
    IInfoProvider<RuleInfo> rules,
    IInfoProvider<SynonymInfo> synonyms,
    IInfoProvider<ContactGroupInfo> contactGroups,
    IAuthenticatedUserAccessor userAccessor,
    IApplicationPermissionEvaluator applicationPermissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : Page<QueryTesterClientProperties>
{
    // Mirror SearchExecutor's limits so the pager never requests a rejected window.
    private const int PageSize = 10;
    internal const int MaxPageSize = 50;
    internal const int MaxContactGroups = 500;

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

        properties.IsFaceted = strategy.FacetsConfigFactory() is not null;
        properties.PageSize = PageSize;
        properties.MaxResultWindow = MaxResultWindow;
        properties.KeyFields = options.Value.SearchFields.TryGetValue(index.IndexName, out var fields) ? [.. fields.Keys] : [];
        properties.MaxContexts = SearchExecutor.MaxContexts;
        properties.MaxContextLength = SearchExecutor.MaxContextLength;
        return Task.FromResult(properties);
    }

    /// <summary>Searches the index; invalid input comes back as <see cref="QueryTesterResult.Error"/> instead of a server error.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public Task<ICommandResponse<QueryTesterResult>> Search(QueryTesterArguments arguments)
    {
        try
        {
            string indexName = GetIndex().IndexName;
            var response = searchService.Search(Request(arguments, indexName));

            return Task.FromResult(ResponseFrom(new QueryTesterResult
            {
                Hits = response.Hits,
                TotalHits = response.TotalHits,
                Facets = response.Facets,
                ElapsedMilliseconds = response.Elapsed.TotalMilliseconds,
                RulesFailed = response.RulesFailed,
                FiredRules = FiredRules(response, RuleNames(response.FiredRuleIds, indexName), EditUrl),
                SynonymsFailed = response.SynonymsFailed,
                AppliedSynonyms = AppliedSynonyms(response, ExistingSynonyms(response.AppliedSynonyms, indexName), SynonymEditUrl),
            }));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(ResponseFrom(new QueryTesterResult { Error = ValidationMessage(exception) }));
        }
    }

    /// <summary>Contact groups the compare mode can simulate; empty unless the user may view Kentico's Contact groups application.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public async Task<ICommandResponse<QueryTesterContactGroups>> ContactGroups()
    {
        if (!await CanViewContactGroups())
        {
            return ResponseFrom(ContactGroupsResult(allowed: false, []));
        }

        return ResponseFrom(ContactGroupsResult(allowed: true, LoadContactGroups(contactGroups)));
    }

    /// <summary>Contact groups that are not recipient lists, by display name, one more than shown so truncation is detectable.</summary>
    internal static List<QueryTesterContactGroup> LoadContactGroups(IInfoProvider<ContactGroupInfo> contactGroups) =>
        contactGroups.Get()
            .WhereFalse(nameof(ContactGroupInfo.ContactGroupIsRecipientList))
            .Columns(nameof(ContactGroupInfo.ContactGroupName), nameof(ContactGroupInfo.ContactGroupDisplayName))
            .OrderBy(nameof(ContactGroupInfo.ContactGroupDisplayName))
            .TopN(MaxContactGroups + 1)
            .Select(group => new QueryTesterContactGroup(group.ContactGroupName, group.ContactGroupDisplayName))
            .ToList();

    /// <summary>Builds the suite request; the tester never records analytics.</summary>
    /// <exception cref="ArgumentOutOfRangeException">PageSize is outside 1–<see cref="MaxPageSize"/>.</exception>
    internal static SuiteSearchRequest Request(QueryTesterArguments arguments, string indexName)
    {
        if (arguments.PageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), arguments.PageSize, $"PageSize must be between 1 and {MaxPageSize}.");
        }

        return new SuiteSearchRequest
        {
            IndexName = indexName,
            Query = arguments.Query,
            Facets = (arguments.Facets ?? []).ToDictionary(f => f.Key, f => (IReadOnlyList<string>)(f.Value ?? [])),
            Page = arguments.Page,
            PageSize = arguments.PageSize,
            TrackAnalytics = false,
            ApplyRules = arguments.ApplyRules,
            ApplySynonyms = arguments.ApplySynonyms,
            Contexts = arguments.Contexts ?? [],
        };
    }

    /// <summary>At most <see cref="MaxContactGroups"/> groups; <paramref name="fetched"/> holds one more when there are more.</summary>
    internal static QueryTesterContactGroups ContactGroupsResult(bool allowed, IReadOnlyList<QueryTesterContactGroup> fetched) =>
        new(allowed, fetched.Count > MaxContactGroups, [.. fetched.Take(MaxContactGroups)]);

    private async Task<bool> CanViewContactGroups()
    {
        var user = await userAccessor.Get();
        return user is not null && (user.IsAdministrator() || applicationPermissionEvaluator.Evaluate(new ApplicationPermissionEvaluationContext
        {
            ApplicationName = ContactGroupsApplication.IDENTIFIER,
            PermissionName = SystemPermissions.VIEW,
            User = user,
        }));
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

    /// <summary>Fired rules in run order, with their custom data; a rule missing from <paramref name="names"/> was deleted since and gets no name or link.</summary>
    internal static IReadOnlyList<QueryTesterFiredRule> FiredRules(SuiteSearchResponse response, IReadOnlyDictionary<int, string> names, Func<int, string> editUrl) =>
        [.. response.FiredRuleIds.Select(id => new QueryTesterFiredRule(
            id,
            names.GetValueOrDefault(id),
            names.ContainsKey(id) ? editUrl(id) : null,
            response.UserData.FirstOrDefault(data => data.RuleId == id)?.Data))];

    /// <summary>Applied synonyms in order; one missing from <paramref name="existing"/> was deleted since and gets no link.</summary>
    internal static IReadOnlyList<QueryTesterAppliedSynonym> AppliedSynonyms(SuiteSearchResponse response, IReadOnlySet<int> existing, Func<int, string> editUrl) =>
        [.. response.AppliedSynonyms.Select(applied => new QueryTesterAppliedSynonym(
            applied.SynonymId,
            applied.Term,
            applied.Replacement,
            existing.Contains(applied.SynonymId) ? editUrl(applied.SynonymId) : null))];

    private HashSet<int> ExistingSynonyms(IReadOnlyList<SuiteAppliedSynonym> applied, string indexName) =>
        applied.Count == 0
            ? []
            : [.. synonyms.Get()
                .WhereIn(nameof(SynonymInfo.SynonymID), [.. applied.Select(synonym => synonym.SynonymId).Distinct()])
                .WhereEquals(nameof(SynonymInfo.IndexName), indexName)
                .Columns(nameof(SynonymInfo.SynonymID))
                .Select(synonym => synonym.SynonymID)];

    private string SynonymEditUrl(int synonymId) => SynonymEditUrl(pageLinkGenerator, IndexIdentifier, synonymId);

    /// <summary>Admin path of a synonym's edit page under the index's Search tuning section.</summary>
    internal static string SynonymEditUrl(IPageLinkGenerator links, int indexIdentifier, int synonymId) => links.GetPath<SynonymEditPage>(new PageParameterValues
    {
        { typeof(SearchTuningSectionPage), indexIdentifier },
        { typeof(SynonymEditPage), synonymId },
    });

    private Dictionary<int, string> RuleNames(IReadOnlyList<int> ids, string indexName) =>
        ids.Count == 0
            ? []
            : rules.Get()
                .WhereIn(nameof(RuleInfo.RuleID), [.. ids])
                .WhereEquals(nameof(RuleInfo.IndexName), indexName)
                .Columns(nameof(RuleInfo.RuleID), nameof(RuleInfo.DisplayName))
                .ToDictionary(rule => rule.RuleID, rule => rule.DisplayName);

    private string EditUrl(int ruleId) => RuleEditUrl(pageLinkGenerator, IndexIdentifier, ruleId);

    /// <summary>Admin path of a rule's edit page under the index's Search tuning section.</summary>
    internal static string RuleEditUrl(IPageLinkGenerator links, int indexIdentifier, int ruleId) => links.GetPath<RuleEditPage>(new PageParameterValues
    {
        { typeof(SearchTuningSectionPage), indexIdentifier },
        { typeof(RuleEditPage), ruleId },
    });

    private LuceneIndex GetIndex() =>
        indexManager.GetIndex(IndexIdentifier) ?? throw new InvalidOperationException($"Lucene index {IndexIdentifier} does not exist.");
}

/// <summary>Template properties of the query tester.</summary>
public sealed class QueryTesterClientProperties : TemplateClientProperties
{
    /// <summary>Whether the index's strategy defines facets.</summary>
    public bool IsFaceted { get; set; }

    /// <summary>Hits per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Highest reachable hit position (page × page size).</summary>
    public int MaxResultWindow { get; set; }

    /// <summary>Configured searchable fields, shown as result columns.</summary>
    public IReadOnlyList<string> KeyFields { get; set; } = [];

    /// <summary>Most contexts per search (server limit).</summary>
    public int MaxContexts { get; set; }

    /// <summary>Most characters per context (server limit).</summary>
    public int MaxContextLength { get; set; }
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

    /// <summary>Whether rules apply to the search.</summary>
    public bool ApplyRules { get; set; } = true;

    /// <summary>Whether synonyms expand the query.</summary>
    public bool ApplySynonyms { get; set; } = true;

    /// <summary>Simulated contexts (contact group code names or free text), sent as the site would.</summary>
    public List<string>? Contexts { get; set; }

    /// <summary>Hits per page, 1–50.</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>Contact groups for the compare mode's context picker.</summary>
/// <param name="Allowed">False when the user may not view Kentico's Contact groups application.</param>
/// <param name="Truncated">True when more groups exist than were returned.</param>
/// <param name="Groups">Groups ordered by display name.</param>
public sealed record QueryTesterContactGroups(bool Allowed, bool Truncated, IReadOnlyList<QueryTesterContactGroup> Groups);

/// <summary>A contact group as the picker shows it.</summary>
/// <param name="Name">Code name, sent as the context.</param>
/// <param name="DisplayName">Name shown to marketers.</param>
public sealed record QueryTesterContactGroup(string Name, string DisplayName);

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

    /// <summary>True when rules could not be applied, so these results are unruled.</summary>
    public bool RulesFailed { get; init; }

    /// <summary>Rules that fired, in run order.</summary>
    public IReadOnlyList<QueryTesterFiredRule> FiredRules { get; init; } = [];

    /// <summary>True when synonyms could not be applied, so the original text was searched alone.</summary>
    public bool SynonymsFailed { get; init; }

    /// <summary>Synonym substitutions used by the search, in order.</summary>
    public IReadOnlyList<QueryTesterAppliedSynonym> AppliedSynonyms { get; init; } = [];
}

/// <summary>A synonym substitution used by a query tester search.</summary>
/// <param name="SynonymId">Synonym identifier.</param>
/// <param name="Term">Query words it matched.</param>
/// <param name="Replacement">What replaced them in the variant.</param>
/// <param name="EditUrl">Path of the synonym's edit page; null when it was deleted since.</param>
public sealed record QueryTesterAppliedSynonym(int SynonymId, string Term, string Replacement, string? EditUrl);

/// <summary>A rule that fired for a query tester search.</summary>
/// <param name="RuleId">Rule identifier.</param>
/// <param name="Name">Display name; null when the rule was deleted since.</param>
/// <param name="EditUrl">Path of the rule's edit page; null when the rule was deleted since.</param>
/// <param name="UserData">Custom data the rule returned, or null.</param>
public sealed record QueryTesterFiredRule(int RuleId, string? Name, string? EditUrl, JsonElement? UserData);
