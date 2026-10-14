using System.Globalization;

using Xunit.Abstractions;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Tests.Seeding;

/// <summary>Runs only when LUCENESUITE_SEED_CONNECTIONSTRING is set: the seeder writes to that database (dev/demo only).</summary>
public sealed class SeedFactAttribute : FactAttribute
{
    internal const string Variable = "LUCENESUITE_SEED_CONNECTIONSTRING";

    public SeedFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"Set {Variable} (plus LUCENESUITE_SEED_CONFIRM and LUCENESUITE_SEED_INDEX) to seed demo search history.";
        }
    }
}

/// <summary>
/// Seeds or clears demo SearchDaily and ItemDaily history. Variables: LUCENESUITE_SEED_CONNECTIONSTRING, LUCENESUITE_SEED_CONFIRM (database name),
/// LUCENESUITE_SEED_INDEX, LUCENESUITE_SEED_INDEXPATH, LUCENESUITE_SEED_TITLEFIELD (Title), LUCENESUITE_SEED_DAYS (60),
/// LUCENESUITE_SEED_PERDAY (150), LUCENESUITE_SEED_RANDOM (2026), LUCENESUITE_SEED_CLEAR (yyyy-MM-dd..yyyy-MM-dd).
/// </summary>
public sealed class SeedRunnerTests(ITestOutputHelper output)
{
    [SeedFact]
    public void SeedRunner()
    {
        string connectionString = Environment.GetEnvironmentVariable(SeedFactAttribute.Variable)!;
        SearchSeeder.Confirm(connectionString, Environment.GetEnvironmentVariable("LUCENESUITE_SEED_CONFIRM"));
        string index = Environment.GetEnvironmentVariable("LUCENESUITE_SEED_INDEX") is { Length: > 0 } name
            ? name
            : throw new InvalidOperationException("Set LUCENESUITE_SEED_INDEX to the index name.");

        if (Environment.GetEnvironmentVariable("LUCENESUITE_SEED_CLEAR") is { Length: > 0 } clear)
        {
            string[] parts = clear.Split("..");
            var (deleted, items) = SearchSeeder.Clear(connectionString, index, Day(parts[0]), Day(parts[^1]));
            output.WriteLine($"Cleared {deleted.Values.Sum()} SearchDaily rows and {items} ItemDaily rows of '{index}' from {clear}.");
            foreach (var (day, count) in deleted)
            {
                output.WriteLine($"  {day:yyyy-MM-dd}: {count}");
            }

            return;
        }

        string? indexPath = Environment.GetEnvironmentVariable("LUCENESUITE_SEED_INDEXPATH");
        var vocabulary = string.IsNullOrEmpty(indexPath)
            ? SeedVocabulary.BuiltIn()
            : SeedVocabulary.FromIndex(indexPath, Environment.GetEnvironmentVariable("LUCENESUITE_SEED_TITLEFIELD") is { Length: > 0 } field ? field : "Title");
        int days = Number("LUCENESUITE_SEED_DAYS", 60);
        int perDay = Number("LUCENESUITE_SEED_PERDAY", 150);
        int seed = Number("LUCENESUITE_SEED_RANDOM", 2026);
        var options = new SuiteAnalyticsOptions();

        var (from, to, rows, itemRows) = SearchSeeder.Seed(
            connectionString,
            index,
            days,
            DateOnly.FromDateTime(DateTime.UtcNow),
            (first, last) => SearchSeeder.Roll(SearchSeeder.Generate(vocabulary, first, last, perDay, seed, options), index, options));

        output.WriteLine($"Seeded '{index}' {from:yyyy-MM-dd}..{to:yyyy-MM-dd}: {rows} SearchDaily rows and {itemRows} ItemDaily rows from {vocabulary.DocumentCount} documents / {vocabulary.Texts.Count} texts, {perDay} searches per weekday, random seed {seed}.");
        output.WriteLine($"Clear with LUCENESUITE_SEED_CLEAR={from:yyyy-MM-dd}..{to:yyyy-MM-dd}");
    }

    private static DateOnly Day(string value) => DateOnly.ParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static int Number(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
}
