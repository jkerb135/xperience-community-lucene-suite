using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Synonyms;

[assembly: RegisterObjectType(typeof(SynonymInfo), SynonymInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Synonyms;

/// <summary>A two-way synonym group or a one-way synonym of one Lucene index, stored in module class <c>LuceneSuite.Synonym</c>.</summary>
public class SynonymInfo : AbstractInfo<SynonymInfo, IInfoProvider<SynonymInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.synonym";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.Synonym";

    /// <summary>Maximum length of <see cref="Kind"/>.</summary>
    public const int KindMaxLength = 20;

    /// <summary>Type information.</summary>
    /// <remarks>CI/CD is off because synonyms are marketer data edited in production; a restore would overwrite them.</remarks>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<SynonymInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(SynonymID),
        nameof(LastModified),
        nameof(SynonymGuid),
        null,
        nameof(Input),
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
    public SynonymInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public SynonymInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int SynonymID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(SynonymID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SynonymID), value);
    }

    /// <summary>Stable identifier.</summary>
    [DatabaseField]
    public virtual Guid SynonymGuid
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(SynonymGuid)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SynonymGuid), value);
    }

    /// <summary>Targeted Kentico Lucene index.</summary>
    [DatabaseField]
    public virtual string IndexName
    {
        get => ValidationHelper.GetString(GetValue(nameof(IndexName)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(IndexName), value);
    }

    /// <summary><c>twoWay</c> or <c>oneWay</c>.</summary>
    [DatabaseField]
    public virtual string Kind
    {
        get => ValidationHelper.GetString(GetValue(nameof(Kind)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Kind), value);
    }

    /// <summary>The term a one-way synonym expands; empty for a two-way group.</summary>
    [DatabaseField]
    public virtual string Input
    {
        get => ValidationHelper.GetString(GetValue(nameof(Input)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Input), value);
    }

    /// <summary>Terms JSON (array of strings): the group of a two-way synonym, or what a one-way input expands to.</summary>
    [DatabaseField]
    public virtual string Terms
    {
        get => ValidationHelper.GetString(GetValue(nameof(Terms)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Terms), value);
    }

    /// <summary>Whether searches use the synonym.</summary>
    [DatabaseField]
    public virtual bool Enabled
    {
        get => ValidationHelper.GetBoolean(GetValue(nameof(Enabled)), false, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Enabled), value);
    }

    /// <summary>Last change, maintained by Kentico.</summary>
    [DatabaseField]
    public virtual DateTime LastModified
    {
        get => ValidationHelper.GetDateTime(GetValue(nameof(LastModified)), DateTimeHelper.ZERO_TIME, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LastModified), value);
    }

    /// <summary>Parses this row; invalid stored values yield errors, never an exception.</summary>
    public SynonymParseResult ToDefinition() => SynonymSerializer.Parse(SynonymID, IndexName, Kind, Input, Terms);
}
