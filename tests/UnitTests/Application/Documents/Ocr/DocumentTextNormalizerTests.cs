using FinFlow.Application.Documents.Ocr;

namespace FinFlow.UnitTests.Application.Documents.Ocr;

public sealed class DocumentTextNormalizerTests
{
    [Theory]
    [InlineData("B�CH H�A XANH")]
    [InlineData("�")]
    [InlineData("prefix � suffix")]
    public void ContainsUnrecoverableMojibake_ReturnsTrue_WhenReplacementCharPresent(string value)
    {
        Assert.True(DocumentTextNormalizer.ContainsUnrecoverableMojibake(value));
    }

    [Theory]
    [InlineData("BÁCH HÓA XANH")]
    [InlineData("OpenRouter Vendor")]
    [InlineData("")]
    [InlineData("   ")]
    public void ContainsUnrecoverableMojibake_ReturnsFalse_ForCleanText(string value)
    {
        Assert.False(DocumentTextNormalizer.ContainsUnrecoverableMojibake(value));
    }

    [Fact]
    public void NormalizeVendorName_DoesNotRepairReplacementChar_BecauseLossIsUnrecoverable()
    {
        // U+FFFD must be preserved verbatim, never routed into the lossy Latin1 repair path.
        var input = "B�CH H�A XANH";

        var result = DocumentTextNormalizer.NormalizeVendorName(input);

        Assert.Contains('�', result);
    }

    [Fact]
    public void NormalizeVendorName_PreservesVietnameseDiacritics()
    {
        var result = DocumentTextNormalizer.NormalizeVendorName("Bách Hóa Xanh");

        Assert.Equal("Bách Hóa Xanh", result);
    }
}
