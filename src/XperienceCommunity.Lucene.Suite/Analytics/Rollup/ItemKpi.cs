namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Rollup output for one (UTC day, index, item); maps onto <see cref="ItemDailyInfo"/>.</summary>
internal sealed record ItemKpi(DateOnly Day, string IndexName, Guid ItemGuid, int ClickedSearches, long ClickPositionSum, int Conversions);

/// <summary>One rolled day: query KPIs (SearchDaily) and item KPIs (ItemDaily) from the same attribution pass.</summary>
internal sealed record DailyRollup(IReadOnlyList<DailyKpi> Kpis, IReadOnlyList<ItemKpi> Items);
