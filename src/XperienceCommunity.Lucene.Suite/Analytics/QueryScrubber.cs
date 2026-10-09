using System.Text.RegularExpressions;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Normalizes query text for analytics and replaces emails and long digit runs with placeholders.</summary>
internal static partial class QueryScrubber
{
    internal const string EmailPlaceholder = "{email}";
    internal const string NumberPlaceholder = "{number}";

    /// <summary>Trims, collapses whitespace, lower-cases invariantly, scrubs PII and caps at <see cref="SearchEventInfo.QueryTextMaxLength"/>.</summary>
    public static string Normalize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        string text = Whitespace().Replace(query.Trim(), " ").ToLowerInvariant();
        text = Email().Replace(text, EmailPlaceholder);
        text = DigitRun().Replace(text, NumberPlaceholder);
        return text.Length > SearchEventInfo.QueryTextMaxLength ? text[..SearchEventInfo.QueryTextMaxLength] : text;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Email();

    // 7+ digits after "+", 10+ digits with any separators, or 7+ digits whose separators include punctuation,
    // so space-separated small numbers ("top 100 2024") stay intact.
    [GeneratedRegex(@"\+\d(?:[\s\-.()/]{0,2}\d){6,}|\d(?:[\s\-.()/]{0,2}\d){9,}|\(?\d(?:(?:\s?[\-.()/]{1,2}\s?)?\d){6,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DigitRun();
}
