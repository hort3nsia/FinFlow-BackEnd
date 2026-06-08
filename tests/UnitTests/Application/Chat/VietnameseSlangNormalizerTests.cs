using FinFlow.Application.Chat.Services;
using Xunit;

namespace FinFlow.UnitTests.Application.Chat;

/// <summary>
/// Tests for VietnameseSlangNormalizer - expands colloquial/slang Vietnamese terms
/// in a retrieval query into their formal equivalents BEFORE embedding, to raise recall
/// on slang queries. The normalizer APPENDS synonyms (keeps original terms) so a query
/// that is already correct is never broken.
/// </summary>
public class VietnameseSlangNormalizerTests
{
    [Fact]
    public void Expand_NullOrWhitespace_ReturnsInputUnchanged()
    {
        Assert.Equal("", VietnameseSlangNormalizer.Expand(""));
        Assert.Equal("   ", VietnameseSlangNormalizer.Expand("   "));
    }

    [Fact]
    public void Expand_QueryWithoutSlang_ReturnsOriginalUnchanged()
    {
        var query = "tổng chi phí phòng ban tháng này là bao nhiêu";
        Assert.Equal(query, VietnameseSlangNormalizer.Expand(query));
    }

    [Fact]
    public void Expand_KeepsOriginalSlangTerm()
    {
        // Must APPEND, not replace - original term stays in the query
        var result = VietnameseSlangNormalizer.Expand("đốt tiền cho marketing");
        Assert.Contains("đốt tiền", result);
        Assert.Contains("chi tiêu", result);
    }

    [Theory]
    [InlineData("đốt tiền", "chi tiêu")]
    [InlineData("ngốn tiền", "chi tiêu")]
    [InlineData("cháy túi", "chi tiêu")]
    [InlineData("xèng", "tiền")]
    [InlineData("lúa", "tiền")]
    public void Expand_CommonSlang_AppendsFormalSynonym(string slang, string formal)
    {
        var result = VietnameseSlangNormalizer.Expand($"vụ marketing {slang} bao nhiêu");
        Assert.Contains(formal, result);
        Assert.Contains(slang, result);
    }

    [Fact]
    public void Expand_RealWorldSlangQuery_AddsFormalTerms()
    {
        // "vụ quảng cáo sáng tạo hết bao nhiêu vậy"
        var result = VietnameseSlangNormalizer.Expand("vụ quảng cáo sáng tạo hết bao nhiêu vậy");
        Assert.Contains("vụ", result);
        // "vụ" -> khoản/chứng từ should be appended
        Assert.True(result.Contains("khoản") || result.Contains("chứng từ"));
    }

    [Fact]
    public void Expand_BenSlang_AppendsSupplierTerm()
    {
        // "bên grab có phát sinh gì không"
        var result = VietnameseSlangNormalizer.Expand("bên grab có phát sinh gì không");
        Assert.Contains("nhà cung cấp", result);
        Assert.Contains("grab", result);
    }

    [Fact]
    public void Expand_IsCaseInsensitive()
    {
        var result = VietnameseSlangNormalizer.Expand("ĐỐT TIỀN cho ads");
        Assert.Contains("chi tiêu", result);
    }

    [Fact]
    public void Expand_DoesNotDuplicateFormalTermIfAlreadyAppendedTwice()
    {
        // Multiple slang terms mapping to the same formal term should not explode
        var result = VietnameseSlangNormalizer.Expand("đốt tiền và ngốn tiền");
        var occurrences = result.Split("chi tiêu").Length - 1;
        Assert.True(occurrences <= 1, $"Expected formal term to appear at most once, got {occurrences}");
    }
}
