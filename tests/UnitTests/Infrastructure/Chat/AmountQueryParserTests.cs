using FinFlow.Infrastructure.Chat;

namespace FinFlow.UnitTests.Infrastructure.Chat;

public class AmountQueryParserTests
{
    [Fact]
    public void Extract_DotSeparatedAmount_NormalizesToDigitsOnly()
    {
        var result = AmountQueryParser.ExtractAmountTokens("Có chứng từ nào tổng cộng 1.661.000 đồng không?");

        Assert.Contains("1661000", result);
    }

    [Fact]
    public void Extract_CommaSeparatedAmount_NormalizesToDigitsOnly()
    {
        var result = AmountQueryParser.ExtractAmountTokens("invoice total 1,661,000 VND");

        Assert.Contains("1661000", result);
    }

    [Fact]
    public void Extract_RawDigits_ReturnedAsIs()
    {
        var result = AmountQueryParser.ExtractAmountTokens("tìm hóa đơn 1661000");

        Assert.Contains("1661000", result);
    }

    [Fact]
    public void Extract_SmallNumberBelowThreshold_Ignored()
    {
        var result = AmountQueryParser.ExtractAmountTokens("phòng 100 có gì");

        Assert.DoesNotContain("100", result);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_NoNumbers_ReturnsEmpty()
    {
        var result = AmountQueryParser.ExtractAmountTokens("chứng từ của nhà cung cấp Grab");

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_MultipleAmounts_ReturnsAllAboveThreshold()
    {
        var result = AmountQueryParser.ExtractAmountTokens("giữa 1.661.000 và 2500000 đồng");

        Assert.Contains("1661000", result);
        Assert.Contains("2500000", result);
    }

    [Fact]
    public void Extract_Deduplicates_EquivalentTokens()
    {
        var result = AmountQueryParser.ExtractAmountTokens("1.661.000 hay 1661000?");

        Assert.Single(result);
        Assert.Contains("1661000", result);
    }

    [Fact]
    public void Extract_FourDigitBoundary_Included()
    {
        var result = AmountQueryParser.ExtractAmountTokens("phí 5000 đồng");

        Assert.Contains("5000", result);
    }

    [Fact]
    public void Extract_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(AmountQueryParser.ExtractAmountTokens(null!));
        Assert.Empty(AmountQueryParser.ExtractAmountTokens("   "));
    }
}
