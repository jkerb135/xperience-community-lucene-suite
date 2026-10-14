using System.Data;
using System.Globalization;

using CMS;
using CMS.DataEngine;
using CMS.Helpers;

using XperienceCommunity.Lucene.Suite.Analytics;

[assembly: RegisterObjectType(typeof(ItemDailyInfo), ItemDailyInfo.OBJECT_TYPE)]

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Daily popularity of one indexed item on one index: one row per (UTC day, index, item), counted by the same rollup pass as <see cref="SearchDailyInfo"/>.</summary>
public class ItemDailyInfo : AbstractInfo<ItemDailyInfo, IInfoProvider<ItemDailyInfo>>
{
    /// <summary>Object type identifier.</summary>
    public const string OBJECT_TYPE = "lucenesuite.itemdaily";

    /// <summary>Module class code name; the table name is this with the dot replaced by an underscore.</summary>
    public const string CLASS_NAME = "LuceneSuite.ItemDaily";

    /// <summary>Type information.</summary>
    public static readonly ObjectTypeInfo TYPEINFO = new(
        typeof(IInfoProvider<ItemDailyInfo>),
        OBJECT_TYPE,
        CLASS_NAME,
        nameof(ItemDailyID),
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
    public ItemDailyInfo()
        : base(TYPEINFO)
    {
    }

    /// <summary>Creates an instance from a data row.</summary>
    public ItemDailyInfo(DataRow dr)
        : base(TYPEINFO, dr)
    {
    }

    /// <summary>Primary key.</summary>
    [DatabaseField]
    public virtual int ItemDailyID
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(ItemDailyID)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ItemDailyID), value);
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

    /// <summary>ItemGuid of the indexed item, as clicked or converted on.</summary>
    [DatabaseField]
    public virtual Guid ItemGuid
    {
        get => ValidationHelper.GetGuid(GetValue(nameof(ItemGuid)), Guid.Empty, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ItemGuid), value);
    }

    /// <summary>Logical searches with a counted click on the item; their sum per index and day equals the sum of <see cref="SearchDailyInfo.Clicks"/>.</summary>
    [DatabaseField]
    public virtual int ClickedSearches
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(ClickedSearches)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ClickedSearches), value);
    }

    /// <summary>Sum of the 1-based positions of those clicks; divide by ClickedSearches for the average position.</summary>
    [DatabaseField]
    public virtual long ClickPositionSum
    {
        get => ValidationHelper.GetLong(GetValue(nameof(ClickPositionSum)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(ClickPositionSum), value);
    }

    /// <summary>Counted conversions on the item, clicked or not; their sum per index and day equals the sum of <see cref="SearchDailyInfo.Conversions"/>.</summary>
    [DatabaseField]
    public virtual int Conversions
    {
        get => ValidationHelper.GetInteger(GetValue(nameof(Conversions)), 0, CultureInfo.InvariantCulture);
        set => SetValue(nameof(Conversions), value);
    }
}
