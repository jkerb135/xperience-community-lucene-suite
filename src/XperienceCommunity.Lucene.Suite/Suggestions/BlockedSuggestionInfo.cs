using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Suggestions;

[assembly: RegisterObjectType(typeof(BlockedSuggestionInfo), BlockedSuggestionInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Suggestions;

/// <summary>A query text kept out of the suggestions of one Lucene index, stored in module class <c>LuceneSuite.BlockedSuggestion</c>. Reports are unaffected.</summary>
public class BlockedSuggestionInfo : AbstractInfo<BlockedSuggestionInfo, IInfoProvider<BlockedSuggestionInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.blockedsuggestion";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.BlockedSuggestion";

    /// <summary>Maximum length of <see cref="Text"/> and <see cref="MatchKey"/>.</summary>
    public const int TextMaxLength = 100;

    /// <summary>Maximum length of <see cref="MatchMode"/>.</summary>
    public const int MatchModeMaxLength = 10;

    /// <summary><see cref="MatchMode"/> value: removes a suggestion whose match key equals the entry's.</summary>
    public const string MatchModeExact = "exact";

    /// <summary><see cref="MatchMode"/> value: removes every suggestion that contains the entry as whole words.</summary>
    public const string MatchModeWord = "word";

    /// <summary>Type information.</summary>
    /// <remarks>CI/CD is off because the blocklist is marketer data edited in production; a restore would overwrite it.</remarks>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<BlockedSuggestionInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(BlockedSuggestionID),
        nameof(LastModified),
        nameof(BlockedSuggestionGuid),
        null,
        nameof(Text),
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
    public BlockedSuggestionInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public BlockedSuggestionInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int BlockedSuggestionID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(BlockedSuggestionID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(BlockedSuggestionID), value);
    }

    /// <summary>Stable identifier.</summary>
    [DatabaseField]
    public virtual Guid BlockedSuggestionGuid
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(BlockedSuggestionGuid)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(BlockedSuggestionGuid), value);
    }

    /// <summary>Targeted Kentico Lucene index.</summary>
    [DatabaseField]
    public virtual string IndexName
    {
        get => ValidationHelper.GetString(GetValue(nameof(IndexName)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(IndexName), value);
    }

    /// <summary>The blocked text as entered (at most <see cref="TextMaxLength"/> characters).</summary>
    [DatabaseField]
    public virtual string Text
    {
        get => ValidationHelper.GetString(GetValue(nameof(Text)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Text), value);
    }

    /// <summary><see cref="Text"/> lower-cased, with whitespace collapsed and punctuation trimmed from each word; for listing and duplicate checks (suggestions recompute it from <see cref="Text"/>).</summary>
    [DatabaseField]
    public virtual string MatchKey
    {
        get => ValidationHelper.GetString(GetValue(nameof(MatchKey)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(MatchKey), value);
    }

    /// <summary><see cref="MatchModeExact"/> or <see cref="MatchModeWord"/>; any other value is treated as exact.</summary>
    [DatabaseField]
    public virtual string MatchMode
    {
        get => ValidationHelper.GetString(GetValue(nameof(MatchMode)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(MatchMode), value);
    }

    /// <summary>Last change, maintained by Kentico.</summary>
    [DatabaseField]
    public virtual DateTime LastModified
    {
        get => ValidationHelper.GetDateTime(GetValue(nameof(LastModified)), DateTimeHelper.ZERO_TIME, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LastModified), value);
    }
}
