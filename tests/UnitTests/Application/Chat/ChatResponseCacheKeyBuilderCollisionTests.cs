using FinFlow.Application.Chat.Services;
using FinFlow.Domain.Documents;

namespace FinFlow.UnitTests.Application.Chat;

public sealed class ChatResponseCacheKeyBuilderCollisionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid MembershipId = Guid.NewGuid();
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DocumentChunkType[] AllowedTypes =
        [DocumentChunkType.Expense, DocumentChunkType.Receipt];

    private static string BuildKey(string query)
    {
        var builder = new ChatResponseCacheKeyBuilder();
        return builder.Build(
            TenantId,
            MembershipId,
            "Staff",
            DepartmentId,
            OwnerId,
            AllowedTypes,
            query,
            "2026.05.3");
    }

    [Fact]
    public void Build_DifferentContent_SameFirstLastWordCount_ProducesDifferentKeys()
    {
        // Same first word ("chi"), same last word ("không"), same word count (6),
        // but different middle content (Alpha vs Beta). Must NOT collide.
        var keyAlpha = BuildKey("chi phí dự án Alpha bao không");
        var keyBeta = BuildKey("chi phí dự án Beta bao không");

        Assert.NotEqual(keyAlpha, keyBeta);
    }

    [Fact]
    public void Build_SameContent_DifferentCaseAndWhitespace_ProducesSameKey()
    {
        // Same query, only differing in casing and surrounding/internal whitespace.
        // Must still HIT the same cache entry.
        var keyA = BuildKey("chi phí dự án Alpha bao không");
        var keyB = BuildKey("  Chi   PHÍ dự án ALPHA  bao   Không ");

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void Build_DifferentTokenOrder_ProducesDifferentKeys()
    {
        // Reordered tokens are semantically different queries -> different keys.
        var keyA = BuildKey("chi phí dự án Alpha");
        var keyB = BuildKey("dự án Alpha chi phí");

        Assert.NotEqual(keyA, keyB);
    }
}
