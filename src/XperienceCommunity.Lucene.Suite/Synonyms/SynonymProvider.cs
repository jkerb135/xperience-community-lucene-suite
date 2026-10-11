using System.Collections.Concurrent;

using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Synonyms;

/// <summary>Loads the enabled, valid synonyms of an index through Kentico's cache, which any synonym change clears (web farms included).</summary>
internal class SynonymProvider(
    IInfoProvider<SynonymInfo> synonyms,
    IProgressiveCache cache,
    IOptions<SuiteSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<SynonymProvider> logger)
{
    private readonly ConcurrentDictionary<string, LogThrottle> invalidThrottles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the lookup for <paramref name="indexName"/>; rows that fail validation are skipped and logged.</summary>
    public virtual SynonymSet GetSynonyms(string indexName) =>
        cache.Load(
            settings =>
            {
                settings.CacheDependency = CacheHelper.GetCacheDependency($"{SynonymInfo.OBJECT_TYPE}|all");
                return Load(indexName);
            },
            new CacheSettings(options.Value.Synonyms.CacheMinutes, "lucenesuite", "synonyms", indexName.ToLowerInvariant()));

    private SynonymSet Load(string indexName)
    {
        var definitions = new List<SynonymDefinition>();
        var skipped = new List<string>();
        var rows = synonyms.Get()
            .WhereEquals(nameof(SynonymInfo.IndexName), indexName)
            .WhereTrue(nameof(SynonymInfo.Enabled))
            .Columns(nameof(SynonymInfo.SynonymID), nameof(SynonymInfo.IndexName), nameof(SynonymInfo.Kind), nameof(SynonymInfo.Input), nameof(SynonymInfo.Terms));
        foreach (var row in rows)
        {
            var result = row.ToDefinition();
            if (result.Definition is { } definition)
            {
                definitions.Add(definition);
            }
            else
            {
                skipped.Add($"{row.SynonymID} ({result.Errors[0].Path})");
            }
        }

        if (skipped.Count > 0 && invalidThrottles.GetOrAdd(indexName, _ => new LogThrottle(timeProvider, TimeSpan.FromMinutes(1))).ShouldLog())
        {
            logger.LogWarning("Lucene Suite skipped invalid synonyms on index {Index}: {Synonyms}.", indexName, string.Join(", ", skipped));
        }

        return definitions.Count == 0 ? SynonymSet.Empty : new SynonymSet(definitions);
    }
}
