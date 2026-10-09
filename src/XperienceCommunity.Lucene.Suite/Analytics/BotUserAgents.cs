namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Case-insensitive User-Agent substring check for crawlers, link previewers and headless browsers.</summary>
internal static class BotUserAgents
{
    private static readonly string[] Markers =
    [
        "bot", "crawl", "spider", "slurp", "headless", "preview", "facebookexternalhit",
        "mediapartners-google", "lighthouse", "pingdom", "phantomjs",
    ];

    /// <summary>True for a known bot marker or a missing User-Agent.</summary>
    public static bool IsBot(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent)
        || Markers.Any(marker => userAgent.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
