using CMS.ContactManagement;
using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Logging;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Search;

/// <summary>Code names of the current contact's contact groups (recipient lists excluded), for search contexts.</summary>
internal class SuiteContactGroupContexts(
    ICurrentContactProvider currentContact,
    IInfoProvider<ContactGroupInfo> groups,
    IInfoProvider<ContactGroupMemberInfo> members,
    IProgressiveCache cache,
    TimeProvider timeProvider,
    ILogger<SuiteContactGroupContexts> logger)
{
    public const int MaxGroups = 50;

    private readonly LogThrottle failures = new(timeProvider, TimeSpan.FromMinutes(1));

    /// <summary>Never creates a contact; empty when there is none or the lookup fails.</summary>
    public IReadOnlyCollection<string> Get()
    {
        try
        {
            if (CurrentContactId() is not { } contactId)
            {
                return [];
            }

            return cache.Load(
                settings =>
                {
                    if (settings.Cached)
                    {
                        settings.CacheDependency = CacheHelper.GetCacheDependency([$"{ContactGroupMemberInfo.OBJECT_TYPE_CONTACT}|all", $"{ContactGroupInfo.OBJECT_TYPE}|all"]);
                    }

                    return LoadGroups(contactId)
                        .Where(name => !string.IsNullOrWhiteSpace(name) && name.Length <= SearchExecutor.MaxContextLength)
                        .Take(MaxGroups)
                        .ToList();
                },
                new CacheSettings(5, "lucenesuite", "contactgroups", contactId));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (failures.ShouldLog())
            {
                logger.LogWarning(exception, "Lucene Suite could not read the current contact's contact groups; searching without them.");
            }

            return [];
        }
    }

    protected virtual int? CurrentContactId() => currentContact.GetExistingContact()?.ContactID;

    protected virtual IEnumerable<string> LoadGroups(int contactId) =>
        groups.Get()
            .WhereFalse(nameof(ContactGroupInfo.ContactGroupIsRecipientList))
            .WhereIn(nameof(ContactGroupInfo.ContactGroupID), members.Get()
                .WhereEquals(nameof(ContactGroupMemberInfo.ContactGroupMemberRelatedID), contactId)
                .WhereEquals(nameof(ContactGroupMemberInfo.ContactGroupMemberType), (int)ContactGroupMemberTypeEnum.Contact)
                .Column(nameof(ContactGroupMemberInfo.ContactGroupMemberContactGroupID)))
            .Column(nameof(ContactGroupInfo.ContactGroupName))
            .OrderBy(nameof(ContactGroupInfo.ContactGroupName))
            .TopN(MaxGroups + 1)
            .GetListResult<string>();
}
