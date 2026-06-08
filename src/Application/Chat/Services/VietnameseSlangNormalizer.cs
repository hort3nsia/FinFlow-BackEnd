using System.Text;

namespace FinFlow.Application.Chat.Services;

/// <summary>
/// Expands colloquial/slang Vietnamese terms in a retrieval query into their formal
/// equivalents to raise recall when the user phrases questions informally.
///
/// Design:
/// - Data-driven (a slang -> formal map), NOT per-sentence if/else.
/// - APPENDS the formal synonym after the original query instead of replacing terms,
///   so a query that is already correct is never broken and the original slang is kept.
/// - Each formal term is appended at most once, so multiple slang hits mapping to the
///   same formal term do not bloat the embedded query.
/// - Slang entries are intentionally narrow (multi-word or unambiguous tokens) to avoid
///   matching common words and introducing retrieval noise.
/// </summary>
public static class VietnameseSlangNormalizer
{
    // Ordered so longer/more specific phrases are matched before shorter ones.
    private static readonly (string Slang, string Formal)[] SlangMap =
    [
        // spending / burning money
        ("đốt tiền", "chi tiêu"),
        ("ngốn tiền", "chi tiêu"),
        ("ngốn", "chi tiêu"),
        ("cháy túi", "chi tiêu"),
        ("vung tiền", "chi tiêu"),
        ("tốn kém", "chi tiêu"),

        // money
        ("xèng", "tiền"),
        ("lúa", "tiền"),
        ("xìn", "tiền"),

        // an item / record / transaction
        ("vụ", "khoản chứng từ"),

        // supplier / counterparty
        ("bên", "nhà cung cấp"),
    ];

    public static string Expand(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return query;

        var lower = query.ToLowerInvariant();
        var appended = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (slang, formal) in SlangMap)
        {
            if (!lower.Contains(slang, StringComparison.Ordinal))
                continue;
            if (seen.Add(formal))
                appended.Add(formal);
        }

        if (appended.Count == 0)
            return query;

        var builder = new StringBuilder(query);
        foreach (var formal in appended)
        {
            builder.Append(' ');
            builder.Append(formal);
        }

        return builder.ToString();
    }
}
