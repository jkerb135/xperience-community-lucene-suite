namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Rollup input mirroring one <see cref="SearchEventInfo"/> row.</summary>
internal sealed record RawSearchRow(
    int Id,
    Guid SearchId,
    int Sequence,
    Guid? SessionId,
    DateTime ReceivedUtc,
    string IndexName,
    string QueryText,
    int Page,
    int PageSize,
    int ResultCount,
    int LatencyMs,
    bool IsSubmit);
