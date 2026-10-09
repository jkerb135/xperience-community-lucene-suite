using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Lucene.Admin;

using XperienceCommunity.Lucene.Suite.Admin.UIPages;

[assembly: PageExtender(typeof(IndexListingPageExtender))]

namespace XperienceCommunity.Lucene.Suite.Admin.UIPages;

/// <summary>Adds a "Search tuning" action to each row of the Lucene index listing; the row click stays upstream's.</summary>
public sealed class IndexListingPageExtender : PageExtender<IndexListingPage>
{
    /// <inheritdoc />
    public override async Task ConfigurePage()
    {
        await base.ConfigurePage();
        Page.PageConfiguration.TableActions.AddLink<SearchTuningSectionPage>(
            "Search tuning",
            icon: Icons.Cogwheel,
            title: "Search tuning",
            parameters: new PageParameterValues { { typeof(SearchTuningSectionPage), ActionConfiguration.ROW_IDENTIFIER_PLACEHOLDER } });
    }
}
