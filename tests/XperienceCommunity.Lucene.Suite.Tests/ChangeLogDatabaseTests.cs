using CMS.Base;
using CMS.DataEngine;
using CMS.Modules;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Suggestions;
using XperienceCommunity.Lucene.Suite.Synonyms;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class ChangeLogDatabaseTests
{
    private const string Index = "changelog-test";

    [DatabaseFact]
    public void EveryChange_WritesOneEntry_ExceptUnderLogEventsOff()
    {
        var services = KenticoServices.Value;
        new AnalyticsModuleInstaller(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            services.GetRequiredService<IInfoProvider<CMS.Activities.ActivityTypeInfo>>(),
            NullLogger<AnalyticsModuleInstaller>.Instance).Install();
        var rules = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        var synonyms = services.GetRequiredService<IInfoProvider<SynonymInfo>>();
        var blocks = services.GetRequiredService<IInfoProvider<BlockedSuggestionInfo>>();
        int start = Scalar("SELECT ISNULL(MAX(EventID), 0) FROM CMS_EventLog");
        Cleanup(start);
        try
        {
            var rule = new RuleInfo { IndexName = Index, DisplayName = "Audit rule", Enabled = true, Priority = 1, Conditions = "[]", Actions = """{"hides":[]}""" };
            rules.Set(rule);
            rule.DisplayName = "Audit rule renamed";
            rules.Set(rule);
            rules.Delete(rule);

            var synonym = new SynonymInfo { IndexName = Index, Kind = SynonymSerializer.TwoWay, Terms = """["grinder","mill"]""", Enabled = true };
            synonyms.Set(synonym);
            synonym.Enabled = false;
            synonyms.Set(synonym);
            synonyms.Delete(synonym);

            var block = new BlockedSuggestionInfo { IndexName = Index, Text = "Coffee", MatchKey = "coffee", MatchMode = BlockedSuggestionInfo.MatchModeExact };
            blocks.Set(block);
            block.MatchMode = BlockedSuggestionInfo.MatchModeWord;
            blocks.Set(block);
            blocks.Delete(block);

            var muted = new RuleInfo { IndexName = Index, DisplayName = "Muted rule", Enabled = true, Priority = 2, Conditions = "[]", Actions = """{"hides":[]}""" };
            using (new CMSActionContext { LogEvents = false })
            {
                rules.Set(muted);
                rules.Delete(muted);
            }

            string longTerms = $"[{string.Join(",", Enumerable.Range(0, 20).Select(i => $"\"{new string((char)('a' + i), 100)}\""))}]";
            var longSynonym = new SynonymInfo { IndexName = Index, Kind = SynonymSerializer.TwoWay, Terms = longTerms, Enabled = true };
            synonyms.Set(longSynonym);

            // The event log may write through a background queue; the last change arriving means every earlier one has.
            var entries = WaitFor(start, $"Synonym {longSynonym.SynonymID} ");

            Assert.Equal(["RULE_CREATE", "RULE_UPDATE", "RULE_DELETE"], Codes(entries, $"Rule {rule.RuleID} "));
            Assert.Equal(["SYNONYM_CREATE", "SYNONYM_UPDATE", "SYNONYM_DELETE"], Codes(entries, $"Synonym {synonym.SynonymID} "));
            Assert.Equal(["BLOCKED_SUGGESTION_CREATE", "BLOCKED_SUGGESTION_UPDATE", "BLOCKED_SUGGESTION_DELETE"], Codes(entries, $"Blocked suggestion {block.BlockedSuggestionID} "));
            Assert.Empty(Codes(entries, $"Rule {muted.RuleID} "));
            Assert.Contains(entries, entry => entry.Description == $"Rule {rule.RuleID} 'Audit rule renamed' on index '{Index}'.");
            Assert.Contains(entries, entry => entry.Description == $"Blocked suggestion {block.BlockedSuggestionID} 'Coffee' (word) on index '{Index}'.");

            string description = Assert.Single(entries, entry => entry.Description.StartsWith($"Synonym {longSynonym.SynonymID} ", StringComparison.Ordinal)).Description;
            Assert.Equal(SuiteChangeLog.DescriptionMaxLength, description.Length);
            Assert.EndsWith("…", description);
            Assert.DoesNotContain('\n', description);
        }
        finally
        {
            Cleanup(start);
        }
    }

    private static string[] Codes(List<(string Code, string Description)> entries, string prefix) =>
        entries.Where(entry => entry.Description.StartsWith(prefix, StringComparison.Ordinal)).Select(entry => entry.Code).ToArray();

    private static List<(string Code, string Description)> WaitFor(int afterEventId, string lastPrefix)
    {
        for (int attempt = 0; ; attempt++)
        {
            var entries = Entries(afterEventId);
            if (entries.Any(entry => entry.Description.StartsWith(lastPrefix, StringComparison.Ordinal)) || attempt == 50)
            {
                return entries;
            }

            Thread.Sleep(100);
        }
    }

    private static List<(string Code, string Description)> Entries(int afterEventId)
    {
        using var connection = Open();
        using var command = new SqlCommand("SELECT EventCode, EventDescription FROM CMS_EventLog WHERE Source = @source AND EventID > @start ORDER BY EventID", connection);
        command.Parameters.AddWithValue("@source", SuiteChangeLog.Source);
        command.Parameters.AddWithValue("@start", afterEventId);
        using var reader = command.ExecuteReader();
        var entries = new List<(string, string)>();
        while (reader.Read())
        {
            entries.Add((reader.GetString(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }

        return entries;
    }

    private static void Cleanup(int afterEventId)
    {
        Execute($"DELETE FROM LuceneSuite_Rule WHERE IndexName = '{Index}'");
        Execute($"DELETE FROM LuceneSuite_Synonym WHERE IndexName = '{Index}'");
        Execute($"DELETE FROM LuceneSuite_BlockedSuggestion WHERE IndexName = '{Index}'");
        Execute($"DELETE FROM CMS_EventLog WHERE Source = '{SuiteChangeLog.Source}' AND EventID > {afterEventId} AND EventDescription LIKE '%''{Index}''%'");
    }
}
