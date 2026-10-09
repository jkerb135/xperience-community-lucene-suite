using System.Text.Json;
using System.Text.RegularExpressions;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Checks docs/analytics-test-vectors.json against <see cref="SearchIdMinter"/>, the reference client rule, and the rollup engine.</summary>
public sealed class TestVectorTests
{
    private static readonly JsonDocument Vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "analytics-test-vectors.json")));

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var testCase in Vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            names.Add(testCase.GetProperty("name").GetString()!);
        }

        return names;
    }

    [Fact]
    public void FileHasAtLeastTwelveCases_AndTwoWithNonDefaultSettings()
    {
        var cases = Vectors.RootElement.GetProperty("cases").EnumerateArray().ToList();

        Assert.True(cases.Count >= 12);
        Assert.True(cases.Count(c => c.TryGetProperty("settings", out _)) >= 2);
    }

    [Fact]
    public void FileDefaults_MatchTheServerDefaults()
    {
        var defaults = new VectorSettings(Vectors.RootElement.GetProperty("settings"));
        var options = new SuiteAnalyticsOptions();

        Assert.Equal(
            new VectorSettings(
                (long)options.Merge.TextChangeWindow.TotalMilliseconds,
                options.Merge.TypoOneEditMinLength,
                options.Merge.TypoTwoEditMinLength,
                options.Merge.DeepBackspaceRatio,
                (long)options.Attribution.AttributionWindow.TotalMilliseconds),
            defaults);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Vector_MatchesReferenceMinter(string name)
    {
        var (testCase, settings) = Case(name);
        var minter = new SearchIdMinter(settings);
        long window = settings.AttributionWindowMs;
        var sentAt = new Dictionary<int, long>();
        int ordinal = 0;
        int step = 0;

        foreach (var s in testCase.GetProperty("steps").EnumerateArray())
        {
            step++;
            long t = s.GetProperty("t").GetInt64();
            var expect = s.GetProperty("expect");
            if (s.TryGetProperty("click", out var click))
            {
                int seq = click.TryGetProperty("seq", out var seqValue) ? seqValue.GetInt32() : minter.Sequence;
                Assert.True(expect.GetProperty("attributed").GetBoolean() == t - sentAt[seq] <= window, $"step {step}: attribution");
                continue;
            }

            bool submit = s.TryGetProperty("submit", out var submitValue) && submitValue.GetBoolean();
            if (minter.Next(s.GetProperty("q").GetString()!, t, submit))
            {
                ordinal++;
                sentAt.Clear();
            }

            sentAt[minter.Sequence] = t;
            Assert.Equal((expect.GetProperty("search").GetInt32(), expect.GetProperty("seq").GetInt32()), (ordinal, minter.Sequence));
        }
    }

    /// <summary>Feeds each vector's expected rows to the rollup engine: it must find the same logical searches and attributions.</summary>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Vector_RollupEngineAgreesOnBoundariesAndAttribution(string name)
    {
        var (testCase, settings) = Case(name);
        var start = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
        var searchIds = new Dictionary<int, Guid>();
        var rows = new List<RawSearchRow>();
        var clicks = new List<RawInteractionRow>();
        int attributed = 0;

        foreach (var s in testCase.GetProperty("steps").EnumerateArray())
        {
            var at = start.AddMilliseconds(s.GetProperty("t").GetInt64());
            var expect = s.GetProperty("expect");
            if (s.TryGetProperty("click", out var click))
            {
                var listed = click.TryGetProperty("seq", out var seqValue) ? rows.Single(r => r.SearchId == rows[^1].SearchId && r.Sequence == seqValue.GetInt32()) : rows[^1];
                int position = ((listed.Page - 1) * listed.PageSize) + 1;
                clicks.Add(new RawInteractionRow(clicks.Count + 1, listed.SearchId, listed.Sequence, Guid.NewGuid(), position, SearchInteractionType.Click, Guid.NewGuid(), at));
                attributed += expect.GetProperty("attributed").GetBoolean() ? 1 : 0;
                continue;
            }

            int ordinal = expect.GetProperty("search").GetInt32();
            int page = s.TryGetProperty("kind", out var kind) && kind.GetString() == "page" ? 2 : 1;
            string query = SearchIdMinter.Norm(s.GetProperty("q").GetString()!);
            searchIds.TryAdd(ordinal, Guid.NewGuid());
            rows.Add(new RawSearchRow(rows.Count + 1, searchIds[ordinal], expect.GetProperty("seq").GetInt32(), null, at, "vectors", query, page, 10, 1, 1, false));
        }

        var engineSettings = new RollupSettings(TimeSpan.FromMilliseconds(settings.TextChangeTimeoutMs), TimeSpan.FromMilliseconds(settings.AttributionWindowMs));
        var expected = rows.GroupBy(r => r.SearchId).Select(g => string.Join(",", g.Select(r => r.Id))).Order().ToList();
        var actual = RollupEngine.Segment(rows, engineSettings).Select(l => string.Join(",", l.Rows.Select(r => r.Id))).Order().ToList();
        Assert.Equal(expected, actual);

        var kpis = RollupEngine.Roll(DateOnly.FromDateTime(start), rows, clicks, engineSettings);
        Assert.Equal((rows.Select(r => r.SearchId).Distinct().Count(), attributed), (kpis.Sum(k => k.Searches), kpis.Sum(k => k.Clicks)));
    }

    [Theory]
    [InlineData("gridner", "grinder", 1)]
    [InlineData("ipad", "imac", 2)]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    public void OptimalStringAlignment_Distance(string a, string b, int expected) =>
        Assert.Equal(expected, SearchIdMinter.Osa(a, b));

    private static (JsonElement Case, VectorSettings Settings) Case(string name)
    {
        var root = Vectors.RootElement;
        var testCase = root.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
        var settings = new VectorSettings(root.GetProperty("settings"));
        return (testCase, testCase.TryGetProperty("settings", out var overrides) ? settings.With(overrides) : settings);
    }
}

/// <summary>Client-relevant settings of a vector case (top-level defaults with the case's overrides).</summary>
internal sealed record VectorSettings(long TextChangeTimeoutMs, int TypoOneEditMinLength, int TypoTwoEditMinLength, double DeepBackspaceRatio, long AttributionWindowMs)
{
    public VectorSettings(JsonElement json)
        : this(
            json.GetProperty("textChangeTimeoutMs").GetInt64(),
            json.GetProperty("typoOneEditMinLength").GetInt32(),
            json.GetProperty("typoTwoEditMinLength").GetInt32(),
            json.GetProperty("deepBackspaceRatio").GetDouble(),
            json.GetProperty("attributionWindowMs").GetInt64())
    {
    }

    public VectorSettings With(JsonElement overrides) => new(
        overrides.TryGetProperty("textChangeTimeoutMs", out var a) ? a.GetInt64() : TextChangeTimeoutMs,
        overrides.TryGetProperty("typoOneEditMinLength", out var b) ? b.GetInt32() : TypoOneEditMinLength,
        overrides.TryGetProperty("typoTwoEditMinLength", out var c) ? c.GetInt32() : TypoTwoEditMinLength,
        overrides.TryGetProperty("deepBackspaceRatio", out var d) ? d.GetDouble() : DeepBackspaceRatio,
        overrides.TryGetProperty("attributionWindowMs", out var e) ? e.GetInt64() : AttributionWindowMs);
}

/// <summary>Reference implementation of the client minting rules in docs/analytics-client-contract.md.</summary>
internal sealed partial class SearchIdMinter(VectorSettings settings)
{
    private bool started;
    private string lastQ = string.Empty;
    private string anchor = string.Empty;
    private int minLen;
    private long lastSentAt;
    private bool submitted;

    public int Sequence { get; private set; }

    /// <summary>Applies one outgoing search; true when it must carry a newly minted searchId.</summary>
    public bool Next(string rawQuery, long now, bool isSubmit)
    {
        string q = Norm(rawQuery);
        bool changed = q != lastQ;
        bool mint = !started
            || (changed && now - lastSentAt > settings.TextChangeTimeoutMs)
            || (submitted && (changed || isSubmit))
            || (q.Length == 0) != (lastQ.Length == 0)
            || (changed && !IsContinuation(lastQ, q))
            || (changed && (q.StartsWith(lastQ, StringComparison.Ordinal) || IsTypo(lastQ, q))
                && minLen < settings.DeepBackspaceRatio * anchor.Length
                && !anchor.StartsWith(q, StringComparison.Ordinal));

        if (mint)
        {
            started = true;
            Sequence = 0;
            anchor = q;
            minLen = q.Length;
            submitted = false;
        }

        if (q.Length < lastQ.Length && lastQ.StartsWith(q, StringComparison.Ordinal))
        {
            minLen = Math.Min(minLen, q.Length);
        }

        if (!anchor.StartsWith(q, StringComparison.Ordinal))
        {
            anchor = q;
            minLen = q.Length;
        }

        Sequence++;
        lastQ = q;
        lastSentAt = now;
        submitted |= isSubmit;
        return mint;
    }

    internal static string Norm(string rawQuery) => Whitespace().Replace(rawQuery.Trim(), " ").ToLowerInvariant();

    internal static int Osa(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }

    private bool IsContinuation(string a, string b) =>
        a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal) || IsTypo(a, b);

    private bool IsTypo(string a, string b)
    {
        int shorter = Math.Min(a.Length, b.Length);
        int allowed = shorter >= settings.TypoTwoEditMinLength ? 2 : shorter >= settings.TypoOneEditMinLength ? 1 : 0;
        return allowed > 0 && Osa(a, b) <= allowed;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
