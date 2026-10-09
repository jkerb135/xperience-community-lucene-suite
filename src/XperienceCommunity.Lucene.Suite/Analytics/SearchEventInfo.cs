using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Analytics;

[assembly: RegisterObjectType(typeof(SearchEventInfo), SearchEventInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Who decided the search boundary of a <see cref="SearchEventInfo"/>.</summary>
public enum SearchEventOrigin
{
    /// <summary>The browser sent the search id and sequence.</summary>
    Client = 1,

    /// <summary>The server minted the search id (server-rendered or no-JS request).</summary>
    Server = 2,
}

/// <summary>One raw, identity-free search event: a single answered search request.</summary>
public class SearchEventInfo : AbstractInfo<SearchEventInfo, IInfoProvider<SearchEventInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.searchevent";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.SearchEvent";

    /// <summary>Maximum length of <see cref="IndexName"/>.</summary>
    public const int IndexNameMaxLength = 200;

    /// <summary>Maximum length of <see cref="QueryText"/>.</summary>
    public const int QueryTextMaxLength = 256;

    /// <summary>Type information.</summary>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<SearchEventInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(SearchEventID),
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
    public SearchEventInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public SearchEventInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int SearchEventID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(SearchEventID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SearchEventID), value);
    }

    /// <summary>Page-memory id of the logical search this request belongs to.</summary>
    [DatabaseField]
    public virtual Guid SearchId
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(SearchId)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SearchId), value);
    }

    /// <summary>1-based position of this request within <see cref="SearchId"/>.</summary>
    [DatabaseField]
    public virtual int Sequence
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Sequence)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Sequence), value);
    }

    /// <summary>Suite session cookie value, or null below the Visitor cookie level.</summary>
    [DatabaseField]
    public virtual Guid? SessionId
    {
        get => GetValue(nameof(SessionId)) as Guid?;
        set => SetValue(nameof(SessionId), value);
    }

    /// <summary>Server time (UTC) the request was answered.</summary>
    [DatabaseField]
    public virtual DateTime ReceivedUtc
    {
        get => ValidationHelper.GetDateTime(GetValue(nameof(ReceivedUtc)), DateTimeHelper.ZERO_TIME, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ReceivedUtc), value);
    }

    /// <summary>Searched Lucene index.</summary>
    [DatabaseField]
    public virtual string IndexName
    {
        get => ValidationHelper.GetString(GetValue(nameof(IndexName)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(IndexName), value);
    }

    /// <summary>Normalized, PII-scrubbed query text.</summary>
    [DatabaseField]
    public virtual string QueryText
    {
        get => ValidationHelper.GetString(GetValue(nameof(QueryText)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(QueryText), value);
    }

    /// <summary>1-based page number.</summary>
    [DatabaseField]
    public virtual int Page
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Page)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Page), value);
    }

    /// <summary>Hits per page.</summary>
    [DatabaseField]
    public virtual int PageSize
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(PageSize)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(PageSize), value);
    }

    /// <summary>Number of applied facet values.</summary>
    [DatabaseField]
    public virtual int FacetFilterCount
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(FacetFilterCount)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(FacetFilterCount), value);
    }

    /// <summary>Number of matching documents.</summary>
    [DatabaseField]
    public virtual int ResultCount
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(ResultCount)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ResultCount), value);
    }

    /// <summary>Search time in whole milliseconds.</summary>
    [DatabaseField]
    public virtual int LatencyMs
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyMs)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyMs), value);
    }

    /// <summary>Whether the visitor explicitly submitted (Enter / button), or the server minted the id.</summary>
    [DatabaseField]
    public virtual bool IsSubmit
    {
        get => ValidationHelper.GetBoolean(GetValue(nameof(IsSubmit)), false, CultureInfo.InvariantCulture);
        set => SetValue(nameof(IsSubmit), value);
    }

    /// <summary>Who decided the search boundary.</summary>
    [DatabaseField]
    public virtual SearchEventOrigin Origin
    {
        get => (SearchEventOrigin)ValidationHelper.GetInteger(GetValue(nameof(Origin)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Origin), (int)value);
    }
}
