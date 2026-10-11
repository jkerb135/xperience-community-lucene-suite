namespace XperienceCommunity.Lucene.Suite.Search;

/// <summary>Places pins at their global 1-based positions in a ranking and slices out one page.</summary>
internal static class PinMerger
{
    /// <summary>
    /// Inserts <paramref name="pins"/> into <paramref name="organic"/> (which must not contain pinned items) in ascending position;
    /// a pin past the end is appended. Returns page <paramref name="page"/> of size <paramref name="pageSize"/>.
    /// </summary>
    public static IReadOnlyList<T> Merge<T>(IReadOnlyList<T> organic, IReadOnlyList<(int Position, T Item)> pins, int page, int pageSize)
    {
        var merged = new List<T>(organic.Count + pins.Count);
        merged.AddRange(organic);
        foreach (var (position, item) in pins.OrderBy(pin => pin.Position))
        {
            merged.Insert(Math.Min(position - 1, merged.Count), item);
        }

        return merged.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
    }
}
