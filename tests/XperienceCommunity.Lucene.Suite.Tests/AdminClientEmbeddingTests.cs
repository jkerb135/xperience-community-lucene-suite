#if !DEBUG
using XperienceCommunity.Lucene.Suite.Admin.UIPages;

namespace XperienceCommunity.Lucene.Suite.Tests;

public class AdminClientEmbeddingTests
{
    [Fact]
    public void ReleaseBuild_EmbedsTheAdminClientBundle()
    {
        var resources = typeof(QueryTesterPage).Assembly.GetManifestResourceNames();

        Assert.Contains(resources, name => name.Contains("AdminResources", StringComparison.Ordinal) && name.EndsWith(".js", StringComparison.Ordinal));
    }
}
#endif
