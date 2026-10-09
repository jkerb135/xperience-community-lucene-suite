using CMS.Base;
using CMS.Membership;
using CMS.Membership.Internal;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Lucene.Admin;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

[assembly: PageExtender(typeof(IndexListingPageExtender))]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Adds a "Search tuning" shortcut into the Site Search app to each row of the Lucene index listing; hidden for users without Site Search → View.</summary>
public sealed class IndexListingPageExtender(
    IAuthenticatedUserAccessor userAccessor,
    IApplicationPermissionEvaluator applicationPermissionEvaluator) : PageExtender<IndexListingPage>
{
    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        await base.ConfigurePage();

        // Kentico activates extenders per request, so this evaluates once per listing request, not per row.
        var canViewSiteSearch = new Lazy<Task<bool>>(CanViewSiteSearch);
        Page.PageConfiguration.TableActions.AddLink<SearchTuningSectionPage>(new AddLinkParameters("Search tuning")
        {
            Icon = Icons.Cogwheel,
            Title = "Search tuning",
            PageParameterValues = new PageParameterValues { { typeof(SearchTuningSectionPage), ActionConfiguration.ROW_IDENTIFIER_PLACEHOLDER } },
            ActionStateEvaluator = VisibleWhen(canViewSiteSearch),
        });
    }

    internal static Func<ActionConfiguration, IDataContainer, CancellationToken, Task> VisibleWhen(Lazy<Task<bool>> permitted) =>
        async (action, _, _) => action.Visible = await permitted.Value;

    private async Task<bool> CanViewSiteSearch()
    {
        var user = await userAccessor.Get();
        return user is not null && (user.IsAdministrator() || applicationPermissionEvaluator.Evaluate(new ApplicationPermissionEvaluationContext
        {
            ApplicationName = SiteSearchApplication.IDENTIFIER,
            PermissionName = SystemPermissions.VIEW,
            User = user,
        }));
    }
}
