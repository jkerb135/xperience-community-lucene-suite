using System.Data;
using System.Globalization;

using Microsoft.Data.SqlClient;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Analytics.Rollup;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests.Seeding;

/// <summary>Raw rows generated for a range, with the number of logical searches started per day.</summary>
internal sealed record SeedTraffic(List<RawSearchRow> Searches, List<RawInteractionRow> Interactions, IReadOnlyDictionary<DateOnly, int> LogicalSearches);

/// <summary>Dev-only demo history: contract-faithful raw searches rolled by the real <see cref="RollupEngine"/> and written to SearchDaily without Kentico.</summary>
internal static class SearchSeeder
{
    public const int PageSize = 10;

    private static readonly string[] OffTopic = ["pizza delivery", "shipping costs", "job openings", "returns policy", "parking", "wifi password"];

    /// <summary>Generates searches for every day in [<paramref name="from"/>, <paramref name="to"/>]; weekends get half of <paramref name="perWeekday"/>.</summary>
    public static SeedTraffic Generate(SeedVocabulary vocabulary, DateOnly from, DateOnly to, int perWeekday, int seed, SuiteAnalyticsOptions options)
    {
        var random = new Random(seed);
        var texts = vocabulary.Texts.OrderBy(_ => random.Next()).ToList();
        // Zipf-like popularity: weight 1 / rank.
        double[] cumulative = new double[texts.Count];
        for (int i = 0; i < texts.Count; i++)
        {
            cumulative[i] = (i == 0 ? 0 : cumulative[i - 1]) + (1.0 / (i + 1));
        }

        // Typing rows 2–5 s apart, always well inside the text-change window.
        var typingGap = TimeSpan.FromSeconds(Math.Min(2, options.Merge.TextChangeWindow.TotalSeconds / 5));
        var traffic = new SeedTraffic([], [], new Dictionary<DateOnly, int>());
        var perDay = (Dictionary<DateOnly, int>)traffic.LogicalSearches;
        int rowId = 0;
        int interactionId = 0;
        Guid NewGuid()
        {
            var bytes = new byte[16];
            random.NextBytes(bytes);
            return new Guid(bytes);
        }

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            int count = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? perWeekday / 2 : perWeekday;
            perDay[day] = count;
            var sessions = Enumerable.Range(0, Math.Max(1, count / 3)).Select(_ => NewGuid()).ToArray();
            var dayStart = RollupScheduler.Midnight(day);
            for (int i = 0; i < count; i++)
            {
                string text = PickText(random, texts, cumulative);
                var matches = vocabulary.Match(text);
                var searchId = NewGuid();
                Guid? session = random.NextDouble() < 0.4 ? sessions[random.Next(sessions.Length)] : null;
                // Starts between 00:10 and 23:00 UTC, so the search and its attribution window stay clear of the next day's rollup.
                var at = dayStart.AddSeconds(random.Next(600, 82_800));
                var rows = new List<RawSearchRow>();
                RawSearchRow Row(string queryText, int page, bool submit)
                {
                    var row = new RawSearchRow(++rowId, searchId, rows.Count + 1, session, at, "", queryText, page, PageSize, vocabulary.Match(queryText).Count, Latency(random), submit);
                    rows.Add(row);
                    at = at.Add(typingGap * (1 + (1.5 * random.NextDouble())));
                    return row;
                }

                if (text.Length > 4)
                {
                    foreach (int length in Prefixes(random, text.Length))
                    {
                        Row(text[..length].TrimEnd(), 1, false);
                    }
                }

                var last = Row(text, 1, random.NextDouble() < 0.15);
                if (matches.Count > PageSize && random.NextDouble() < 0.1)
                {
                    last = Row(text, 2, false);
                }

                traffic.Searches.AddRange(rows);
                AddInteractions(random, traffic.Interactions, last, matches, NewGuid, ref interactionId);
            }
        }

        return traffic;
    }

    /// <summary>Rolls each day with the real engine.</summary>
    public static List<DailyKpi> Roll(SeedTraffic traffic, string indexName, SuiteAnalyticsOptions options)
    {
        var settings = RollupSettings.From(options);
        var searches = traffic.Searches.Select(row => row with { IndexName = indexName }).ToList();
        return traffic.LogicalSearches.Keys.Order()
            .SelectMany(day => RollupEngine.Roll(day, searches.Where(row => DateOnly.FromDateTime(row.ReceivedUtc) == day), traffic.Interactions.Where(row => DateOnly.FromDateTime(row.ReceivedUtc) == day), settings))
            .ToList();
    }

    /// <summary>Refuses unless <paramref name="confirm"/> is the target's database name.</summary>
    public static void Confirm(string connectionString, string? confirm)
    {
        string catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrEmpty(catalog) || !string.Equals(confirm, catalog, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Set LUCENESUITE_SEED_CONFIRM to the target database name ('{catalog}') to seed or clear it.");
        }
    }

    /// <summary>The <paramref name="days"/> days ending before the first day that real data or the rollup job can own.</summary>
    public static (DateOnly From, DateOnly To) Range(SqlConnection connection, SqlTransaction? transaction, string indexName, int days, DateOnly today)
    {
        if (!int.TryParse(Scalar(connection, transaction, "SELECT KeyValue FROM CMS_SettingsKey WHERE KeyName = @key", ("@key", AnalyticsModuleInstaller.VersionKeyName)) as string, out int version) || version < 3)
        {
            throw new InvalidOperationException("Lucene Suite analytics (installer version 3 or later) is not installed in the target database.");
        }

        var limits = new List<DateOnly> { today };
        if (DateOnly.TryParseExact(Scalar(connection, transaction, "SELECT KeyValue FROM CMS_SettingsKey WHERE KeyName = @key", ("@key", AnalyticsModuleInstaller.WatermarkKeyName)) as string, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var watermark))
        {
            limits.Add(watermark.AddDays(1));
        }

        if (Scalar(connection, transaction, "SELECT MIN(ReceivedUtc) FROM LuceneSuite_SearchEvent") is DateTime earliestRaw)
        {
            limits.Add(DateOnly.FromDateTime(earliestRaw));
        }

        if (Scalar(connection, transaction, "SELECT MIN(Day) FROM LuceneSuite_SearchDaily WHERE IndexName = @index", ("@index", indexName)) is DateTime firstDaily)
        {
            limits.Add(DateOnly.FromDateTime(firstDaily));
        }

        var to = limits.Min().AddDays(-1);
        return (to.AddDays(1 - days), to);
    }

    /// <summary>Seeds the range under the rollup lock in one serializable transaction; returns the range and the rows written.</summary>
    public static (DateOnly From, DateOnly To, int Rows) Seed(string connectionString, string indexName, int days, DateOnly today, Func<DateOnly, DateOnly, IReadOnlyList<DailyKpi>> kpis)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, 400);
        using var rollupLock = RollupLock.TryAcquire(connectionString) ?? throw new InvalidOperationException("The rollup job holds the rollup lock; try again when it has finished.");
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        var (from, to) = Range(connection, transaction, indexName, days, today);
        if ((int)Scalar(connection, transaction, "SELECT COUNT(*) FROM LuceneSuite_SearchDaily WHERE IndexName = @index AND Day BETWEEN @from AND @to", ("@index", indexName), ("@from", Day(from)), ("@to", Day(to)))! > 0)
        {
            throw new InvalidOperationException($"SearchDaily already has rows for '{indexName}' between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}.");
        }

        var rows = kpis(from, to);
        if (rows.Any(kpi => kpi.Day < from || kpi.Day > to || kpi.IndexName != indexName))
        {
            throw new InvalidOperationException("Generated rows fall outside the seed range or index.");
        }

        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction) { DestinationTableName = "LuceneSuite_SearchDaily" };
        var table = ToTable(rows);
        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        bulk.WriteToServer(table);
        transaction.Commit();
        return (from, to, rows.Count);
    }

    /// <summary>Deletes the index's SearchDaily rows in the range under the rollup lock; returns rows deleted per day.</summary>
    public static SortedDictionary<DateOnly, int> Clear(string connectionString, string indexName, DateOnly from, DateOnly to)
    {
        using var rollupLock = RollupLock.TryAcquire(connectionString) ?? throw new InvalidOperationException("The rollup job holds the rollup lock; try again when it has finished.");
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand("DELETE FROM LuceneSuite_SearchDaily OUTPUT deleted.Day WHERE IndexName = @index AND Day BETWEEN @from AND @to", connection);
        command.Parameters.AddWithValue("@index", indexName);
        command.Parameters.AddWithValue("@from", Day(from));
        command.Parameters.AddWithValue("@to", Day(to));
        var perDay = new SortedDictionary<DateOnly, int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var day = DateOnly.FromDateTime(reader.GetDateTime(0));
            perDay[day] = perDay.GetValueOrDefault(day) + 1;
        }

        return perDay;
    }

    private static string PickText(Random random, List<string> texts, double[] cumulative)
    {
        double r = random.NextDouble();
        if (r < 0.02)
        {
            return "";
        }

        if (r < 0.06)
        {
            return OffTopic[random.Next(OffTopic.Length)];
        }

        int index = Array.BinarySearch(cumulative, random.NextDouble() * cumulative[^1]);
        string text = texts[index >= 0 ? index : ~index];
        if (r < 0.14 && text.Length >= 5)
        {
            // A typo: one interior letter dropped.
            int drop = random.Next(1, text.Length - 1);
            return SeedVocabulary.Normalize(text.Remove(drop, 1));
        }

        return text;
    }

    private static IEnumerable<int> Prefixes(Random random, int length)
    {
        int first = Math.Min(3, length - 1);
        yield return first;
        if (length > 8 && random.NextDouble() < 0.5)
        {
            yield return random.Next(first + 1, length);
        }
    }

    private static int Latency(Random random)
    {
        // Log-normal: median ≈ 60 ms with a tail past one second.
        double normal = Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        return (int)Math.Clamp(Math.Exp(Math.Log(60) + (0.9 * normal)), 1, 20_000);
    }

    private static void AddInteractions(Random random, List<RawInteractionRow> interactions, RawSearchRow last, IReadOnlyList<Guid> matches, Func<Guid> newGuid, ref int id)
    {
        int pageStart = (last.Page - 1) * PageSize;
        int onPage = Math.Min(PageSize, last.ResultCount - pageStart);
        if (onPage <= 0 || random.NextDouble() >= 0.35)
        {
            return;
        }

        var clickAt = last.ReceivedUtc;
        int clicks = random.NextDouble() < 0.15 ? 2 : 1;
        for (int c = 0; c < clicks; c++)
        {
            int position = pageStart + WeightedRank(random, onPage);
            clickAt = clickAt.AddSeconds(random.Next(5, 60));
            interactions.Add(new RawInteractionRow(++id, last.SearchId, last.Sequence, matches[position - 1], position, SearchInteractionType.Click, newGuid(), clickAt));
        }

        if (random.NextDouble() < 0.08)
        {
            interactions.Add(new RawInteractionRow(++id, last.SearchId, last.Sequence, matches[pageStart], null, SearchInteractionType.Conversion, newGuid(), clickAt.AddMinutes(5)));
        }
    }

    // Rank k in 1..n with weight 1/k: top results are clicked most.
    private static int WeightedRank(Random random, int n)
    {
        double total = Enumerable.Range(1, n).Sum(k => 1.0 / k);
        double pick = random.NextDouble() * total;
        for (int k = 1; k <= n; k++)
        {
            pick -= 1.0 / k;
            if (pick <= 0)
            {
                return k;
            }
        }

        return n;
    }

    private static DataTable ToTable(IEnumerable<DailyKpi> kpis)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(SearchDailyInfo.Day), typeof(DateTime));
        table.Columns.Add(nameof(SearchDailyInfo.IndexName), typeof(string));
        table.Columns.Add(nameof(SearchDailyInfo.QueryText), typeof(string));
        string[] ints =
        [
            nameof(SearchDailyInfo.Searches), nameof(SearchDailyInfo.ZeroResultSearches), nameof(SearchDailyInfo.SubmittedSearches),
            nameof(SearchDailyInfo.ClickedSearches), nameof(SearchDailyInfo.Clicks),
        ];
        foreach (string column in ints)
        {
            table.Columns.Add(column, typeof(int));
        }

        table.Columns.Add(nameof(SearchDailyInfo.ClickPositionSum), typeof(long));
        foreach (string column in (string[])[nameof(SearchDailyInfo.ConvertedSearches), nameof(SearchDailyInfo.Conversions), nameof(SearchDailyInfo.SessionCount), .. Enumerable.Range(0, SearchDailyInfo.LatencyBucketCount).Select(i => $"LatencyBucket{i}")])
        {
            table.Columns.Add(column, typeof(int));
        }

        foreach (var kpi in kpis)
        {
            table.Rows.Add(
            [
                RollupScheduler.Midnight(kpi.Day), kpi.IndexName, kpi.QueryText, kpi.Searches, kpi.ZeroResultSearches, kpi.SubmittedSearches,
                kpi.ClickedSearches, kpi.Clicks, kpi.ClickPositionSum, kpi.ConvertedSearches, kpi.Conversions, kpi.SessionCount, .. kpi.LatencyBuckets.Cast<object>(),
            ]);
        }

        return table;
    }

    private static DateTime Day(DateOnly day) => RollupScheduler.Midnight(day);

    private static object? Scalar(SqlConnection connection, SqlTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = new SqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteScalar() is var result and not DBNull ? result : null;
    }
}
