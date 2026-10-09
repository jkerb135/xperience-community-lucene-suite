using CMS;

using Kentico.Xperience.Admin.Base;

using XperienceCommunity.Lucene.Suite.Admin;

[assembly: RegisterModule(typeof(LuceneSuiteAdminModule))]
[assembly: AssemblyDiscoverable]

namespace XperienceCommunity.Lucene.Suite.Admin;

internal sealed class LuceneSuiteAdminModule : AdminModule
{
    public LuceneSuiteAdminModule()
        : base("XperienceCommunity.Lucene.Suite.Admin")
    {
    }

    protected override void OnInit()
    {
        base.OnInit();
        RegisterClientModule("xperience-community", "lucene-suite");
    }
}
