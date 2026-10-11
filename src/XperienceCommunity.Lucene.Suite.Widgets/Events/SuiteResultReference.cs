using System.Globalization;

using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Events;

/// <summary>The <c>sr</c> link parameter (<c>searchId.sequence.position</c>) that carries a search result to the page where a conversion happens.</summary>
/// <param name="SearchId">Search id of the result list.</param>
/// <param name="Sequence">Sequence of the request whose response listed the result.</param>
/// <param name="Position">1-based absolute position of the result.</param>
public readonly record struct SuiteResultReference(Guid SearchId, int Sequence, int Position)
{
    /// <summary>Query string parameter name for result links.</summary>
    public const string QueryParameter = "sr";

    private const int MaxLength = 64;

    /// <summary>Formats a reference as <c>&lt;searchId in N format&gt;.&lt;sequence&gt;.&lt;position&gt;</c>.</summary>
    public static string Format(Guid searchId, int sequence, int position) =>
        string.Create(CultureInfo.InvariantCulture, $"{searchId:N}.{sequence}.{position}");

    /// <summary>Parses a reference from untrusted input; never throws.</summary>
    /// <returns>False for anything but a non-empty N-format GUID, a sequence of at least 1 and a position from 1 to 1000.</returns>
    public static bool TryParse(string? value, out SuiteResultReference reference)
    {
        reference = default;
        if (value is null || value.Length > MaxLength)
        {
            return false;
        }

        string[] parts = value.Split('.');
        if (parts.Length != 3
            || !Guid.TryParseExact(parts[0], "N", out var searchId) || searchId == Guid.Empty
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int sequence) || sequence < 1
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int position) || position is < 1 or > SearchExecutor.MaxResultWindow)
        {
            return false;
        }

        reference = new SuiteResultReference(searchId, sequence, position);
        return true;
    }

    /// <inheritdoc cref="Format"/>
    public override string ToString() => Format(SearchId, Sequence, Position);
}
