using CMS.ContactManagement;
using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class QueryTesterDatabaseTests
{
    private const string Prefix = "LuceneSuiteQt";

    [DatabaseFact]
    public void ContactGroups_ExcludeRecipientLists_AndAreOrderedByDisplayName()
    {
        var groups = KenticoServices.Value.GetRequiredService<IInfoProvider<ContactGroupInfo>>();
        Cleanup();
        try
        {
            groups.Set(Group("Zeta", "Zeta buyers", recipientList: false));
            groups.Set(Group("Alpha", "Alpha buyers", recipientList: false));
            groups.Set(Group("News", "Alpha newsletter", recipientList: true));

            var ours = QueryTesterPage.LoadContactGroups(groups).Where(group => group.Name.StartsWith(Prefix, StringComparison.Ordinal)).ToList();

            Assert.Equal(
                [new QueryTesterContactGroup($"{Prefix}Alpha", "Alpha buyers"), new QueryTesterContactGroup($"{Prefix}Zeta", "Zeta buyers")],
                ours);
        }
        finally
        {
            Cleanup();
        }
    }

    private static ContactGroupInfo Group(string name, string displayName, bool recipientList) => new()
    {
        ContactGroupName = Prefix + name,
        ContactGroupDisplayName = displayName,
        ContactGroupIsRecipientList = recipientList,
        ContactGroupEnabled = true,
    };

    private static void Cleanup() => Execute($"DELETE FROM OM_ContactGroup WHERE ContactGroupName LIKE '{Prefix}%'");
}
