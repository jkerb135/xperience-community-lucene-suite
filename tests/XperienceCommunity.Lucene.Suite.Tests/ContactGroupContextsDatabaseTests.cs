using CMS.ContactManagement;
using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.Lucene.Suite.Widgets.Search;

using static XperienceCommunity.Lucene.Suite.Tests.DatabaseTestHost;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>The contact-group membership query of <see cref="SuiteContactGroupContexts"/> on the disposable database.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ContactGroupContextsDatabaseTests
{
    private const string Prefix = "LuceneSuiteCgc";

    [DatabaseFact]
    public void LoadGroups_ReturnsTheContactsGroups_NotRecipientListsOrOtherContacts()
    {
        var services = KenticoServices.Value;
        var contacts = services.GetRequiredService<IInfoProvider<ContactInfo>>();
        var groups = services.GetRequiredService<IInfoProvider<ContactGroupInfo>>();
        var members = services.GetRequiredService<IInfoProvider<ContactGroupMemberInfo>>();
        Cleanup();
        try
        {
            var contact = new ContactInfo { ContactFirstName = "Search", ContactLastName = Prefix };
            var other = new ContactInfo { ContactFirstName = "Other", ContactLastName = Prefix };
            contacts.Set(contact);
            contacts.Set(other);
            var buyers = Group("Buyers", recipientList: false);
            var newsletter = Group("News", recipientList: true);
            var others = Group("Others", recipientList: false);
            foreach (var group in new[] { buyers, newsletter, others })
            {
                groups.Set(group);
            }

            members.Set(Member(buyers, contact));
            members.Set(Member(newsletter, contact));
            members.Set(Member(others, other));

            var loaded = new Probe(groups, members).Load(contact.ContactID);

            Assert.Equal([$"{Prefix}Buyers"], loaded);
        }
        finally
        {
            Cleanup();
        }
    }

    private static ContactGroupInfo Group(string name, bool recipientList) => new()
    {
        ContactGroupName = Prefix + name,
        ContactGroupDisplayName = name,
        ContactGroupIsRecipientList = recipientList,
        ContactGroupEnabled = true,
    };

    private static ContactGroupMemberInfo Member(ContactGroupInfo group, ContactInfo contact) => new()
    {
        ContactGroupMemberContactGroupID = group.ContactGroupID,
        ContactGroupMemberRelatedID = contact.ContactID,
        ContactGroupMemberType = ContactGroupMemberTypeEnum.Contact,
        ContactGroupMemberFromManual = true,
    };

    private static void Cleanup()
    {
        Execute($"DELETE m FROM OM_ContactGroupMember m JOIN OM_ContactGroup g ON g.ContactGroupID = m.ContactGroupMemberContactGroupID WHERE g.ContactGroupName LIKE '{Prefix}%'");
        Execute($"DELETE FROM OM_ContactGroup WHERE ContactGroupName LIKE '{Prefix}%'");
        Execute($"DELETE FROM OM_Contact WHERE ContactLastName = '{Prefix}'");
    }

    /// <summary>Exposes the real query; the contact and cache are not used by it.</summary>
    private sealed class Probe(IInfoProvider<ContactGroupInfo> groups, IInfoProvider<ContactGroupMemberInfo> members)
        : SuiteContactGroupContexts(null!, groups, members, null!, TimeProvider.System, NullLogger<SuiteContactGroupContexts>.Instance)
    {
        public List<string> Load(int contactId) => [.. LoadGroups(contactId)];
    }
}
