using FinFlow.Infrastructure.Ocr.Paddle;

namespace FinFlow.UnitTests.Infrastructure.Ocr;

/// <summary>
/// Tests for the verbatim grounding layer (layer 2 of the 4-layer anti-hallucination
/// guard). This class had zero coverage before; it gates accounting figures, so a
/// false-positive here lets a hallucinated amount through.
/// </summary>
public sealed class OcrVerbatimVerifierTests
{
    // ---- VerifyAmount: must reject a value that only appears as a substring of a
    // larger unrelated digit run (the C3 false-positive). ----

    [Fact]
    public void VerifyAmount_RejectsValueThatIsOnlySubstringOfLargerNumber()
    {
        // 410 is NOT a real amount on the receipt; it only appears inside "224107"
        // (e.g. an invoice code / phone number). It must NOT be accepted.
        const string ocr = "Ma hoa don: 224107\nNgay: 16/07/2021";
        var result = OcrVerbatimVerifier.VerifyAmount(410m, ocr);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void VerifyAmount_RejectsValueBridgingTwoSeparateNumbers()
    {
        // "1250" must not be matched by concatenating "...12" and "50..." across tokens.
        const string ocr = "SDT 0912 508877 thanh tien 99000";
        var result = OcrVerbatimVerifier.VerifyAmount(1250m, ocr);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void VerifyAmount_AcceptsExactTokenWithThousandSeparators()
    {
        const string ocr = "Tong cong: 1.234.500 VND";
        var result = OcrVerbatimVerifier.VerifyAmount(1234500m, ocr);
        Assert.Equal(1234500m, result);
    }

    [Fact]
    public void VerifyAmount_AcceptsValueWrittenWithCommaSeparators()
    {
        const string ocr = "Total: 22,410";
        var result = OcrVerbatimVerifier.VerifyAmount(22410m, ocr);
        Assert.Equal(22410m, result);
    }

    [Fact]
    public void VerifyAmount_AcceptsLineItemColumnSum_WhenTotalNotPrintedVerbatim()
    {
        // The printed total line wasn't OCR'd, but line items sum to it.
        const string ocr = "Item A 10000\nItem B 15000";
        var lineTotals = new[] { 10000m, 15000m };
        var result = OcrVerbatimVerifier.VerifyAmount(25000m, ocr, lineTotals);
        Assert.Equal(25000m, result);
    }

    [Fact]
    public void VerifyAmount_ReturnsZeroForZeroInput()
    {
        Assert.Equal(0m, OcrVerbatimVerifier.VerifyAmount(0m, "anything 123"));
    }

    [Fact]
    public void VerifyAmount_ReturnsZeroWhenOcrTextEmpty()
    {
        Assert.Equal(0m, OcrVerbatimVerifier.VerifyAmount(500m, ""));
    }

    // ---- VerifyString ----

    [Fact]
    public void VerifyString_AcceptsVendorPresentInText_IgnoringDiacritics()
    {
        const string ocr = "CONG TY TNHH HOA BINH";
        var result = OcrVerbatimVerifier.VerifyString("Hoà Bình", ocr);
        Assert.Equal("Hoà Bình", result);
    }

    [Fact]
    public void VerifyString_RejectsValueNotInText()
    {
        var result = OcrVerbatimVerifier.VerifyString("Vinamilk", "CONG TY ABC");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void VerifyString_RejectsTooShortNeedle()
    {
        // Length < 3 after normalization is rejected to avoid spurious matches.
        var result = OcrVerbatimVerifier.VerifyString("AB", "ABCDEF");
        Assert.Equal(string.Empty, result);
    }

    // ---- VerifyDate ----

    [Fact]
    public void VerifyDate_AcceptsDateInDayMonthYearFormat()
    {
        const string ocr = "Ngay lap: 16/07/2021";
        var result = OcrVerbatimVerifier.VerifyDate(new DateOnly(2021, 7, 16), ocr);
        Assert.Equal(new DateOnly(2021, 7, 16), result);
    }

    [Fact]
    public void VerifyDate_AcceptsTwoDigitYear()
    {
        const string ocr = "16/07/21";
        var result = OcrVerbatimVerifier.VerifyDate(new DateOnly(2021, 7, 16), ocr);
        Assert.Equal(new DateOnly(2021, 7, 16), result);
    }

    [Fact]
    public void VerifyDate_RejectsDateNotInText()
    {
        const string ocr = "Ngay lap: 16/07/2021";
        var result = OcrVerbatimVerifier.VerifyDate(new DateOnly(2020, 1, 1), ocr);
        Assert.Null(result);
    }

    [Fact]
    public void VerifyDate_ReturnsNullForNullInput()
    {
        Assert.Null(OcrVerbatimVerifier.VerifyDate(null, "16/07/2021"));
    }

    // ---- VerifyTaxId ----

    [Fact]
    public void VerifyTaxId_AcceptsTenDigitIdPresentInText()
    {
        const string ocr = "MST: 0312345678";
        var result = OcrVerbatimVerifier.VerifyTaxId("0312345678", ocr);
        Assert.Equal("0312345678", result);
    }

    [Fact]
    public void VerifyTaxId_AcceptsThirteenDigitId()
    {
        const string ocr = "Ma so thue 0312345678-001";
        var result = OcrVerbatimVerifier.VerifyTaxId("0312345678-001", ocr);
        Assert.Equal("0312345678-001", result);
    }

    [Fact]
    public void VerifyTaxId_RejectsWrongLength()
    {
        // 11 digits is neither 10 nor 13.
        var result = OcrVerbatimVerifier.VerifyTaxId("03123456789", "03123456789");
        Assert.Null(result);
    }

    [Fact]
    public void VerifyTaxId_RejectsIdNotInText()
    {
        var result = OcrVerbatimVerifier.VerifyTaxId("0312345678", "MST: 9999999999");
        Assert.Null(result);
    }
}
