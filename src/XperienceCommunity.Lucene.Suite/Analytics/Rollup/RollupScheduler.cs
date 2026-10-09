using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Days one rollup run handles: <paramref name="SkipThrough"/> (if set) advances the watermark without rolling, then <paramref name="Days"/> are rolled in order.</summary>
internal sealed record RollupPlan(DateOnly? SkipThrough, IReadOnlyList<DateOnly> Days);

/// <summary>Chooses the UTC days to roll: a pure function of the watermark, the earliest raw day, the clock and the settings.</summary>
internal static class RollupScheduler
{
    /// <summary>Safety margin after the last moment a day's input rows can arrive.</summary>
    internal static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    /// <summary>Plans one run; <paramref name="earliestRawDay"/> is used only when there is no watermark.</summary>
    public static RollupPlan Plan(DateOnly? watermark, DateOnly? earliestRawDay, DateTimeOffset now, SuiteAnalyticsOptions options)
    {
        if ((watermark?.AddDays(1) ?? earliestRawDay) is not DateOnly start)
        {
            return new RollupPlan(null, []);
        }

        var window = options.Attribution.AttributionWindow;
        var utcNow = now.UtcDateTime;

        // A day whose input window starts before raw retention may have lost rows; rolling it would replace good KPIs with partial ones.
        var firstComplete = CeilingDay(utcNow - options.Retention.RawRetention + window);
        DateOnly? skipThrough = start < firstComplete ? firstComplete.AddDays(-1) : null;
        var day = skipThrough is null ? start : firstComplete;

        var completeAfter = TimeSpan.FromDays(1) + (2 * window) + options.FlushInterval + Margin;
        var days = new List<DateOnly>();
        while (days.Count < options.Job.MaxDaysPerRun && Midnight(day) + completeAfter <= utcNow)
        {
            days.Add(day);
            day = day.AddDays(1);
        }

        return new RollupPlan(skipThrough, days);
    }

    /// <summary>The day's start as a UTC instant.</summary>
    internal static DateTime Midnight(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>The first day whose midnight is at or after <paramref name="instant"/>.</summary>
    internal static DateOnly CeilingDay(DateTime instant)
    {
        var day = DateOnly.FromDateTime(instant);
        return Midnight(day) < instant ? day.AddDays(1) : day;
    }
}
