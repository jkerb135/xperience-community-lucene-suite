using CMS.ContactManagement;
using CMS.DataEngine;
using CMS.Membership;
using CMS.Membership.Internal;

using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

namespace XperienceCommunity.Lucene.Suite.Admin.Rules;

/// <summary>Contact groups offered as contexts (query tester, rule conditions), gated by Kentico's Contact groups → View.</summary>
internal static class ContactGroupAccess
{
    internal const int MaxGroups = 500;

    /// <summary>The groups, or nothing with Allowed false when the current user may not view Kentico's Contact groups application.</summary>
    public static async Task<QueryTesterContactGroups> Get(
        IAuthenticatedUserAccessor userAccessor, IApplicationPermissionEvaluator permissionEvaluator, IInfoProvider<ContactGroupInfo> contactGroups) =>
        await CanView(userAccessor, permissionEvaluator) ? Result(allowed: true, Load(contactGroups)) : Result(allowed: false, []);

    public static async Task<bool> CanView(IAuthenticatedUserAccessor userAccessor, IApplicationPermissionEvaluator permissionEvaluator)
    {
        var user = await userAccessor.Get();
        return user is not null && (user.IsAdministrator() || permissionEvaluator.Evaluate(new ApplicationPermissionEvaluationContext
        {
            ApplicationName = ContactGroupsApplication.IDENTIFIER,
            PermissionName = SystemPermissions.VIEW,
            User = user,
        }));
    }

    /// <summary>Contact groups that are not recipient lists, by display name, one more than shown so truncation is detectable.</summary>
    public static List<QueryTesterContactGroup> Load(IInfoProvider<ContactGroupInfo> contactGroups) =>
        contactGroups.Get()
            .WhereFalse(nameof(ContactGroupInfo.ContactGroupIsRecipientList))
            .Columns(nameof(ContactGroupInfo.ContactGroupName), nameof(ContactGroupInfo.ContactGroupDisplayName))
            .OrderBy(nameof(ContactGroupInfo.ContactGroupDisplayName))
            .TopN(MaxGroups + 1)
            .Select(group => new QueryTesterContactGroup(group.ContactGroupName, group.ContactGroupDisplayName))
            .ToList();

    /// <summary>At most <see cref="MaxGroups"/> groups; <paramref name="fetched"/> holds one more when there are more.</summary>
    public static QueryTesterContactGroups Result(bool allowed, IReadOnlyList<QueryTesterContactGroup> fetched) =>
        new(allowed, fetched.Count > MaxGroups, [.. fetched.Take(MaxGroups)]);
}
