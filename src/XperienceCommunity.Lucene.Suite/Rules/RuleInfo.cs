using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Rules;

[assembly: RegisterObjectType(typeof(RuleInfo), RuleInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Rules;

/// <summary>A stored search rule; conditions and actions are JSON validated by <see cref="RuleSerializer"/>.</summary>
public class RuleInfo : AbstractInfo<RuleInfo, IInfoProvider<RuleInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.rule";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.Rule";

    /// <summary>Maximum length of <see cref="DisplayName"/>.</summary>
    public const int DisplayNameMaxLength = 200;

    /// <summary>Type information.</summary>
    /// <remarks>CI/CD is off because rules are marketer data edited in production; a restore would overwrite them.</remarks>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<RuleInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(RuleID),
        nameof(LastModified),
        nameof(RuleGuid),
        null,
        nameof(DisplayName),
        null,
        null,
        null)
    {
        ContinuousIntegrationSettings = { Enabled = false },
        LogEvents = true,
        TouchCacheDependencies = true,
        SupportsInvalidation = true,
        SupportsCloning = false,
    };

    /// <summary>Creates an empty instance.</summary>
    public RuleInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public RuleInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int RuleID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(RuleID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(RuleID), value);
    }

    /// <summary>Stable identifier.</summary>
    [DatabaseField]
    public virtual Guid RuleGuid
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(RuleGuid)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(RuleGuid), value);
    }

    /// <summary>Targeted Kentico Lucene index.</summary>
    [DatabaseField]
    public virtual string IndexName
    {
        get => ValidationHelper.GetString(GetValue(nameof(IndexName)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(IndexName), value);
    }

    /// <summary>Name shown to marketers.</summary>
    [DatabaseField]
    public virtual string DisplayName
    {
        get => ValidationHelper.GetString(GetValue(nameof(DisplayName)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(DisplayName), value);
    }

    /// <summary>Optional notes.</summary>
    [DatabaseField]
    public virtual string? Description
    {
        get => GetValue(nameof(Description)) as string;
        set => SetValue(nameof(Description), value);
    }

    /// <summary>Whether the rule can fire.</summary>
    [DatabaseField]
    public virtual bool Enabled
    {
        get => ValidationHelper.GetBoolean(GetValue(nameof(Enabled)), false, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Enabled), value);
    }

    /// <summary>1 runs first.</summary>
    [DatabaseField]
    public virtual int Priority
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Priority)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Priority), value);
    }

    /// <summary>Inclusive UTC start of the validity window; null is unbounded.</summary>
    [DatabaseField]
    public virtual DateTime? ValidFromUtc
    {
        get => GetValue(nameof(ValidFromUtc)) as DateTime?;
        set => SetValue(nameof(ValidFromUtc), value);
    }

    /// <summary>Exclusive UTC end of the validity window; null is unbounded.</summary>
    [DatabaseField]
    public virtual DateTime? ValidUntilUtc
    {
        get => GetValue(nameof(ValidUntilUtc)) as DateTime?;
        set => SetValue(nameof(ValidUntilUtc), value);
    }

    /// <summary>Conditions JSON (array).</summary>
    [DatabaseField]
    public virtual string Conditions
    {
        get => ValidationHelper.GetString(GetValue(nameof(Conditions)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Conditions), value);
    }

    /// <summary>Actions JSON (object).</summary>
    [DatabaseField]
    public virtual string Actions
    {
        get => ValidationHelper.GetString(GetValue(nameof(Actions)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Actions), value);
    }

    /// <summary>Last change, maintained by Kentico.</summary>
    [DatabaseField]
    public virtual DateTime LastModified
    {
        get => ValidationHelper.GetDateTime(GetValue(nameof(LastModified)), DateTimeHelper.ZERO_TIME, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LastModified), value);
    }

    /// <summary>Parses this row; invalid stored JSON yields errors, never an exception.</summary>
    internal RuleParseResult ToDefinition() =>
        RuleSerializer.Parse(new RuleMetadata(RuleID, IndexName, Priority, Enabled, ValidFromUtc, ValidUntilUtc), Conditions, Actions);
}
