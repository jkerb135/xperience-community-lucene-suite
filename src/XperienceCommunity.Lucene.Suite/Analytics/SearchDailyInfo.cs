using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Analytics;

[assembly: RegisterObjectType(typeof(SearchDailyInfo), SearchDailyInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Daily KPIs of one query on one index: one row per (UTC day, index, counted query text).</summary>
public class SearchDailyInfo : AbstractInfo<SearchDailyInfo, IInfoProvider<SearchDailyInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.searchdaily";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.SearchDaily";

    /// <summary>Number of latency histogram buckets (<c>LatencyBucket0</c> … <c>LatencyBucket9</c>).</summary>
    public const int LatencyBucketCount = 10;

    /// <summary>Type information.</summary>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<SearchDailyInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(SearchDailyID),
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
    public SearchDailyInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public SearchDailyInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int SearchDailyID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(SearchDailyID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SearchDailyID), value);
    }

    /// <summary>UTC day the logical searches started on (time part is midnight).</summary>
    [DatabaseField]
    public virtual DateTime Day
    {
        get => ValidationHelper.GetDateTime(GetValue(nameof(Day)), DateTimeHelper.ZERO_TIME, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Day), value);
    }

    /// <summary>Searched Lucene index.</summary>
    [DatabaseField]
    public virtual string IndexName
    {
        get => ValidationHelper.GetString(GetValue(nameof(IndexName)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(IndexName), value);
    }

    /// <summary>Counted query text; '' is the browse (match-all) bucket.</summary>
    [DatabaseField]
    public virtual string QueryText
    {
        get => ValidationHelper.GetString(GetValue(nameof(QueryText)), string.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(QueryText), value);
    }

    /// <summary>Logical searches.</summary>
    [DatabaseField]
    public virtual int Searches
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Searches)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Searches), value);
    }

    /// <summary>Logical searches where every request with the counted text returned no results.</summary>
    [DatabaseField]
    public virtual int ZeroResultSearches
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(ZeroResultSearches)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ZeroResultSearches), value);
    }

    /// <summary>Logical searches with at least one explicit submit.</summary>
    [DatabaseField]
    public virtual int SubmittedSearches
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(SubmittedSearches)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SubmittedSearches), value);
    }

    /// <summary>Logical searches with at least one counted click.</summary>
    [DatabaseField]
    public virtual int ClickedSearches
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(ClickedSearches)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ClickedSearches), value);
    }

    /// <summary>Counted clicks (one per logical search and item).</summary>
    [DatabaseField]
    public virtual int Clicks
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Clicks)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Clicks), value);
    }

    /// <summary>Sum of the 1-based positions of counted clicks; divide by Clicks for the average position.</summary>
    [DatabaseField]
    public virtual long ClickPositionSum
    {
        get => ValidationHelper.GetLong(GetValue(nameof(ClickPositionSum)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ClickPositionSum), value);
    }

    /// <summary>Logical searches with at least one counted conversion.</summary>
    [DatabaseField]
    public virtual int ConvertedSearches
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(ConvertedSearches)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ConvertedSearches), value);
    }

    /// <summary>Counted conversions.</summary>
    [DatabaseField]
    public virtual int Conversions
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Conversions)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Conversions), value);
    }

    /// <summary>Distinct non-null session ids among the logical searches.</summary>
    [DatabaseField]
    public virtual int SessionCount
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(SessionCount)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(SessionCount), value);
    }

    /// <summary>Logical searches whose latency was 0–10 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket0
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket0)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket0), value);
    }

    /// <summary>Logical searches whose latency was 11–25 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket1
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket1)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket1), value);
    }

    /// <summary>Logical searches whose latency was 26–50 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket2
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket2)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket2), value);
    }

    /// <summary>Logical searches whose latency was 51–100 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket3
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket3)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket3), value);
    }

    /// <summary>Logical searches whose latency was 101–250 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket4
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket4)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket4), value);
    }

    /// <summary>Logical searches whose latency was 251–500 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket5
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket5)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket5), value);
    }

    /// <summary>Logical searches whose latency was 501–1000 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket6
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket6)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket6), value);
    }

    /// <summary>Logical searches whose latency was 1001–2500 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket7
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket7)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket7), value);
    }

    /// <summary>Logical searches whose latency was 2501–5000 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket8
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket8)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket8), value);
    }

    /// <summary>Logical searches whose latency was over 5000 ms.</summary>
    [DatabaseField]
    public virtual int LatencyBucket9
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(LatencyBucket9)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(LatencyBucket9), value);
    }
}
