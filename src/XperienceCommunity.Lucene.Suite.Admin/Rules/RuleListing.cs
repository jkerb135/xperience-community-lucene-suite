using System.Globalization;
using System.Text;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>Status of a rule derived from Enabled and its validity window at a moment.</summary>
internal enum RuleStatus
{
    Active,
    Scheduled,
    Expired,
    Disabled,
}

internal enum RuleBulkAction
{
    Enable,
    Disable,
    ChangePriority,
    Delete,
}

/// <summary>The lightweight columns of a stored rule the listing works on.</summary>
internal sealed record RuleListRow(int Id, string Name, bool Enabled, int Priority, DateTime? ValidFromUtc, DateTime? ValidUntilUtc, DateTime LastModified);

/// <summary>One listing page; <see cref="Counts"/> are over the searched rules before the status filter.</summary>
internal sealed record RuleListSlice(IReadOnlyList<RuleListRow> Rows, int Total, int Page, int PageSize, RuleStatusCounts Counts);

/// <summary>Shift every priority by <see cref="Value"/>, or set it to <see cref="Value"/>.</summary>
internal sealed record RulePriorityChange(bool Set, int Value);

/// <summary>A rule a bulk command changes; <see cref="After"/> is null for Delete.</summary>
internal sealed record RuleBulkChange(RuleListRow Before, RuleListRow? After);

/// <summary>What a bulk command will do. <see cref="Error"/> refuses the whole command; <see cref="Unchanged"/> rules were already in the target state.</summary>
internal sealed record RuleBulkPlan(IReadOnlyList<RuleBulkChange> Changes, int Unchanged, IReadOnlyList<RuleBulkFailure> Failed, string? Error);

/// <summary>Status, search, sort, paging and bulk planning of the rules listing (pure; the page reads and writes the rules).</summary>
internal static class RuleListing
{
    public const int MaxQueryLength = 200;
    public const int MaxItems = 1000;
    public const int DefaultPageSize = 25;
    public const int AuditPartLength = 8000;
    public const string NotInIndex = "not in this index";
    public const string ChangedBySomeoneElse = "it was changed by someone else";
    public const string MatchingChanged = "The matching rules changed. Reload and try again.";
    public const string OutOfRange = "priority out of range";
    public const string BelowOne = "priority must be 1 or greater";

    private static readonly int[] PageSizes = [25, 50, 100];

    /// <summary>Disabled, else Scheduled (from in the future), else Expired (until is exclusive), else Active; the engine's window.</summary>
    public static RuleStatus Of(bool enabled, DateTime? fromUtc, DateTime? untilUtc, DateTime nowUtc) =>
        !enabled ? RuleStatus.Disabled
        : fromUtc > nowUtc ? RuleStatus.Scheduled
        : untilUtc <= nowUtc ? RuleStatus.Expired
        : RuleStatus.Active;

    public static RuleStatus Of(RuleListRow row, DateTime nowUtc) => Of(row.Enabled, row.ValidFromUtc, row.ValidUntilUtc, nowUtc);

    /// <summary>Null, empty or "All" is no filter (<paramref name="status"/> null); false for an unknown name.</summary>
    public static bool TryStatus(string? value, out RuleStatus? status)
    {
        status = null;
        if (string.IsNullOrEmpty(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Enum.TryParse also accepts numbers; only the names are part of the contract.
        if (!int.TryParse(value, out _) && Enum.TryParse<RuleStatus>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            status = parsed;
            return true;
        }

        return false;
    }

    public static string NormalizeQuery(string? query)
    {
        string text = (query ?? string.Empty).Trim();
        return text.Length > MaxQueryLength ? text[..MaxQueryLength] : text;
    }

    /// <summary>Rules whose name contains the query (case-insensitive) and that have the status (null = any).</summary>
    public static List<RuleListRow> Matching(IEnumerable<RuleListRow> rows, string? query, RuleStatus? status, DateTime nowUtc) =>
        Search(rows, query).Where(row => status is null || Of(row, nowUtc) == status).ToList();

    public static RuleListSlice Apply(
        IEnumerable<RuleListRow> rows, string? query, RuleStatus? status, string? sort, bool descending, int page, int pageSize, DateTime nowUtc)
    {
        var searched = Search(rows, query).Select(row => (Row: row, Status: Of(row, nowUtc))).ToList();
        int Count(RuleStatus s) => searched.Count(x => x.Status == s);
        var counts = new RuleStatusCounts
        {
            All = searched.Count,
            Active = Count(RuleStatus.Active),
            Scheduled = Count(RuleStatus.Scheduled),
            Expired = Count(RuleStatus.Expired),
            Disabled = Count(RuleStatus.Disabled),
        };

        var matching = Sort(searched.Where(x => status is null || x.Status == status).Select(x => x.Row), sort, descending).ToList();
        int size = PageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;
        int last = Math.Max(1, (matching.Count + size - 1) / size);
        int current = Math.Clamp(page, 1, last);
        return new RuleListSlice(matching.Skip((current - 1) * size).Take(size).ToList(), matching.Count, current, size, counts);
    }

    /// <summary>Shift or set from the dialog's text; null with <paramref name="error"/> for an unknown mode or a value that is not an int.</summary>
    public static RulePriorityChange? ParsePriority(string? mode, string? value, out string? error)
    {
        bool set = string.Equals(mode, "set", StringComparison.OrdinalIgnoreCase);
        if (!set && !string.Equals(mode, "shift", StringComparison.OrdinalIgnoreCase))
        {
            error = "Choose Shift by or Set to.";
            return null;
        }

        if (!int.TryParse(value?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
        {
            error = "Enter a whole number between -2,147,483,648 and 2,147,483,647.";
            return null;
        }

        if (set && number < 1)
        {
            error = "The priority must be 1 or greater.";
            return null;
        }

        error = null;
        return new RulePriorityChange(set, number);
    }

    /// <summary>The new priority, or a failure reason when it overflows or drops below 1 (the serializer rejects such rules).</summary>
    public static (int? Priority, string? Failure) PlanPriority(RulePriorityChange change, int current)
    {
        int next;
        try
        {
            next = change.Set ? change.Value : checked(current + change.Value);
        }
        catch (OverflowException)
        {
            return (null, OutOfRange);
        }

        return next < 1 ? (null, BelowOne) : (next, null);
    }

    /// <summary>Why the target is malformed, or null.</summary>
    public static string? ValidateTarget(RuleBulkTarget? target) => target switch
    {
        null or { Items: null, Filter: null } or { Items: not null, Filter: not null } => "Send either the selected rules or the filter.",
        { Items.Count: 0 } => "Select at least one rule.",
        { Items.Count: > MaxItems } => $"At most {MaxItems.ToString("N0", CultureInfo.InvariantCulture)} rules can be selected; use Select all matching.",
        { Filter.ExpectedCount: < 0 } => "The expected count must be 0 or more.",
        _ => null,
    };

    /// <summary>
    /// Resolves the target against the stored rules and plans each change. <paramref name="read"/> returns this index's rules
    /// among the ids, or all of them for null. Selected ids missing from the index or with another version fail one by one;
    /// a filter whose match count differs from ExpectedCount refuses the whole command.
    /// </summary>
    public static RuleBulkPlan Plan(
        RuleBulkAction action, RuleBulkTarget target, RulePriorityChange? priority, DateTime nowUtc, Func<IReadOnlyCollection<int>?, IReadOnlyList<RuleListRow>> read)
    {
        if (ValidateTarget(target) is { } invalid)
        {
            return new RuleBulkPlan([], 0, [], invalid);
        }

        if (action == RuleBulkAction.ChangePriority && priority is null)
        {
            return new RuleBulkPlan([], 0, [], "Choose Shift by or Set to.");
        }

        var failed = new List<RuleBulkFailure>();
        List<RuleListRow> targets;
        if (target.Filter is { } filter)
        {
            if (!TryStatus(filter.Status, out var status))
            {
                return new RuleBulkPlan([], 0, [], $"Unknown status '{filter.Status}'.");
            }

            targets = Matching(read(null), filter.Query, status, nowUtc);
            if (targets.Count != filter.ExpectedCount)
            {
                return new RuleBulkPlan([], 0, [], MatchingChanged);
            }
        }
        else
        {
            var items = target.Items!.DistinctBy(item => item.Id).ToList();
            var stored = read(items.Select(item => item.Id).ToList()).ToDictionary(row => row.Id);
            targets = [];
            foreach (var item in items)
            {
                if (!stored.TryGetValue(item.Id, out var row))
                {
                    failed.Add(new RuleBulkFailure { Id = item.Id, Reason = NotInIndex });
                }
                else if (item.Version != RuleEditMapping.VersionOf(row.LastModified))
                {
                    failed.Add(new RuleBulkFailure { Id = row.Id, Name = row.Name, Reason = ChangedBySomeoneElse });
                }
                else
                {
                    targets.Add(row);
                }
            }
        }

        var changes = new List<RuleBulkChange>();
        int unchanged = 0;
        foreach (var row in targets)
        {
            RuleListRow? after;
            switch (action)
            {
                case RuleBulkAction.Delete:
                    changes.Add(new RuleBulkChange(row, null));
                    continue;
                case RuleBulkAction.ChangePriority:
                    var (next, failure) = PlanPriority(priority!, row.Priority);
                    if (failure is not null)
                    {
                        failed.Add(new RuleBulkFailure { Id = row.Id, Name = row.Name, Reason = failure });
                        continue;
                    }

                    after = row with { Priority = next!.Value };
                    break;
                default:
                    after = row with { Enabled = action == RuleBulkAction.Enable };
                    break;
            }

            if (after == row)
            {
                unchanged++;
            }
            else
            {
                changes.Add(new RuleBulkChange(row, after));
            }
        }

        return new RuleBulkPlan(changes, unchanged, failed, null);
    }

    public static string SuccessMessage(RuleBulkAction action, int count) => action switch
    {
        RuleBulkAction.Enable => $"{RulesText(count)} enabled.",
        RuleBulkAction.Disable => $"{RulesText(count)} disabled.",
        RuleBulkAction.Delete => $"{RulesText(count)} deleted.",
        _ => $"Priority changed for {RulesText(count)}.",
    };

    /// <summary>
    /// Event log descriptions of one bulk command: the index, then per changed rule its id, name and before → after values
    /// (Delete: every value it had), then the failed ids with reasons. Split into "part i of n" entries of at most
    /// <paramref name="maxLength"/> characters.
    /// </summary>
    public static IReadOnlyList<string> AuditParts(
        RuleBulkAction action, string indexName, IReadOnlyList<RuleBulkChange> changes, IReadOnlyList<RuleBulkFailure> failed, int maxLength = AuditPartLength)
    {
        string header = $"Index: {indexName}. {action}: {changes.Count} changed, {failed.Count} failed.";
        var lines = changes.Select(Describe).ToList();
        if (failed.Count > 0)
        {
            lines.Add("Failed:");
            lines.AddRange(failed.Select(f => $"{f.Id} · {f.Name} · {f.Reason}"));
        }

        // Room for the " (part i of n)" suffix of the header.
        int budget = maxLength - header.Length - 32;
        var parts = new List<StringBuilder> { new() };
        foreach (string line in lines)
        {
            if (parts[^1].Length > 0 && parts[^1].Length + line.Length + 1 > budget)
            {
                parts.Add(new StringBuilder());
            }

            parts[^1].Append('\n').Append(line);
        }

        return parts.Count == 1
            ? [header + parts[0]]
            : parts.Select((part, i) => $"{header} (part {i + 1} of {parts.Count}){part}").ToList();
    }

    private static string Describe(RuleBulkChange change)
    {
        var (before, after) = change;
        return after is null
            ? $"{before.Id} · {before.Name} · deleted (priority {before.Priority}, enabled {before.Enabled}, valid from {Date(before.ValidFromUtc)}, valid until {Date(before.ValidUntilUtc)})"
            : $"{before.Id} · {before.Name} · enabled {before.Enabled} → {after.Enabled}, priority {before.Priority} → {after.Priority}";
    }

    public static string Date(DateTime? value) =>
        value is { } date ? date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;

    private static string RulesText(int count) => count == 1 ? "1 rule" : $"{count.ToString("N0", CultureInfo.InvariantCulture)} rules";

    private static IEnumerable<RuleListRow> Search(IEnumerable<RuleListRow> rows, string? query)
    {
        string text = NormalizeQuery(query);
        return text.Length == 0 ? rows : rows.Where(row => row.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static IOrderedEnumerable<RuleListRow> Sort(IEnumerable<RuleListRow> rows, string? sort, bool descending) => sort switch
    {
        "name" => By(rows, row => row.Name, StringComparer.OrdinalIgnoreCase, descending),
        "validFrom" => By(rows, row => row.ValidFromUtc, null, descending),
        "validUntil" => By(rows, row => row.ValidUntilUtc, null, descending),
        _ => By(rows, row => row.Priority, null, descending),
    };

    private static IOrderedEnumerable<RuleListRow> By<T>(IEnumerable<RuleListRow> rows, Func<RuleListRow, T> key, IComparer<T>? comparer, bool descending) =>
        (descending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer)).ThenBy(row => row.Id);
}
