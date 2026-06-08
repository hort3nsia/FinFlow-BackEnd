using FinFlow.Application.Documents.Ocr;

namespace FinFlow.Infrastructure.Ocr.LlmVision;

/// <summary>
/// Post-parse safety guard for LLM-vision OCR providers (Groq, OpenRouter).
///
/// Unlike the Paddle hybrid path, vision models receive the image directly and there is
/// no intermediate OCR text to verbatim-verify against. So this guard cannot do verbatim
/// grounding; instead it applies the checks that ARE possible without raw text:
///   1. Sanity bounds — drop impossible magnitudes (> 1 tỷ VND), and amounts that violate
///      basic ordering (vat > total, subtotal > total).
///   2. Arithmetic reconciliation — if subtotal + vat does not reconcile to total (beyond a
///      small tolerance), surface a warning so the human reviewer double-checks.
///   3. Provenance warning — mark the result as NOT verbatim-verified so downstream/UI can
///      treat vision-extracted figures as lower-trust than the Paddle hybrid path.
///
/// The guard never silently rewrites figures it cannot justify (other than the bounds
/// above); it prefers warning the reviewer over hiding a discrepancy.
/// </summary>
public static class LlmVisionSanityGuard
{
    private const decimal MaxAmount = 1_000_000_000m; // 1 tỷ VND

    public const string NotVerbatimVerifiedWarning =
        "Số liệu được trích bằng mô hình thị giác (chưa đối chiếu verbatim) — vui lòng kiểm tra lại.";

    public static OcrExtractionResult Apply(OcrExtractionResult parsed, int processedPageCount, bool wasTruncated)
    {
        var subtotal = parsed.Subtotal;
        var vat = parsed.Vat;
        var total = parsed.TotalAmount;

        var warnings = new List<string> { NotVerbatimVerifiedWarning };
        if (wasTruncated)
            warnings.Add($"Document was truncated. Only {processedPageCount} page(s) were processed.");

        // 1. Sanity bounds.
        if (subtotal > MaxAmount) { subtotal = 0m; warnings.Add("Tổng phụ vượt ngưỡng hợp lệ — đã loại, vui lòng nhập tay."); }
        if (vat > MaxAmount) { vat = 0m; warnings.Add("Tiền thuế vượt ngưỡng hợp lệ — đã loại, vui lòng nhập tay."); }
        if (total > MaxAmount) { total = 0m; warnings.Add("Tổng tiền vượt ngưỡng hợp lệ — đã loại, vui lòng nhập tay."); }
        if (vat > total && total > 0m) { vat = 0m; warnings.Add("Tiền thuế lớn hơn tổng tiền — đã loại, vui lòng kiểm tra."); }
        if (subtotal > total && total > 0m) { subtotal = 0m; warnings.Add("Tổng phụ lớn hơn tổng tiền — đã loại, vui lòng kiểm tra."); }

        // 2. Arithmetic reconciliation (only when all three are present).
        if (subtotal > 0m && total > 0m)
        {
            var expectedTotal = subtotal + vat;
            var tolerance = Math.Max(1m, total * 0.01m);
            if (Math.Abs(expectedTotal - total) > tolerance)
                warnings.Add("Tổng phụ + thuế không khớp tổng tiền — vui lòng kiểm tra lại số liệu.");
        }

        // 3. Missing-field warnings (parity with the Paddle path).
        if (string.IsNullOrWhiteSpace(parsed.VendorName))
            warnings.Add("Không xác định được nhà cung cấp — vui lòng nhập tay.");
        if (total == 0m)
            warnings.Add("Không trích xuất được tổng tiền — vui lòng nhập tay.");

        return new OcrExtractionResult(
            VendorName: parsed.VendorName,
            Reference: parsed.Reference,
            DocumentDate: parsed.DocumentDate,
            ExtractedInvoiceDueDate: parsed.ExtractedInvoiceDueDate,
            Category: parsed.Category,
            VendorTaxId: parsed.VendorTaxId,
            Subtotal: subtotal,
            Vat: vat,
            TotalAmount: total,
            Source: parsed.Source,
            ConfidenceLabel: "AI vision (unverified)",
            LineItems: parsed.LineItems,
            ProcessedPageCount: processedPageCount,
            CurrencyCode: parsed.CurrencyCode,
            Warnings: warnings.Distinct().ToList(),
            TaxLines: parsed.TaxLines ?? []);
    }
}
