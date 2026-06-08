using FinFlow.Application.Chat.Interfaces;
using FinFlow.Domain.Documents;

namespace FinFlow.Application.Chat.Services;

public sealed class ChatResponseCacheKeyBuilder : IChatResponseCacheKeyBuilder
{
    private const string Prefix = "chat:response:";

    private static readonly string[] TimeRelativeMarkers =
    [
        "today", "yesterday", "tomorrow",
        "this month", "this year", "this quarter", "this week",
        "last month", "last year", "last week", "last quarter",
        "current month", "now", "current year",
        "hôm nay", "hôm qua", "tháng này", "năm nay", "tuần này", "quý này",
        "tháng trước", "năm trước", "tuần trước"
    ];

    public string Build(
        Guid tenantId,
        Guid membershipId,
        string role,
        Guid? departmentId,
        Guid? ownerFilter,
        IReadOnlyCollection<DocumentChunkType> allowedTypes,
        string query,
        string promptVersion)
    {
        var typesStr = string.Join(",", allowedTypes.OrderBy(t => t.ToString(), StringComparer.Ordinal).Select(t => t.ToString()));
        var normalized = NormalizeQueryStructure(query);
        var wordCount = GetWordCount(query);
        var keyInput = $"{promptVersion}|{membershipId}|{role}|{departmentId}|{ownerFilter}|{typesStr}|{wordCount}|{normalized}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(keyInput));
        var hex = Convert.ToHexString(hash);
        return $"{Prefix}{tenantId}:{hex}";
    }

    public bool IsCacheable(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        var wordCount = GetWordCount(query);
        if (wordCount < 3) return false;
        var lower = query.ToLowerInvariant();
        return !TimeRelativeMarkers.Any(marker => lower.Contains(marker));
    }

    private static string NormalizeQueryStructure(string query)
    {
        // Hash the FULL normalized query (lowercase, trimmed, whitespace-collapsed,
        // all tokens preserved) so that distinct queries never share a cache key,
        // while queries differing only in case/whitespace still hit the same entry.
        var words = query.Trim().ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join(' ', words);
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    private static int GetWordCount(string query)
    {
        return query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}