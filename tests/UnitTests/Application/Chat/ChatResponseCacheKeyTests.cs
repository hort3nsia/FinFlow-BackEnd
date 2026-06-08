using FinFlow.Application.Chat.Services;
using FinFlow.Domain.Documents;

namespace FinFlow.UnitTests.Application.Chat;

public sealed class ChatResponseCacheKeyTests
{
    [Fact]
    public void Build_ChangesWhenPromptVersionChanges()
    {
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var allowedTypes = new[] { DocumentChunkType.Expense, DocumentChunkType.Receipt };
        const string query = "show tất cả expense giúp tôi";

        var keyA = ChatResponseCacheKey.Build(
            tenantId,
            membershipId,
            "Staff",
            departmentId,
            ownerId,
            allowedTypes,
            query,
            "2026.05.3");

        var keyB = ChatResponseCacheKey.Build(
            tenantId,
            membershipId,
            "Staff",
            departmentId,
            ownerId,
            allowedTypes,
            query,
            "2026.05.4");

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Build_DifferentContent_SameFirstLastWordCount_ProducesDifferentKeys()
    {
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var allowedTypes = new[] { DocumentChunkType.Expense, DocumentChunkType.Receipt };

        // Same first word, same last word, same word count (6), different middle content.
        var keyAlpha = ChatResponseCacheKey.Build(
            tenantId, membershipId, "Staff", departmentId, ownerId, allowedTypes,
            "chi phí dự án Alpha bao không", "2026.05.3");
        var keyBeta = ChatResponseCacheKey.Build(
            tenantId, membershipId, "Staff", departmentId, ownerId, allowedTypes,
            "chi phí dự án Beta bao không", "2026.05.3");

        Assert.NotEqual(keyAlpha, keyBeta);
    }

    [Fact]
    public void Build_SameContent_DifferentCaseAndWhitespace_ProducesSameKey()
    {
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var allowedTypes = new[] { DocumentChunkType.Expense, DocumentChunkType.Receipt };

        var keyA = ChatResponseCacheKey.Build(
            tenantId, membershipId, "Staff", departmentId, ownerId, allowedTypes,
            "chi phí dự án Alpha bao không", "2026.05.3");
        var keyB = ChatResponseCacheKey.Build(
            tenantId, membershipId, "Staff", departmentId, ownerId, allowedTypes,
            "  Chi   PHÍ dự án ALPHA  bao   Không ", "2026.05.3");

        Assert.Equal(keyA, keyB);
    }
}
