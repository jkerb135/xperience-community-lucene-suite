using CMS.Base;
using CMS.Core;
using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Suggestions;
using XperienceCommunity.Lucene.Suite.Synonyms;

namespace XperienceCommunity.Lucene.Suite;

/// <summary>Writes one event-log entry per create, update and delete of a rule, synonym or blocked suggestion; Kentico 31.9.1 writes none for these types.</summary>
internal static class SuiteChangeLog
{
    public const string Source = "LuceneSuite";
    public const int DescriptionMaxLength = 1000;

    public static void Register(IServiceProvider services)
    {
        Hook<RuleInfo>(services, RuleInfo.TYPEINFO, "RULE", rule => $"Rule {rule.RuleID} '{rule.DisplayName}' on index '{rule.IndexName}'.");
        Hook<SynonymInfo>(services, SynonymInfo.TYPEINFO, "SYNONYM", synonym => string.IsNullOrEmpty(synonym.Input)
            ? $"Synonym {synonym.SynonymID} ({synonym.Kind}) on index '{synonym.IndexName}': {synonym.Terms}"
            : $"Synonym {synonym.SynonymID} ({synonym.Kind}) on index '{synonym.IndexName}': {synonym.Input} -> {synonym.Terms}");
        Hook<BlockedSuggestionInfo>(services, BlockedSuggestionInfo.TYPEINFO, "BLOCKED_SUGGESTION", block => $"Blocked suggestion {block.BlockedSuggestionID} '{block.Text}' ({block.MatchMode}) on index '{block.IndexName}'.");
    }

    /// <summary>One line, at most <see cref="DescriptionMaxLength"/> characters.</summary>
    public static string Clip(string description)
    {
        string line = description.ReplaceLineEndings(" ");
        return line.Length <= DescriptionMaxLength ? line : string.Concat(line.AsSpan(0, DescriptionMaxLength - 1), "…");
    }

    private static void Hook<TInfo>(IServiceProvider services, ObjectTypeInfo type, string prefix, Func<TInfo, string> describe)
        where TInfo : BaseInfo
    {
        type.Events.Insert.After += (_, e) => Log(services, $"{prefix}_CREATE", e.Object, describe);
        type.Events.Update.After += (_, e) => Log(services, $"{prefix}_UPDATE", e.Object, describe);
        type.Events.Delete.After += (_, e) => Log(services, $"{prefix}_DELETE", e.Object, describe);
    }

    // The event log stamps the request's user itself; without one (API, background) the entry is written without it.
    private static void Log<TInfo>(IServiceProvider services, string code, BaseInfo info, Func<TInfo, string> describe)
        where TInfo : BaseInfo
    {
        if (!CMSActionContext.CurrentLogEvents)
        {
            return;
        }

        var eventLog = services.GetService<IEventLogService>();
        try
        {
            eventLog?.LogInformation(Source, code, Clip(describe((TInfo)info)));
        }
        catch (Exception exception)
        {
            // The change is already saved; a logging failure must not fail it.
            eventLog?.LogException(Source, "CHANGE_LOG", exception);
        }
    }
}
