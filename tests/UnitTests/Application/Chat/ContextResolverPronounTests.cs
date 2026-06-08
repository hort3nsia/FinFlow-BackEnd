using FinFlow.Application.Chat.Services;
using FinFlow.Domain.Chat;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinFlow.UnitTests.Application.Chat;

/// <summary>
/// Tests the deterministic pronoun/anaphora resolution in ContextResolver: a follow-up
/// like "ngày của nó là khi nào" must resolve the pronoun ("nó"/"đó"/...) to the most
/// recently referenced tracked entity of ANY type (vendor, document, expense, department,
/// person...), NOT just one hardcoded category. Runs with the Null LLM extractor so it
/// exercises the deterministic fallback only.
/// </summary>
public sealed class ContextResolverPronounTests
{
    private static ContextResolver CreateResolver() =>
        new(new ConfidenceScorer(), NullLlmEntityExtractor.Instance, NullLogger<ContextResolver>.Instance, new TextNormalizer());

    private static ConversationContext ContextWith(params (string name, EntityType type, int turn)[] entities)
    {
        var ctx = ConversationContext.Create(Guid.NewGuid());
        foreach (var (name, type, turn) in entities)
            ctx.AddEntity(TrackedEntity.Create(name, type, turn), turn);
        return ctx;
    }

    [Theory]
    [InlineData("ngày của nó là khi nào")]
    [InlineData("trạng thái của nó ra sao")]
    [InlineData("cái đó tổng bao nhiêu")]
    [InlineData("chứng từ đó duyệt chưa")]
    public async Task ResolveAsync_ResolvesPronoun_ToMostRecentEntity_VendorType(string query)
    {
        var resolver = CreateResolver();
        var context = ContextWith(("Bách Hóa Xanh", EntityType.VENDOR, 1));

        var result = await resolver.ResolveAsync(query, History("chi phí ở Bách Hóa Xanh là bao nhiêu"), context);

        Assert.Contains("Bách Hóa Xanh", result.ResolvedQuery);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesPronoun_ToDocumentEntity_NotJustVendor()
    {
        var resolver = CreateResolver();
        var context = ContextWith(("hóa đơn #INV-2026-0042", EntityType.DOCUMENT, 1));

        var result = await resolver.ResolveAsync("nó được duyệt chưa", History("xem hóa đơn #INV-2026-0042"), context);

        Assert.Contains("INV-2026-0042", result.ResolvedQuery);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesPronoun_ToExpenseEntity()
    {
        var resolver = CreateResolver();
        var context = ContextWith(("khoản chi tiếp khách quý 2", EntityType.EXPENSE, 1));

        var result = await resolver.ResolveAsync("nó thuộc hạng mục gì", History("khoản chi tiếp khách quý 2 là gì"), context);

        Assert.Contains("tiếp khách", result.ResolvedQuery);
    }

    [Fact]
    public async Task ResolveAsync_PicksMostRecentlyReferenced_WhenMultipleEntities()
    {
        var resolver = CreateResolver();
        // Two entities; the one referenced on the later turn should win the pronoun.
        var context = ContextWith(
            ("FPT Software", EntityType.VENDOR, 1),
            ("Highlands Coffee", EntityType.VENDOR, 3));

        var result = await resolver.ResolveAsync("nó chi bao nhiêu", History("chi phí ở Highlands Coffee"), context);

        Assert.Contains("Highlands Coffee", result.ResolvedQuery);
        Assert.DoesNotContain("FPT", result.ResolvedQuery);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotInjectEntity_WhenNoPronoun()
    {
        var resolver = CreateResolver();
        var context = ContextWith(("Bách Hóa Xanh", EntityType.VENDOR, 1));

        // A fresh, self-contained question with no anaphora must NOT be rewritten.
        var result = await resolver.ResolveAsync("tổng chi tiêu công ty tháng này", History("xin chào"), context);

        Assert.DoesNotContain("Bách Hóa Xanh", result.ResolvedQuery);
    }

    private static IReadOnlyList<ChatMessage> History(string userText) =>
        [ChatMessage.Create(Guid.NewGuid(), Guid.NewGuid(), ChatMessageRole.User, userText)];
}
