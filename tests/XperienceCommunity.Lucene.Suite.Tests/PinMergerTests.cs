using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class PinMergerTests
{
    private static readonly string[] Organic = Enumerable.Range(1, 15).Select(i => $"o{i}").ToArray();

    [Theory]
    // Pins at 1 and 3 among organic hits.
    [InlineData("1:A,3:B", 5, 1, 10, "A,o1,B,o2,o3,o4,o5")]
    // Input order doesn't matter. Equal positions (the engine never produces them): the later pin lands first.
    [InlineData("3:B,1:A", 5, 1, 10, "A,o1,B,o2,o3,o4,o5")]
    [InlineData("2:A,2:B", 3, 1, 10, "o1,B,A,o2,o3")]
    // Beyond the end: appended, in position order.
    [InlineData("9:B,7:A", 3, 1, 10, "o1,o2,o3,A,B")]
    // Global positions: 12 is hit 2 of page 2; page 1 is all organic.
    [InlineData("12:A", 15, 1, 10, "o1,o2,o3,o4,o5,o6,o7,o8,o9,o10")]
    [InlineData("12:A", 15, 2, 10, "o11,A,o12,o13,o14,o15")]
    // No organic hits: pins only, in order.
    [InlineData("5:B,2:A", 0, 1, 10, "A,B")]
    // No pins: the organic slice.
    [InlineData("", 15, 2, 10, "o11,o12,o13,o14,o15")]
    // A page past the end is empty.
    [InlineData("1:A", 3, 3, 2, "")]
    public void Merge_PlacesPinsGlobally_AndSlicesThePage(string pins, int organicCount, int page, int pageSize, string expected)
    {
        var parsed = pins.Length == 0
            ? []
            : pins.Split(',').Select(pin => pin.Split(':')).Select(parts => (int.Parse(parts[0]), parts[1])).ToArray();

        var result = PinMerger.Merge(Organic[..organicCount], parsed, page, pageSize);

        Assert.Equal(expected, string.Join(",", result));
    }
}
