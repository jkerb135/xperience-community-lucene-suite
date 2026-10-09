using Microsoft.Extensions.Options;

namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Rejects analytics settings that would skew or lose data; each failure names its configuration key.</summary>
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

        var minimumRaw = TimeSpan.FromDays(1) + a.Attribution.AttributionWindow + a.Job.RollupInterval + TimeSpan.FromHours(1);
        if (a.Retention.RawRetention < minimumRaw)
        {
            failures.Add($"{Prefix}Retention:{nameof(a.Retention.RawRetention)} must be at least 1 day + Attribution:AttributionWindow + Job:RollupInterval + 1 h = {minimumRaw}, so raw rows outlive the rollup of their day (was {a.Retention.RawRetention}).");
        }

        if (a.Retention.DailyRetention < TimeSpan.FromDays(1))
        {
            failures.Add($"{Prefix}Retention:{nameof(a.Retention.DailyRetention)} must be at least 1 day (was {a.Retention.DailyRetention}).");
        }

        Positive(a.Retention.SubThresholdPruneAfter, "Retention:" + nameof(a.Retention.SubThresholdPruneAfter));
        AtLeastOne(a.Surfacing.MinSearches, "Surfacing:" + nameof(a.Surfacing.MinSearches));
        AtLeastOne(a.Surfacing.MinDistinctDays, "Surfacing:" + nameof(a.Surfacing.MinDistinctDays));

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
