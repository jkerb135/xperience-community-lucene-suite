using System.Globalization;
using System.Text.RegularExpressions;

using Kentico.Xperience.Lucene.Core;

using Lucene.Net.Index;
using Lucene.Net.Store;

namespace XperienceCommunity.Lucene.Suite.Tests.Seeding;

/// <summary>Searchable titles with their ItemGuids; query texts and clicked items for the seed generator.</summary>
internal sealed partial class SeedVocabulary
{
    private const int MaxTextLength = 100;

    private static readonly string[] BuiltInTitles =
    [
        "Espresso blend", "Colombia single origin", "Ethiopia Yirgacheffe", "Kenya AA", "Decaf house blend", "Cold brew bags",
        "French press", "Pour over dripper", "Paper filters", "Burr grinder", "Hand grinder", "Espresso machine", "Milk frother",
        "Travel mug", "Ceramic mug", "Tea sampler", "Green tea", "Chai latte mix", "Hot chocolate", "Coffee scale",
        "Barista course", "Latte art guide", "Brewing guide", "Cafe Amsterdam", "Cafe Boston", "Cafe London", "Gift card",
        "Coffee subscription", "Descaling tablets", "Cleaning brush", "Kettle gooseneck", "Moka pot", "Aeropress filters",
        "Oat milk barista", "Caramel syrup", "Vanilla syrup", "Dark roast", "Light roast", "Medium roast", "Store opening hours",
    ];

    private readonly IReadOnlyList<(Guid ItemGuid, string Title)> documents;

    private SeedVocabulary(IEnumerable<(Guid ItemGuid, string Title)> documents)
    {
        this.documents = documents.Select(document => (document.ItemGuid, Normalize(document.Title))).Where(document => document.Item2.Length > 0).ToList();
        Texts = this.documents
            .SelectMany(document => WordPattern().Matches(document.Title).Select(word => word.Value).Where(word => word.Length >= 4).Prepend(document.Title))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Candidate query texts: every title and every word of four or more letters, normalized and distinct.</summary>
    public IReadOnlyList<string> Texts { get; }

    public int DocumentCount => documents.Count;

    /// <summary>Distinct ItemGuids whose title contains <paramref name="text"/> (case-insensitive), in document order.</summary>
    public IReadOnlyList<Guid> Match(string text)
    {
        string needle = Normalize(text);
        return documents.Where(document => needle.Length > 0 && document.Title.Contains(needle, StringComparison.Ordinal)).Select(document => document.ItemGuid).Distinct().ToList();
    }

    /// <summary>A fixed coffee-shop catalogue with deterministic ItemGuids (for databases without an index).</summary>
    public static SeedVocabulary BuiltIn() =>
        new(BuiltInTitles.Select((title, i) => (new Guid(i + 1, 0, 0, new byte[8]), title)));

    /// <summary>Reads stored titles and ItemGuids from a Kentico Lucene index folder (or one of its generation folders), read-only.</summary>
    public static SeedVocabulary FromIndex(string path, string titleField)
    {
        string folder = IndexFolder(path) ?? throw new DirectoryNotFoundException($"No Lucene index found in '{path}' or its i-g* generation folders.");
        using var directory = FSDirectory.Open(folder);
        using var reader = DirectoryReader.Open(directory);
        var liveDocs = MultiFields.GetLiveDocs(reader);
        var documents = new List<(Guid, string)>();
        for (int doc = 0; doc < reader.MaxDoc; doc++)
        {
            if (liveDocs is not null && !liveDocs.Get(doc))
            {
                continue;
            }

            var document = reader.Document(doc);
            if (Guid.TryParse(document.Get(BaseDocumentProperties.ITEM_GUID), out var itemGuid) && document.Get(titleField) is { } title)
            {
                documents.Add((itemGuid, title));
            }
        }

        return new SeedVocabulary(documents);
    }

    /// <summary>Lowercase, single-spaced, trimmed and capped: SQL Server compares case-insensitively and ignores trailing spaces, so texts must not differ only by those.</summary>
    internal static string Normalize(string text)
    {
        string normalized = SpacePattern().Replace(text.ToLower(CultureInfo.InvariantCulture), " ").Trim();
        return normalized.Length <= MaxTextLength ? normalized : normalized[..MaxTextLength].TrimEnd();
    }

    private static string? IndexFolder(string path)
    {
        if (OpenableIndex(path))
        {
            return path;
        }

        return System.IO.Directory.Exists(path)
            ? System.IO.Directory.GetDirectories(path, "i-g*")
                .Where(folder => !folder.EndsWith("_taxonomy", StringComparison.Ordinal))
                .OrderByDescending(folder => Path.GetFileName(folder), StringComparer.Ordinal)
                .FirstOrDefault(OpenableIndex)
            : null;
    }

    private static bool OpenableIndex(string folder)
    {
        if (!System.IO.Directory.Exists(folder))
        {
            return false;
        }

        using var directory = FSDirectory.Open(folder);
        return DirectoryReader.IndexExists(directory);
    }

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacePattern();
}
