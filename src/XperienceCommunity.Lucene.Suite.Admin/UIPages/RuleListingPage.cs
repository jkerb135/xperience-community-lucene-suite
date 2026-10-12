using CMS.Core;
using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Core.Indexing;

using XperienceCommunity.Lucene.Suite.Admin;
using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Rules;

[assembly: UIPage(
    parentType: typeof(SearchTuningSectionPage),
    slug: "rules",
    uiPageType: typeof(RuleListingPage),
    name: "Rules",
    templateName: "@xperience-community/lucene-suite/RuleListing",
    order: 200,
    Icon = Icons.ListBullets)]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>The search rules of one Lucene index: derived status, search, paging and bulk changes.</summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class RuleListingPage(
    ILuceneIndexManager indexManager,
    IInfoProvider<RuleInfo> rules,
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator,
    IEventLogService eventLog,
    TimeProvider clock) : Page<RuleListingClientProperties>
{
    private const string EventSource = "LuceneSuite";
    private const string LoadError = "The rules could not be loaded. Check the connection and try again.";

    /// <summary>Identifier of the index, bound from the parent section.</summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(SearchTuningSectionPage))]
    public int IndexIdentifier { get; set; }

    /// <summary>Repeats the section's index check (Kentico validates only the requested page).</summary>
    public override Task<PageValidationResult> ValidatePage() =>
        Task.FromResult(SearchTuningSectionPage.Validate(indexManager, IndexIdentifier));

    /// <inheritdoc />
    public override async Task<RuleListingClientProperties> ConfigureTemplateProperties(RuleListingClientProperties properties)
    {
        properties.CanManage = (await permissionEvaluator.Evaluate(SiteSearchPermissions.MANAGE_RULES)).Succeeded;
        properties.CreatePath = pageLinkGenerator.GetPath<RuleCreatePage>(new PageParameterValues { { typeof(SearchTuningSectionPage), IndexIdentifier } });
        properties.IndexName = IndexName();
        return properties;
    }

    /// <summary>One page of this index's rules; a failed read comes back as <see cref="RuleListResult.Error"/>.</summary>
    [PageCommand(Permission = SystemPermissions.VIEW)]
    public Task<ICommandResponse<RuleListResult>> List(RuleListArguments arguments)
    {
        if (!RuleListing.TryStatus(arguments.Status, out var status))
        {
            return Task.FromResult(ResponseFrom(new RuleListResult { Error = $"Unknown status '{arguments.Status}'." }));
        }

        try
        {
            string indexName = IndexName();
            var now = clock.GetUtcNow().UtcDateTime;
            var slice = RuleListing.Apply(Rows(indexName, null), arguments.Query, status, arguments.Sort, arguments.Descending, arguments.Page, arguments.PageSize, now);
            // Rule JSON only for the visible page.
            var json = slice.Rows.Count == 0
                ? []
                : rules.Get()
                    .WhereEquals(nameof(RuleInfo.IndexName), indexName)
                    .WhereIn(nameof(RuleInfo.RuleID), slice.Rows.Select(row => row.Id).ToList())
                    .Columns(nameof(RuleInfo.RuleID), nameof(RuleInfo.Conditions), nameof(RuleInfo.Actions))
                    .ToDictionary(rule => rule.RuleID);
            return Task.FromResult(ResponseFrom(new RuleListResult
            {
                Rows = slice.Rows.Select(row => new RuleListItem
                {
                    Id = row.Id,
                    Name = row.Name,
                    Priority = row.Priority,
                    Status = RuleListing.Of(row, now).ToString(),
                    ValidFromUtc = RuleListing.Date(row.ValidFromUtc),
                    ValidUntilUtc = RuleListing.Date(row.ValidUntilUtc),
                    Conditions = RuleSummaries.Conditions(json.GetValueOrDefault(row.Id)?.Conditions),
                    Actions = RuleSummaries.Actions(json.GetValueOrDefault(row.Id)?.Actions),
                    EditPath = QueryTesterPage.RuleEditUrl(pageLinkGenerator, IndexIdentifier, row.Id),
                    Version = RuleEditMapping.VersionOf(row.LastModified),
                }).ToList(),
                Total = slice.Total,
                Page = slice.Page,
                PageSize = slice.PageSize,
                Counts = slice.Counts,
            }));
        }
        catch (Exception exception)
        {
            eventLog.LogException(EventSource, "RULES_LIST", exception);
            return Task.FromResult(ResponseFrom(new RuleListResult { Error = LoadError }));
        }
    }

    /// <summary>Enables the target rules of this index.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public Task<ICommandResponse<RuleBulkResult>> Enable(RuleBulkTarget target) => Bulk(RuleBulkAction.Enable, target, null);

    /// <summary>Disables the target rules of this index.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public Task<ICommandResponse<RuleBulkResult>> Disable(RuleBulkTarget target) => Bulk(RuleBulkAction.Disable, target, null);

    /// <summary>Deletes the target rules of this index.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public Task<ICommandResponse<RuleBulkResult>> Delete(RuleBulkTarget target) => Bulk(RuleBulkAction.Delete, target, null);

    /// <summary>Shifts or sets the priority of the target rules of this index.</summary>
    [PageCommand(Permission = SiteSearchPermissions.MANAGE_RULES)]
    public Task<ICommandResponse<RuleBulkResult>> ChangePriority(RulePriorityArguments arguments)
    {
        var change = RuleListing.ParsePriority(arguments.Mode, arguments.Value, out string? error);
        return change is null
            ? Task.FromResult(ResponseFrom(new RuleBulkResult { Error = error }))
            : Bulk(RuleBulkAction.ChangePriority, arguments, change);
    }

    private Task<ICommandResponse<RuleBulkResult>> Bulk(RuleBulkAction action, RuleBulkTarget target, RulePriorityChange? priority)
    {
        var result = Execute(action, target, priority);
        var response = ResponseFrom(result);
        // Kentico shows the message as a toast; failures are reported by the page from the result.
        return Task.FromResult(result.Succeeded > 0 ? response.AddSuccessMessage(RuleListing.SuccessMessage(action, result.Succeeded)) : response);
    }

    private RuleBulkResult Execute(RuleBulkAction action, RuleBulkTarget target, RulePriorityChange? priority)
    {
        string indexName = IndexName();
        if (indexName.Length == 0)
        {
            return new RuleBulkResult { Error = "The index does not exist." };
        }

        RuleBulkPlan plan;
        try
        {
            plan = RuleListing.Plan(action, target, priority, clock.GetUtcNow().UtcDateTime, ids => Rows(indexName, ids));
        }
        catch (Exception exception)
        {
            eventLog.LogException(EventSource, $"RULES_BULK_{Code(action)}", exception);
            return new RuleBulkResult { Error = LoadError };
        }

        if (plan.Error is not null)
        {
            return new RuleBulkResult { Error = plan.Error };
        }

        var failed = plan.Failed.ToList();
        var applied = new List<RuleBulkChange>();
        var stored = plan.Changes.Count == 0
            ? []
            : rules.Get()
                .WhereEquals(nameof(RuleInfo.IndexName), indexName)
                .WhereIn(nameof(RuleInfo.RuleID), plan.Changes.Select(change => change.Before.Id).ToList())
                .ToDictionary(rule => rule.RuleID);
        // Per rule through the provider so RuleProvider's info events invalidate every node's cache; never bulk SQL.
        foreach (var change in plan.Changes)
        {
            var before = change.Before;
            if (!stored.TryGetValue(before.Id, out var rule))
            {
                failed.Add(new RuleBulkFailure { Id = before.Id, Name = before.Name, Reason = RuleListing.NotInIndex });
                continue;
            }

            if (rule.LastModified != before.LastModified)
            {
                failed.Add(new RuleBulkFailure { Id = before.Id, Name = before.Name, Reason = RuleListing.ChangedBySomeoneElse });
                continue;
            }

            try
            {
                if (change.After is null)
                {
                    rules.Delete(rule);
                }
                else
                {
                    rule.Enabled = change.After.Enabled;
                    rule.Priority = change.After.Priority;
                    rules.Set(rule);
                }

                applied.Add(change);
            }
            catch (Exception exception)
            {
                eventLog.LogException(EventSource, $"RULES_BULK_{Code(action)}", exception);
                failed.Add(new RuleBulkFailure { Id = before.Id, Name = before.Name, Reason = "it could not be saved" });
            }
        }

        if (applied.Count > 0 || failed.Count > 0)
        {
            foreach (string part in RuleListing.AuditParts(action, indexName, applied, failed))
            {
                eventLog.LogInformation(EventSource, $"RULES_BULK_{Code(action)}", part);
            }
        }

        return new RuleBulkResult { Succeeded = applied.Count + plan.Unchanged, Failed = failed };
    }

    private List<RuleListRow> Rows(string indexName, IReadOnlyCollection<int>? ids)
    {
        var query = rules.Get()
            .WhereEquals(nameof(RuleInfo.IndexName), indexName)
            .Columns(
                nameof(RuleInfo.RuleID),
                nameof(RuleInfo.DisplayName),
                nameof(RuleInfo.Enabled),
                nameof(RuleInfo.Priority),
                nameof(RuleInfo.ValidFromUtc),
                nameof(RuleInfo.ValidUntilUtc),
                nameof(RuleInfo.LastModified));
        if (ids is not null)
        {
            query = query.WhereIn(nameof(RuleInfo.RuleID), ids.ToList());
        }

        return query.AsEnumerable()
            .Select(rule => new RuleListRow(rule.RuleID, rule.DisplayName, rule.Enabled, rule.Priority, rule.ValidFromUtc, rule.ValidUntilUtc, rule.LastModified))
            .ToList();
    }

    private static string Code(RuleBulkAction action) => action == RuleBulkAction.ChangePriority ? "PRIORITY" : action.ToString().ToUpperInvariant();

    private string IndexName() => indexManager.GetIndex(IndexIdentifier)?.IndexName ?? string.Empty;
}

/// <summary>Loads a rule only when it belongs to the given index.</summary>
internal static class RuleOwnership
{
    public static RuleInfo? Find(IInfoProvider<RuleInfo> rules, int ruleId, string indexName)
    {
        var rule = rules.Get().WhereEquals(nameof(RuleInfo.RuleID), ruleId).FirstOrDefault();
        return rule is not null && indexName.Length > 0 && string.Equals(rule.IndexName, indexName, StringComparison.OrdinalIgnoreCase)
            ? rule
            : null;
    }
}

/// <summary>Template properties of the rules listing.</summary>
public sealed class RuleListingClientProperties : TemplateClientProperties
{
    /// <summary>Whether the user may change rules (Site Search → Manage rules); only shows the controls, the commands check it again.</summary>
    public bool CanManage { get; set; }

    /// <summary>Admin-root-relative path of this index's Create rule page.</summary>
    public string CreatePath { get; set; } = string.Empty;

    /// <summary>Code name of the index, for display only.</summary>
    public string IndexName { get; set; } = string.Empty;
}

/// <summary>Requested listing page; the index always comes from the page.</summary>
public sealed class RuleListArguments
{
    /// <summary>Text the rule name contains (case-insensitive, at most 200 characters are used).</summary>
    public string? Query { get; set; }

    /// <summary>All (or empty), Active, Scheduled, Expired or Disabled.</summary>
    public string? Status { get; set; }

    /// <summary>name, priority, validFrom or validUntil; anything else sorts by priority.</summary>
    public string? Sort { get; set; }

    /// <summary>Sort descending.</summary>
    public bool Descending { get; set; }

    /// <summary>1-based page, clamped to the last page.</summary>
    public int Page { get; set; } = 1;

    /// <summary>25, 50 or 100; anything else is 25.</summary>
    public int PageSize { get; set; } = 25;
}

/// <summary>One listing page; only <see cref="Error"/> is set when the rules could not be read.</summary>
public sealed class RuleListResult
{
    /// <summary>Why nothing was listed.</summary>
    public string? Error { get; set; }

    /// <summary>The page's rules.</summary>
    public IReadOnlyList<RuleListItem> Rows { get; set; } = [];

    /// <summary>Rules matching the search and status.</summary>
    public int Total { get; set; }

    /// <summary>The page returned (after clamping).</summary>
    public int Page { get; set; }

    /// <summary>The page size used.</summary>
    public int PageSize { get; set; }

    /// <summary>Rules per status among those matching the search.</summary>
    public RuleStatusCounts Counts { get; set; } = new();
}

/// <summary>One listed rule.</summary>
public sealed class RuleListItem
{
    /// <summary>Rule identifier.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower runs first.</summary>
    public int Priority { get; set; }

    /// <summary>Active, Scheduled, Expired or Disabled at the time of the request.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Inclusive start (yyyy-MM-dd HH:mm, UTC); empty when unbounded.</summary>
    public string ValidFromUtc { get; set; } = string.Empty;

    /// <summary>Exclusive end (yyyy-MM-dd HH:mm, UTC); empty when unbounded.</summary>
    public string ValidUntilUtc { get; set; } = string.Empty;

    /// <summary>One-line summary of the conditions.</summary>
    public string Conditions { get; set; } = string.Empty;

    /// <summary>One-line summary of the actions.</summary>
    public string Actions { get; set; } = string.Empty;

    /// <summary>Admin-root-relative path of the rule's edit page.</summary>
    public string EditPath { get; set; } = string.Empty;

    /// <summary>Concurrency token sent back with bulk commands (the edit page's version).</summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>Rules per derived status.</summary>
public sealed class RuleStatusCounts
{
    /// <summary>Every rule counted.</summary>
    public int All { get; set; }

    /// <summary>Enabled and inside the validity window.</summary>
    public int Active { get; set; }

    /// <summary>Enabled, valid from in the future.</summary>
    public int Scheduled { get; set; }

    /// <summary>Enabled, valid until passed.</summary>
    public int Expired { get; set; }

    /// <summary>Not enabled.</summary>
    public int Disabled { get; set; }
}

/// <summary>The rules a bulk command changes: either <see cref="Items"/> or <see cref="Filter"/>.</summary>
public class RuleBulkTarget
{
    /// <summary>Selected rules with the version the list showed (at most 1,000).</summary>
    public IReadOnlyList<RuleBulkItem>? Items { get; set; }

    /// <summary>Every rule matching this filter, re-applied on the server.</summary>
    public RuleBulkFilter? Filter { get; set; }
}

/// <summary>A selected rule.</summary>
public sealed class RuleBulkItem
{
    /// <summary>Rule identifier.</summary>
    public int Id { get; set; }

    /// <summary><see cref="RuleListItem.Version"/> as listed; a different stored version fails the rule.</summary>
    public string? Version { get; set; }
}

/// <summary>Select-all-matching filter.</summary>
public sealed class RuleBulkFilter
{
    /// <summary>Search text, as in <see cref="RuleListArguments.Query"/>.</summary>
    public string? Query { get; set; }

    /// <summary>Status filter, as in <see cref="RuleListArguments.Status"/>.</summary>
    public string? Status { get; set; }

    /// <summary>The match count the user confirmed; any other count refuses the whole command.</summary>
    public int ExpectedCount { get; set; }
}

/// <summary>Target plus the priority change.</summary>
public sealed class RulePriorityArguments : RuleBulkTarget
{
    /// <summary>shift or set.</summary>
    public string? Mode { get; set; }

    /// <summary>Whole number as typed; validated on the server.</summary>
    public string? Value { get; set; }
}

/// <summary>Outcome of a bulk command; <see cref="Error"/> means nothing was changed.</summary>
public sealed class RuleBulkResult
{
    /// <summary>Why the whole command was refused.</summary>
    public string? Error { get; set; }

    /// <summary>Rules now in the requested state (including those that already were).</summary>
    public int Succeeded { get; set; }

    /// <summary>Rules left unchanged, with the reason.</summary>
    public IReadOnlyList<RuleBulkFailure> Failed { get; set; } = [];
}

/// <summary>A rule a bulk command did not change.</summary>
public sealed class RuleBulkFailure
{
    /// <summary>Rule identifier as sent.</summary>
    public int Id { get; set; }

    /// <summary>Display name; empty when the rule is not in this index.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Why, e.g. "it was changed by someone else".</summary>
    public string Reason { get; set; } = string.Empty;
}
