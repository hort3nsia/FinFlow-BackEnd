using System.Text.RegularExpressions;

namespace FinFlow.Infrastructure.Chat;

/// <summary>
/// Extracts monetary amounts from a free-text query and normalizes them to
/// digit-only tokens (e.g. "1.661.000", "1,661,000" and "1661000" all -> "1661000").
///
/// Neural embeddings cannot distinguish specific numeric values, so amount-lookup
/// queries ("Có chứng từ nào tổng cộng 1.661.000 đồng không?") need a structured
/// exact-match path. These tokens drive a LIKE on chunk content (which stores both
/// the raw "1661000" and the separated "1.661.000" forms).
/// </summary>
internal static partial class AmountQueryParser
{
    // Minimum digit count to treat a number as an amount. Guards against false
    // positives on small incidental numbers ("phòng 100", "top 5").
    private const int MinDigits = 4;

    [GeneratedRegex(@"\d[\d.,]*\d|\d", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    public static IReadOnlyList<string> ExtractAmountTokens(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokens = new List<string>();

        foreach (Match match in NumberRegex().Matches(query))
        {
            var digitsOnly = StripSeparators(match.Value);
            if (digitsOnly.Length < MinDigits)
                continue;
            if (seen.Add(digitsOnly))
                tokens.Add(digitsOnly);
        }

        return tokens;
    }

    private static string StripSeparators(string raw)
    {
        Span<char> buffer = raw.Length <= 64 ? stackalloc char[raw.Length] : new char[raw.Length];
        var len = 0;
        foreach (var ch in raw)
        {
            if (ch is >= '0' and <= '9')
                buffer[len++] = ch;
        }

        return new string(buffer[..len]);
    }
}
