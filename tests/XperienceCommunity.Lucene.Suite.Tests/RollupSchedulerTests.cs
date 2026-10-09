using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class RollupSchedulerTests
{
    // Defaults: a day completes 1 day + 2 × 1 h + 2 s + 5 min after its start, i.e. at 02:05:02 the next day.
    private static readonly DateOnly Jan1 = new(2030, 1, 1);

    [Fact]
    public void FirstRun_StartsAtTheEarliestRawDay_ExactlyAtTheEligibilityInstant()
    {
        var plan = Plan(null, Jan1, At("2030-01-02T02:05:02"));

        Assert.Null(plan.SkipThrough);
        Assert.Equal([Jan1], plan.Days);
    }

    [Fact]
    public void OneTickBeforeTheEligibilityInstant_NothingIsEligible()
    {
        var plan = Plan(null, Jan1, At("2030-01-02T02:05:02").AddTicks(-1));

        Assert.Empty(plan.Days);
        Assert.Null(plan.SkipThrough);
    }

    [Fact]
    public void WithAWatermark_TheNextDayWaitsForItsOwnEligibilityInstant()
    {
        Assert.Empty(Plan(Jan1, null, At("2030-01-03T02:05:01")).Days);
        Assert.Equal([Jan1.AddDays(1)], Plan(Jan1, null, At("2030-01-03T02:05:02")).Days);
    }

    [Fact]
    public void NoWatermarkAndNoRawRows_NothingToDo()
    {
        var plan = Plan(null, null, At("2030-01-10T00:00:00"));

        Assert.Equal((null, 0), (plan.SkipThrough, plan.Days.Count));
    }

    [Fact]
    public void CatchUpAfterDowntime_IsCappedByMaxDaysPerRun_InAscendingOrder()
    {
        var options = new SuiteAnalyticsOptions();
        options.Retention.RawRetention = TimeSpan.FromDays(30);

        var plan = RollupScheduler.Plan(Jan1, null, At("2030-01-20T12:00:00"), options);

        Assert.Null(plan.SkipThrough);
        Assert.Equal(Enumerable.Range(1, 7).Select(Jan1.AddDays), plan.Days);

        options.Job.MaxDaysPerRun = 1;
        Assert.Equal([Jan1.AddDays(1)], RollupScheduler.Plan(Jan1, null, At("2030-01-20T12:00:00"), options).Days);
    }

    [Fact]
    public void StaleOrResetWatermark_SkipsDaysWhoseInputWindowLeftRawRetention_ThenRollsTheRest()
    {
        // now − 48 h + 1 h = 2030-01-08 13:00, so 01-09 is the first day whose input window is complete.
        var plan = Plan(new DateOnly(2029, 12, 1), null, At("2030-01-10T12:00:00"));

        Assert.Equal(new DateOnly(2030, 1, 8), plan.SkipThrough);
        Assert.Equal([new DateOnly(2030, 1, 9)], plan.Days);
    }

    [Fact]
    public void CompletenessFloor_Boundary_DayStartMinusWindowEqualToNowMinusRetentionIsStillRolled()
    {
        // 2030-01-09 − 1 h = 2030-01-10 23:00 − 48 h.
        var exactly = Plan(new DateOnly(2030, 1, 8), null, At("2030-01-10T23:00:00"));
        Assert.Null(exactly.SkipThrough);
        Assert.Equal([new DateOnly(2030, 1, 9)], exactly.Days);

        var after = Plan(new DateOnly(2030, 1, 8), null, At("2030-01-10T23:00:00").AddTicks(1));
        Assert.Equal(new DateOnly(2030, 1, 9), after.SkipThrough);
        Assert.Empty(after.Days);
    }

    [Fact]
    public void FirstRun_WithRawRowsOlderThanTheFloor_SkipsThem()
    {
        var plan = Plan(null, Jan1, At("2030-01-05T12:00:00"));

        Assert.Equal(new DateOnly(2030, 1, 3), plan.SkipThrough);
        Assert.Equal([new DateOnly(2030, 1, 4)], plan.Days);
    }

    private static RollupPlan Plan(DateOnly? watermark, DateOnly? earliestRawDay, DateTimeOffset now) =>
        RollupScheduler.Plan(watermark, earliestRawDay, now, new SuiteAnalyticsOptions());

    private static DateTimeOffset At(string utc) => DateTimeOffset.Parse(utc + "Z", System.Globalization.CultureInfo.InvariantCulture);
}
