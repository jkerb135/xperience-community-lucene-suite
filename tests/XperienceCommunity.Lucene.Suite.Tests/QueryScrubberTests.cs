using XperienceCommunity.Lucene.Suite.Analytics;

namespace XperienceCommunity.Lucene.Suite.Tests;

public sealed class QueryScrubberTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("  Coffee   GRINDER \t beans ", "coffee grinder beans")]
    [InlineData("ÉCLAIR", "éclair")]
    [InlineData("contact John.Doe+x@Example.co.uk now", "contact {email} now")]
    [InlineData("order 1234567", "order {number}")]
    [InlineData("call 555-123-4567", "call {number}")]
    [InlineData("call (555) 123-4567", "call {number}")]
    [InlineData("+44 20 7946 0958", "{number}")]
    [InlineData("card 4111 1111-1111 1111", "card {number}")]
    [InlineData("4111 1111 1111 1111", "{number}")]
    [InlineData("555 123 4567", "{number}")]
    [InlineData("iban de89.3704.0044.0532.0130.00", "iban de{number}")]
    // False-positive guards: numbers of six digits or fewer stay.
    [InlineData("iPhone 15", "iphone 15")]
    [InlineData("2025 Calendar", "2025 calendar")]
    [InlineData("top 100 2024 ideas", "top 100 2024 ideas")]
    [InlineData("zip 123456", "zip 123456")]
    [InlineData("a@b", "a@b")]
    public void Normalize(string? input, string expected) =>
        Assert.Equal(expected, QueryScrubber.Normalize(input));

    [Fact]
    public void Normalize_CapsAt256()
    {
        string result = QueryScrubber.Normalize(new string('a', 300));

        Assert.Equal(SearchEventInfo.QueryTextMaxLength, result.Length);
    }
}
