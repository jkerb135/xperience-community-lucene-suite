using CMS.Websites.Routing;

using Kentico.PageBuilder.Web.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace XperienceCommunity.Lucene.Suite.Widgets.Search;

/// <summary>Kentico preview and Page Builder edit state of a request.</summary>
internal readonly record struct SuitePreview(bool IsPreview, bool IsEditMode);

/// <summary>Reads <see cref="SuitePreview"/>; replaceable in tests.</summary>
internal interface ISuitePreviewState
{
    SuitePreview Get(HttpContext context);
}

/// <summary>Reads the state from Kentico's request services (edit mode only exists inside preview).</summary>
internal sealed class KenticoPreviewState : ISuitePreviewState
{
    public SuitePreview Get(HttpContext context)
    {
        bool preview = context.RequestServices.GetRequiredService<IWebsiteChannelContext>().IsPreview;
        return new SuitePreview(preview, preview && context.RequestServices.GetRequiredService<IPageBuilderDataContextRetriever>().Retrieve().EditMode);
    }
}
