using System.Collections.Concurrent;

using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Rules;

/// <summary>Loads the valid rules of an index through Kentico's cache, which any rule change clears (web farms included).</summary>
internal class RuleProvider(
    IInfoProvider<RuleInfo> rules,
    IProgressiveCache cache,
    IOptions<SuiteSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<RuleProvider> logger)
{
    private readonly ConcurrentDictionary<string, LogThrottle> invalidRuleThrottles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the parsed rules stored for <paramref name="indexName"/>; rows that fail validation are skipped and logged.</summary>
    /// <remarks>Enabled and validity are left to <see cref="RuleEngine"/>, so cached sets stay correct over time.</remarks>
    public virtual IReadOnlyList<RuleDefinition> GetRules(string indexName) =>
        cache.Load(
            settings =>
            {
                settings.CacheDependency = CacheHelper.GetCacheDependency($"{RuleInfo.OBJECT_TYPE}|all");
                return Load(indexName);
            },
            new CacheSettings(options.Value.Rules.CacheMinutes, "lucenesuite", "rules", indexName.ToLowerInvariant()));

    private IReadOnlyList<RuleDefinition> Load(string indexName)
    {
        var definitions = new List<RuleDefinition>();
        var skipped = new List<string>();
        foreach (var row in rules.Get().WhereEquals(nameof(RuleInfo.IndexName), indexName))
        {
            var result = row.ToDefinition();
            if (result.Definition is { } definition && string.Equals(definition.IndexName, indexName, StringComparison.OrdinalIgnoreCase))
            {
                definitions.Add(definition);
            }
            else if (result.Errors.Count > 0)
            {
                skipped.Add($"{row.RuleID} ({result.Errors[0].Path})");
            }
        }

        if (skipped.Count > 0 && invalidRuleThrottles.GetOrAdd(indexName, _ => new LogThrottle(timeProvider, TimeSpan.FromMinutes(1))).ShouldLog())
        {
            logger.LogWarning("Lucene Suite skipped invalid rules on index {Index}: {Rules}.", indexName, string.Join(", ", skipped));
        }

        return definitions;
    }
}
