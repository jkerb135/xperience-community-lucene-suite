using System.Reflection;
using System.Runtime.CompilerServices;

using CMS.DataEngine;
using CMS.Modules;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Forms;
using Kentico.Xperience.Lucene.Core.Indexing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.Lucene.Suite.Admin.Rules;
using XperienceCommunity.Lucene.Suite.Admin.Synonyms;
using XperienceCommunity.Lucene.Suite.Admin.UIPages;
using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Rules;
using XperienceCommunity.Lucene.Suite.Synonyms;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Saves through the edit pages' ProcessFormData as an open form does: a successful save reloads the page, whose token comes from the database.</summary>
/// <remarks>Kentico components need its services, hence the database host; the items mimic the edit pages' shape (categories are siblings of their components).</remarks>
[Collection(DatabaseCollection.Name)]
public sealed class EditPageSaveDatabaseTests
{
    private const string Index = "edit-save-test";
    private const int IndexId = 41;

    [DatabaseFact]
    public async Task Rule_EachSaveReloadsTheEditPage_AndAStaleTokenIsRefusedEveryTime()
    {
        var services = Install();
        var rules = services.GetRequiredService<IInfoProvider<RuleInfo>>();
        var links = services.GetRequiredService<IPageLinkGenerator>();
        Cleanup();
        try
        {
            var rule = new RuleInfo
            {
                IndexName = Index,
                DisplayName = "Coffee",
                Enabled = true,
                Priority = 1,
                Conditions = """[{"anchoring":"contains","pattern":"coffee"}]""",
                Actions = """{"boosts":[{"field":"Type","value":"Article","strength":40}]}""",
            };
            rules.Set(rule);
            var page = new RuleEditPage(null!, null!, new OneIndex(), rules, null!, links) { IndexIdentifier = IndexId, RuleId = rule.RuleID };
            string editPath = links.GetPath<RuleEditPage>(new PageParameterValues { { typeof(SearchTuningSectionPage), IndexId }, { typeof(RuleEditPage), rule.RuleID } });

            foreach (string name in new[] { "Coffee 2", "Coffee 3", "Coffee 4" })
            {
                // What the (re)loaded edit page puts in the hidden field.
                string? token = RuleEditMapping.FromRule(StoredRule.From(Stored(rules, rule.RuleID))).LoadedVersion;

                var response = Assert.IsType<NavigateResponse>(await SaveRule(page, rule.RuleID, name, token));

                Assert.Equal((editPath, true, "Rule saved."), (response.NavigationUrl, response.RefetchAll, Messages(response)));
                Assert.Equal(name, Stored(rules, rule.RuleID).DisplayName);
            }

            // Another editor saves: the open form's token is now stale, and a retry with the returned token stays refused.
            string? stale = RuleEditMapping.FromRule(StoredRule.From(Stored(rules, rule.RuleID))).LoadedVersion;
            Execute($"UPDATE LuceneSuite_Rule SET LastModified = DATEADD(second, 1, LastModified) WHERE RuleID = {rule.RuleID}");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var response = (ICommandResponse<FormSubmissionResult>)await SaveRule(page, rule.RuleID, "Mine", stale);
                var items = response.Result.Items;

                Assert.Equal((FormSubmissionStatus.ValidationFailure, RuleEditPageBase.ConcurrencyMessage), (response.Result.Status, Messages(response)));
                Assert.Equal(stale, Token(items));
                Assert.True(Assert.Single(items.OfType<RuleStatusClientProperties>()).Conflict);
                Assert.Equal("Coffee 4", Stored(rules, rule.RuleID).DisplayName);
                stale = Token(items);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    [DatabaseFact]
    public async Task Synonym_EachSaveReloadsTheEditPage_AndAStaleTokenIsRefusedEveryTime()
    {
        var services = Install();
        var synonyms = services.GetRequiredService<IInfoProvider<SynonymInfo>>();
        var links = services.GetRequiredService<IPageLinkGenerator>();
        Cleanup();
        try
        {
            var synonym = new SynonymInfo { IndexName = Index, Kind = SynonymSerializer.TwoWay, Input = string.Empty, Terms = """["grinder","mill"]""", Enabled = true };
            synonyms.Set(synonym);
            var page = new SynonymEditPage(null!, null!, new OneIndex(), synonyms, null!, links) { IndexIdentifier = IndexId, SynonymId = synonym.SynonymID };
            string editPath = links.GetPath<SynonymEditPage>(new PageParameterValues { { typeof(SearchTuningSectionPage), IndexId }, { typeof(SynonymEditPage), synonym.SynonymID } });

            foreach (string third in new[] { "burr", "quern", "hopper" })
            {
                string? token = SynonymEditMapping.FromSynonym(StoredSynonym.From(Stored(synonyms, synonym.SynonymID))).LoadedVersion;

                var response = Assert.IsType<NavigateResponse>(await SaveSynonym(page, $"""["grinder","mill","{third}"]""", token));

                Assert.Equal((editPath, true, "Synonym saved."), (response.NavigationUrl, response.RefetchAll, Messages(response)));
                Assert.Contains(third, Stored(synonyms, synonym.SynonymID).Terms, StringComparison.Ordinal);
            }

            string? stale = SynonymEditMapping.FromSynonym(StoredSynonym.From(Stored(synonyms, synonym.SynonymID))).LoadedVersion;
            Execute($"UPDATE LuceneSuite_Synonym SET LastModified = DATEADD(second, 1, LastModified) WHERE SynonymID = {synonym.SynonymID}");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var response = (ICommandResponse<FormSubmissionResult>)await SaveSynonym(page, """["grinder","mine"]""", stale);

                Assert.Equal((FormSubmissionStatus.ValidationFailure, SynonymEditPageBase.ConcurrencyMessage), (response.Result.Status, Messages(response)));
                Assert.Equal(stale, Token(response.Result.Items));
                Assert.Contains("hopper", Stored(synonyms, synonym.SynonymID).Terms, StringComparison.Ordinal);
                stale = Token(response.Result.Items);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    private static Task<ICommandResponse> SaveRule(RuleEditPage page, int ruleId, string name, string? token)
    {
        var model = RuleEditMapping.FromRule(StoredRule.From(Stored(KenticoServices.Value.GetRequiredService<IInfoProvider<RuleInfo>>(), ruleId)));
        model.DisplayName = name;
        model.LoadedVersion = token;
        ICollection<IFormItem> items =
        [
            new FormCategory { Title = "General", Order = 1 },
            Named(new RuleStatusFormComponent(), nameof(RuleEditModel.Status), null),
            new FormCategory { Title = "Custom data", Order = 30 },
            Named(new RuleHiddenFormComponent(), nameof(RuleEditModel.LoadedVersion), token),
        ];
        return Process(page, model, items);
    }

    private static Task<ICommandResponse> SaveSynonym(SynonymEditPage page, string terms, string? token)
    {
        var model = new SynonymEditModel { Kind = SynonymSerializer.TwoWay, Terms = terms, Enabled = true, LoadedVersion = token };
        return Process(page, model, [Named(new RuleHiddenFormComponent(), nameof(SynonymEditModel.LoadedVersion), token)]);
    }

    // ProcessFormData is what Kentico's Submit calls after binding the client's values into the model and the items.
    private static Task<ICommandResponse> Process(object page, object model, ICollection<IFormItem> items) =>
        (Task<ICommandResponse>)page.GetType().BaseType!.GetMethod("ProcessFormData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [model, items])!;

    // Kentico names components while building the form (IFormComponent.SetName is internal).
    private static IFormComponent Named(IFormComponent component, string name, string? value)
    {
        typeof(IFormComponent).GetMethod("SetName", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, [name]);
        component.SetObjectValue(value);
        return component;
    }

    private static string Messages(ICommandResponse response) => string.Join(" ", response.Messages.Select(message => message.Message));

    private static string? Token(IEnumerable<IFormItemClientProperties> items) =>
        items.OfType<FormComponentClientProperties<string>>().Single(item => item.Name == nameof(RuleEditModel.LoadedVersion)).Value;

    private static RuleInfo Stored(IInfoProvider<RuleInfo> rules, int id) => rules.Get().WhereEquals(nameof(RuleInfo.RuleID), id).First();

    private static SynonymInfo Stored(IInfoProvider<SynonymInfo> synonyms, int id) => synonyms.Get().WhereEquals(nameof(SynonymInfo.SynonymID), id).First();

    private static IServiceProvider Install()
    {
        var services = KenticoServices.Value;
        new AnalyticsModuleInstaller(
            services.GetRequiredService<IInfoProvider<ResourceInfo>>(),
            services.GetRequiredService<IInfoProvider<SettingsKeyInfo>>(),
            services.GetRequiredService<IInfoProvider<CMS.Activities.ActivityTypeInfo>>(),
            NullLogger<AnalyticsModuleInstaller>.Instance).Install();
        return services;
    }

    private static void Cleanup() =>
        Execute($"DELETE FROM LuceneSuite_Rule WHERE IndexName = '{Index}'; DELETE FROM LuceneSuite_Synonym WHERE IndexName = '{Index}'");

    private sealed class OneIndex : ILuceneIndexManager
    {
        public LuceneIndex? GetIndex(int identifier)
        {
            if (identifier != IndexId)
            {
                return null;
            }

            // LuceneIndex has only an internal constructor; the pages read nothing but the name.
            var index = (LuceneIndex)RuntimeHelpers.GetUninitializedObject(typeof(LuceneIndex));
            typeof(LuceneIndex).GetField("<IndexName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(index, Index);
            return index;
        }

        public LuceneIndex? GetIndex(string indexName) => throw new NotSupportedException();

        public IEnumerable<LuceneIndex> GetAllIndices() => throw new NotSupportedException();

        public LuceneIndex GetRequiredIndex(string indexName) => throw new NotSupportedException();

        public void AddIndex(LuceneIndexModel indexConfiguration) => throw new NotSupportedException();
    }
}
