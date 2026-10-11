using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics.Rollup;

namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Rejects analytics and rule settings that would skew or lose data; each failure names its configuration key.</summary>
internal sealed class SuiteSearchOptionsValidator : IValidateOptions<SuiteSearchOptions>
{
    private const string Prefix = SuiteSearchOptions.SectionName + ":Analytics:";

    public ValidateOptionsResult Validate(string? name, SuiteSearchOptions options)
    {
        var a = options.Analytics;
        var failures = new List<string>();

        void Positive(TimeSpan value, string key)
        {
            if (value <= TimeSpan.Zero)
            {
                failures.Add($"{Prefix}{key} must be positive (was {value}).");
            }
        }

        void AtLeastOne(int value, string key)
        {
            if (value < 1)
            {
                failures.Add($"{Prefix}{key} must be at least 1 (was {value}).");
            }
        }

        AtLeastOne(a.QueueCapacity, nameof(a.QueueCapacity));
        AtLeastOne(a.BatchSize, nameof(a.BatchSize));
        Positive(a.FlushInterval, nameof(a.FlushInterval));

        Positive(a.Merge.TextChangeWindow, "Merge:" + nameof(a.Merge.TextChangeWindow));
        AtLeastOne(a.Merge.TypoOneEditMinLength, "Merge:" + nameof(a.Merge.TypoOneEditMinLength));
        if (a.Merge.TypoTwoEditMinLength <= a.Merge.TypoOneEditMinLength)
        {
            failures.Add($"{Prefix}Merge:{nameof(a.Merge.TypoTwoEditMinLength)} must be greater than Merge:{nameof(a.Merge.TypoOneEditMinLength)} (was {a.Merge.TypoTwoEditMinLength} ≤ {a.Merge.TypoOneEditMinLength}).");
        }

        // Negated so NaN fails too.
        if (!(a.Merge.DeepBackspaceRatio > 0 && a.Merge.DeepBackspaceRatio < 1))
        {
            failures.Add($"{Prefix}Merge:{nameof(a.Merge.DeepBackspaceRatio)} must be between 0 and 1, exclusive (was {a.Merge.DeepBackspaceRatio}).");
        }

        Positive(a.Attribution.AttributionWindow, "Attribution:" + nameof(a.Attribution.AttributionWindow));
        Positive(a.Job.RollupInterval, "Job:" + nameof(a.Job.RollupInterval));
        AtLeastOne(a.Job.MaxDaysPerRun, "Job:" + nameof(a.Job.MaxDaysPerRun));

        // A day becomes rollable after 1 day + 2 × window + flush + margin and stops being rollable once its input window (start − window) leaves raw retention.
        var minimumRaw = TimeSpan.FromDays(1) + (3 * a.Attribution.AttributionWindow) + a.FlushInterval + RollupScheduler.Margin + a.Job.RollupInterval + TimeSpan.FromHours(1);
        if (a.Retention.RawRetention < minimumRaw)
        {
            failures.Add($"{Prefix}Retention:{nameof(a.Retention.RawRetention)} must be at least 1 day + 3 × Attribution:AttributionWindow + FlushInterval + 5 min + Job:RollupInterval + 1 h = {minimumRaw}, so every day stays rollable for at least one run (was {a.Retention.RawRetention}).");
        }

        if (a.Retention.RawHardLimit < a.Retention.RawRetention)
        {
            failures.Add($"{Prefix}Retention:{nameof(a.Retention.RawHardLimit)} must be at least Retention:RawRetention = {a.Retention.RawRetention} (was {a.Retention.RawHardLimit}).");
        }

        if (a.Retention.DailyRetention < TimeSpan.FromDays(1))
        {
            failures.Add($"{Prefix}Retention:{nameof(a.Retention.DailyRetention)} must be at least 1 day (was {a.Retention.DailyRetention}).");
        }

        Positive(a.Retention.SubThresholdPruneAfter, "Retention:" + nameof(a.Retention.SubThresholdPruneAfter));
        AtLeastOne(a.Surfacing.MinSearches, "Surfacing:" + nameof(a.Surfacing.MinSearches));
        AtLeastOne(a.Surfacing.MinDistinctDays, "Surfacing:" + nameof(a.Surfacing.MinDistinctDays));

        const string rules = SuiteSearchOptions.SectionName + ":Rules:";
        if (!(options.Rules.MaxBoostFactor > 1 && options.Rules.MaxBoostFactor <= 1000))
        {
            failures.Add($"{rules}{nameof(options.Rules.MaxBoostFactor)} must be greater than 1 and at most 1000 (was {options.Rules.MaxBoostFactor}).");
        }

        if (options.Rules.CacheMinutes is < 0 or > 1440)
        {
            failures.Add($"{rules}{nameof(options.Rules.CacheMinutes)} must be between 0 and 1440 (was {options.Rules.CacheMinutes}).");
        }

        const string synonyms = SuiteSearchOptions.SectionName + ":Synonyms:";
        if (!(options.Synonyms.Weight > 0 && options.Synonyms.Weight <= 1))
        {
            failures.Add($"{synonyms}{nameof(options.Synonyms.Weight)} must be greater than 0 and at most 1 (was {options.Synonyms.Weight}).");
        }

        if (options.Synonyms.MaxExpansions is < 1 or > 50)
        {
            failures.Add($"{synonyms}{nameof(options.Synonyms.MaxExpansions)} must be between 1 and 50 (was {options.Synonyms.MaxExpansions}).");
        }

        if (options.Synonyms.CacheMinutes is < 0 or > 1440)
        {
            failures.Add($"{synonyms}{nameof(options.Synonyms.CacheMinutes)} must be between 0 and 1440 (was {options.Synonyms.CacheMinutes}).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
