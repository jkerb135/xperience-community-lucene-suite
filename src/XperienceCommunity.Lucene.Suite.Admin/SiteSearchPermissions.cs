namespace XperienceCommunity.Lucene.Suite.Admin;

/// <summary>Custom permissions of the Site Search application, assignable per role in the Roles application.</summary>
public static class SiteSearchPermissions
{
    /// <summary>Create, edit and delete search rules.</summary>
    public const string MANAGE_RULES = "LuceneSuite.ManageRules";

    /// <summary>Display name of <see cref="MANAGE_RULES"/>.</summary>
    public const string MANAGE_RULES_DISPLAY_NAME = "Manage rules";

    /// <summary>Create, edit and delete synonyms.</summary>
    public const string MANAGE_SYNONYMS = "LuceneSuite.ManageSynonyms";

    /// <summary>Display name of <see cref="MANAGE_SYNONYMS"/>.</summary>
    public const string MANAGE_SYNONYMS_DISPLAY_NAME = "Manage synonyms";

    /// <summary>Create, start and stop search experiments.</summary>
    public const string MANAGE_EXPERIMENTS = "LuceneSuite.ManageExperiments";

    /// <summary>Display name of <see cref="MANAGE_EXPERIMENTS"/>.</summary>
    public const string MANAGE_EXPERIMENTS_DISPLAY_NAME = "Manage experiments";
}
