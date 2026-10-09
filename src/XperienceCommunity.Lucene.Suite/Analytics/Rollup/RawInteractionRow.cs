namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Rollup input mirroring one <see cref="SearchInteractionInfo"/> row.</summary>
internal sealed record RawInteractionRow(
    int Id,
    Guid SearchId,
    int Sequence,
    Guid ItemGuid,
    int? Position,
    SearchInteractionType Type,
    Guid EventId,
    DateTime ReceivedUtc);
