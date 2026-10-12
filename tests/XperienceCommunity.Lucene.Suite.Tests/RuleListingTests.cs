using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RuleListingTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Modified = new(2026, 10, 1, 8, 30, 15, 123, DateTimeKind.Utc);

    private static RuleListRow Rule(int id, string name = "Rule", int priority = 10, bool enabled = true, DateTime? from = null, DateTime? until = null) =>
        new(id, name, enabled, priority, from, until, Modified);

    private static RuleBulkItem Item(RuleListRow row) => new() { Id = row.Id, Version = RuleEditMapping.VersionOf(row.LastModified) };

    private static Func<IReadOnlyCollection<int>?, IReadOnlyList<RuleListRow>> Index(params RuleListRow[] rows) =>
        ids => ids is null ? rows : rows.Where(row => ids.Contains(row.Id)).ToList();

    [Fact]
    public void Status_AtTheBoundaries()
    {
        Assert.Equal(RuleStatus.Disabled, RuleListing.Of(false, Now.AddDays(-1), Now.AddDays(1), Now));
        Assert.Equal(RuleStatus.Disabled, RuleListing.Of(false, Now.AddMinutes(1), null, Now));
        Assert.Equal(RuleStatus.Scheduled, RuleListing.Of(true, Now.AddMinutes(1), null, Now));
        Assert.Equal(RuleStatus.Expired, RuleListing.Of(true, null, Now, Now));
        Assert.Equal(RuleStatus.Active, RuleListing.Of(true, Now, null, Now));
        Assert.Equal(RuleStatus.Active, RuleListing.Of(true, null, Now.AddTicks(1), Now));
    }

    [Fact]
    public void Counts_AddUpToAll_OverTheSearchedRules()
    {
        var rows = new[]
        {
            Rule(1, "Coffee a"), Rule(2, "Coffee b", enabled: false), Rule(3, "Coffee c", from: Now.AddDays(1)),
            Rule(4, "Coffee d", until: Now.AddDays(-1)), Rule(5, "Tea"),
        };

        var slice = RuleListing.Apply(rows, "  COFFEE ", RuleStatus.Active, null, false, 1, 25, Now);
        var c = slice.Counts;

        Assert.Equal((4, 1, 1, 1, 1), (c.All, c.Active, c.Scheduled, c.Expired, c.Disabled));
        Assert.Equal(c.All, c.Active + c.Scheduled + c.Expired + c.Disabled);
        Assert.Equal([1], slice.Rows.Select(r => r.Id));
        Assert.Equal(1, slice.Total);
    }

    [Fact]
    public void FiveHundredRules_SearchFilterSortDescendingAndPage()
    {
        var rows = Enumerable.Range(1, 500)
            .Select(i => Rule(i, i % 2 == 0 ? $"Coffee {i}" : $"Tea {i}", priority: i, enabled: i % 10 != 0))
            .ToList();

        var slice = RuleListing.Apply(rows, "coffee", RuleStatus.Active, "priority", descending: true, page: 1, pageSize: 50, Now);

        // 250 coffee rules, 50 of them disabled (multiples of 10).
        Assert.Equal(200, slice.Total);
        Assert.Equal(50, slice.Rows.Count);
        Assert.Equal(498, slice.Rows[0].Priority);
        Assert.True(slice.Rows.Zip(slice.Rows.Skip(1)).All(p => p.First.Priority > p.Second.Priority));
        Assert.All(slice.Rows, r => Assert.Contains("Coffee", r.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Paging_IsClampedAndWhitelisted_SortIsWhitelistedWithIdTieBreak()
    {
        var rows = Enumerable.Range(1, 30).Select(i => Rule(i, $"r{i:00}", priority: 5)).ToList();

        var odd = RuleListing.Apply(rows, null, null, "DROP TABLE", false, 99, 7, Now);
        Assert.Equal((2, 25, 5), (odd.Page, odd.PageSize, odd.Rows.Count));
        Assert.Equal([26, 27, 28, 29, 30], odd.Rows.Select(r => r.Id));

        Assert.Equal(1, RuleListing.Apply(rows, null, null, null, false, -3, 100, Now).Page);
        Assert.Equal(1, RuleListing.Apply([], null, null, null, false, 4, 25, Now).Page);

        var byName = RuleListing.Apply([Rule(1, "beta"), Rule(2, "Alpha"), Rule(3, "alpha")], null, null, "name", false, 1, 25, Now);
        Assert.Equal([2, 3, 1], byName.Rows.Select(r => r.Id));
    }

    [Fact]
    public void Query_IsTrimmedAndCappedAt200Characters()
    {
        string name = new('x', 250);
        Assert.Equal(200, RuleListing.NormalizeQuery($"  {name}  ").Length);
        Assert.Single(RuleListing.Apply([Rule(1, name)], name + "y", null, null, false, 1, 25, Now).Rows);
    }

    [Fact]
    public void Status_OnlyNamesParse()
    {
        Assert.True(RuleListing.TryStatus(null, out var none) && none is null);
        Assert.True(RuleListing.TryStatus("all", out var all) && all is null);
        Assert.True(RuleListing.TryStatus("scheduled", out var scheduled) && scheduled == RuleStatus.Scheduled);
        Assert.False(RuleListing.TryStatus("1", out _));
        Assert.False(RuleListing.TryStatus("Live", out _));
    }

    [Fact]
    public void Priority_ShiftSetOverflowAndBelowOne()
    {
        int[] before = [10, 20, 30, 40, 50];
        var shift = RuleListing.ParsePriority("shift", "-5", out _)!;
        var set = RuleListing.ParsePriority("Set", " 1 ", out _)!;

        Assert.Equal([5, 15, 25, 35, 45], before.Select(p => RuleListing.PlanPriority(shift, p).Priority!.Value));
        Assert.All(before, p => Assert.Equal(1, RuleListing.PlanPriority(set, p).Priority));
        Assert.Equal((null, RuleListing.OutOfRange), RuleListing.PlanPriority(new RulePriorityChange(false, int.MaxValue), 1));
        Assert.Equal((null, RuleListing.BelowOne), RuleListing.PlanPriority(new RulePriorityChange(false, -10), 10));
    }

    [Theory]
    [InlineData("shift", "")]
    [InlineData("shift", null)]
    [InlineData("shift", "1.5")]
    [InlineData("shift", "1e3")]
    [InlineData("shift", "2147483648")]
    [InlineData("set", "0")]
    [InlineData("move", "1")]
    public void Priority_InvalidInputIsRefused(string? mode, string? value)
    {
        Assert.Null(RuleListing.ParsePriority(mode, value, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Disable_ForeignMissingAndStaleIdsFail_OthersApply()
    {
        var a = Rule(1, "A");
        var b = Rule(2, "B");
        var c = Rule(3, "C", enabled: false);
        var target = new RuleBulkTarget
        {
            Items =
            [
                Item(a),
                new RuleBulkItem { Id = 2, Version = RuleEditMapping.VersionOf(Modified.AddMilliseconds(1)) },
                Item(c),
                // Another index's rule: the read is scoped to this index, so it is simply not returned.
                new RuleBulkItem { Id = 77, Version = "x" },
            ],
        };

        var plan = RuleListing.Plan(RuleBulkAction.Disable, target, null, Now, Index(a, b, c));

        Assert.Null(plan.Error);
        Assert.Equal([1], plan.Changes.Select(change => change.Before.Id));
        Assert.False(plan.Changes[0].After!.Enabled);
        Assert.Equal(1, plan.Unchanged);
        Assert.Equal(
            [(2, "B", RuleListing.ChangedBySomeoneElse), (77, "", RuleListing.NotInIndex)],
            plan.Failed.Select(f => (f.Id, f.Name, f.Reason)));
    }

    [Fact]
    public void Filter_IsReappliedOnTheServer_AndAChangedCountRefusesEverything()
    {
        var rows = Enumerable.Range(1, 120).Select(i => Rule(i, $"Coffee {i}", enabled: false))
            .Append(Rule(500, "Tea", enabled: false)).ToArray();
        var filter = new RuleBulkFilter { Query = "coffee", Status = "Disabled", ExpectedCount = 120 };

        var plan = RuleListing.Plan(RuleBulkAction.Enable, new RuleBulkTarget { Filter = filter }, null, Now, Index(rows));
        Assert.Null(plan.Error);
        Assert.Equal(120, plan.Changes.Count);
        Assert.DoesNotContain(plan.Changes, change => change.Before.Id == 500);

        var drifted = RuleListing.Plan(RuleBulkAction.Delete, new RuleBulkTarget { Filter = filter }, null, Now, Index([.. rows, Rule(121, "Coffee new", enabled: false)]));
        Assert.Equal(RuleListing.MatchingChanged, drifted.Error);
        Assert.Empty(drifted.Changes);
    }

    [Fact]
    public void Target_MustBeItemsOrFilter_AndAtMost1000Items()
    {
        Assert.NotNull(RuleListing.ValidateTarget(new RuleBulkTarget()));
        Assert.NotNull(RuleListing.ValidateTarget(new RuleBulkTarget { Items = [], Filter = new RuleBulkFilter() }));
        Assert.NotNull(RuleListing.ValidateTarget(new RuleBulkTarget { Items = [] }));
        Assert.NotNull(RuleListing.ValidateTarget(new RuleBulkTarget { Items = Enumerable.Range(1, 1001).Select(i => new RuleBulkItem { Id = i }).ToList() }));
        Assert.Null(RuleListing.ValidateTarget(new RuleBulkTarget { Filter = new RuleBulkFilter() }));
        Assert.NotNull(RuleListing.Plan(RuleBulkAction.ChangePriority, new RuleBulkTarget { Items = [new RuleBulkItem { Id = 1 }] }, null, Now, Index()).Error);
    }

    [Fact]
    public void Audit_RecordsBeforeAndAfterPerRule_AndFailures_SplitIntoParts()
    {
        var a = Rule(1, "Coffee promo", priority: 10);
        var d = Rule(2, "Old banner", priority: 3, from: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var failure = new RuleBulkFailure { Id = 9, Name = "Busy", Reason = RuleListing.ChangedBySomeoneElse };

        string priority = Assert.Single(RuleListing.AuditParts(RuleBulkAction.ChangePriority, "site", [new RuleBulkChange(a, a with { Priority = 5 })], [failure]));
        Assert.Contains("Index: site", priority, StringComparison.Ordinal);
        Assert.Contains("1 · Coffee promo · enabled True → True, priority 10 → 5", priority, StringComparison.Ordinal);
        Assert.Contains("9 · Busy · it was changed by someone else", priority, StringComparison.Ordinal);

        string deleted = Assert.Single(RuleListing.AuditParts(RuleBulkAction.Delete, "site", [new RuleBulkChange(d, null)], []));
        Assert.Contains("2 · Old banner · deleted (priority 3, enabled True, valid from 2026-01-01 00:00, valid until )", deleted, StringComparison.Ordinal);

        var many = Enumerable.Range(1, 50).Select(i => new RuleBulkChange(Rule(i, $"Rule {i}"), null)).ToList();
        var parts = RuleListing.AuditParts(RuleBulkAction.Delete, "site", many, [], maxLength: 600);
        Assert.True(parts.Count > 1);
        Assert.All(parts, part => Assert.True(part.Length <= 600));
        Assert.StartsWith("Index: site. Delete: 50 changed, 0 failed. (part 1 of ", parts[0], StringComparison.Ordinal);
        Assert.Equal(50, parts.Sum(part => part.Split('\n').Length - 1));
    }

    [Fact]
    public void SuccessMessages()
    {
        Assert.Equal("3 rules disabled.", RuleListing.SuccessMessage(RuleBulkAction.Disable, 3));
        Assert.Equal("1 rule deleted.", RuleListing.SuccessMessage(RuleBulkAction.Delete, 1));
        Assert.Equal("Priority changed for 1,200 rules.", RuleListing.SuccessMessage(RuleBulkAction.ChangePriority, 1200));
    }
}
