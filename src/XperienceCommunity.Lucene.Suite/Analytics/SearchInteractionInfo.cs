using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Analytics;

[assembly: RegisterObjectType(typeof(SearchInteractionInfo), SearchInteractionInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Kind of a <see cref="SearchInteractionInfo"/>.</summary>
public enum SearchInteractionType
{
    /// <summary>The visitor opened a result.</summary>
    Click = 1,

    /// <summary>The visitor completed a goal attributed to a result.</summary>
    Conversion = 2,
}

/// <summary>One raw, identity-free click or conversion on a search result.</summary>
public class SearchInteractionInfo : AbstractInfo<SearchInteractionInfo, IInfoProvider<SearchInteractionInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.searchinteraction";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.SearchInteraction";

    /// <summary>Type information.</summary>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<SearchInteractionInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(SearchInteractionID),
        null,
        null,
        null,
        null,
        null,
        null,
        null)
    {
        ContinuousIntegrationSettings = { Enabled = false },
        LogEvents = false,
        TouchCacheDependencies = false,
        SupportsInvalidation = false,
        SupportsCloning = false,
    };

    /// <summary>Creates an empty instance.</summary>
    public SearchInteractionInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public SearchInteractionInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int SearchInteractionID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(SearchInteractionID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SearchInteractionID), value);
    }

    /// <summary>Logical search the interacted result list came from.</summary>
    [DatabaseField]
    public virtual Guid SearchId
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(SearchId)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SearchId), value);
    }

    /// <summary>Sequence of the search request whose response listed the result.</summary>
    [DatabaseField]
    public virtual int Sequence
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Sequence)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Sequence), value);
    }

    /// <summary>Indexed item the visitor interacted with.</summary>
    [DatabaseField]
    public virtual Guid ItemGuid
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(ItemGuid)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ItemGuid), value);
    }

    /// <summary>1-based absolute result position; null for a conversion without a known position.</summary>
    [DatabaseField]
    public virtual int? Position
    {
        get => GetValue(nameof(Position)) as int?;
        set => SetValue(nameof(Position), value);
    }

    /// <summary>Click or conversion.</summary>
    [DatabaseField]
    public virtual SearchInteractionType Type
    {
        get => (SearchInteractionType)ValidationHelper.GetInteger(GetValue(nameof(Type)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Type), (int)value);
    }

    /// <summary>Caller-supplied (or minted) id used to dedupe retried events at rollup.</summary>
    [DatabaseField]
    public virtual Guid EventId
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(EventId)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(EventId), value);
    }

    /// <summary>Suite session cookie value, or null below the Visitor cookie level.</summary>
    [DatabaseField]
    public virtual Guid? SessionId
    {
        get => GetValue(nameof(SessionId)) as Guid?;
        set => SetValue(nameof(SessionId), value);
    }

    /// <summary>Server time (UTC) the interaction was logged.</summary>
    [DatabaseField]
    public virtual DateTime ReceivedUtc
    {
        get => ValidationHelper.GetDateTime(GetValue(nameof(ReceivedUtc)), DateTimeHelper.ZERO_TIME, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ReceivedUtc), value);
    }
}
