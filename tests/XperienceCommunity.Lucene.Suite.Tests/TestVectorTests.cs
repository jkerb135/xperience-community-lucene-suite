using System.Text.Json;
using System.Text.RegularExpressions;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Checks docs/analytics-test-vectors.json against <see cref="SearchIdMinter"/>, the reference client rule.</summary>
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
    public void FileHasAtLeastTwelveCases() => Assert.True(Vectors.RootElement.GetProperty("cases").GetArrayLength() >= 12);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Vector_MatchesReferenceMinter(string name)
    {
        var root = Vectors.RootElement;
        var testCase = root.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
        var minter = new SearchIdMinter(root.GetProperty("textChangeTimeoutMs").GetInt64());
        long window = root.GetProperty("attributionWindowMs").GetInt64();
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

    [Theory]
    [InlineData("gridner", "grinder", 1)]
    [InlineData("ipad", "imac", 2)]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    public void OptimalStringAlignment_Distance(string a, string b, int expected) =>
        Assert.Equal(expected, SearchIdMinter.Osa(a, b));
}

/// <summary>Reference implementation of the client minting rules in docs/analytics-client-contract.md.</summary>
internal sealed partial class SearchIdMinter(long timeoutMs)
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
        string q = Whitespace().Replace(rawQuery.Trim(), " ").ToLowerInvariant();
        bool changed = q != lastQ;
        bool mint = !started
            || (changed && now - lastSentAt > timeoutMs)
            || (submitted && (changed || isSubmit))
            || (q.Length == 0) != (lastQ.Length == 0)
            || (changed && !IsContinuation(lastQ, q))
            || (changed && (q.StartsWith(lastQ, StringComparison.Ordinal) || IsTypo(lastQ, q))
                && 2 * minLen < anchor.Length
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

    private static bool IsContinuation(string a, string b) =>
        a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal) || IsTypo(a, b);

    private static bool IsTypo(string a, string b)
    {
        int shorter = Math.Min(a.Length, b.Length);
        int allowed = shorter >= 8 ? 2 : shorter >= 4 ? 1 : 0;
        return allowed > 0 && Osa(a, b) <= allowed;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
