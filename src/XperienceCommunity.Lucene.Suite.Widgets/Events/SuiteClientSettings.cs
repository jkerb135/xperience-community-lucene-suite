using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Events;

/// <summary>The server's client-relevant analytics settings, named as in docs/analytics-test-vectors.json.</summary>
/// <param name="TextChangeTimeoutMs">Text-change window in milliseconds.</param>
/// <param name="TypoOneEditMinLength">Shortest query length that tolerates one edit.</param>
/// <param name="TypoTwoEditMinLength">Shortest query length that tolerates two edits.</param>
/// <param name="DeepBackspaceRatio">Deep-backspace ratio.</param>
/// <param name="AttributionWindowMs">Attribution window in milliseconds.</param>
/// <param name="EventsUrl">Where to POST clicks and conversions.</param>
internal sealed record SuiteClientSettings(
    long TextChangeTimeoutMs,
    int TypoOneEditMinLength,
    int TypoTwoEditMinLength,
    double DeepBackspaceRatio,
    long AttributionWindowMs,
    string EventsUrl)
{
    /// <summary>Reads the settings from validated suite options.</summary>
    public static SuiteClientSettings From(SuiteSearchOptions options, string eventsUrl)
    {
        var analytics = options.Analytics;
        return new(
            (long)analytics.Merge.TextChangeWindow.TotalMilliseconds,
            analytics.Merge.TypoOneEditMinLength,
            analytics.Merge.TypoTwoEditMinLength,
            analytics.Merge.DeepBackspaceRatio,
            (long)analytics.Attribution.AttributionWindow.TotalMilliseconds,
            eventsUrl);
    }
}
