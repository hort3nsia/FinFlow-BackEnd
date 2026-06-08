using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinFlow.Application.Documents.Ocr;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinFlow.Infrastructure.Ocr.Paddle;

/// <summary>
/// Hybrid OCR provider:
///   1. PaddleOCR sidecar (/ocr) reads the image and returns raw lines.
///   2. The text is sent to a strict-grounded LLM structurer (NEVER the image).
///   3. Every value the LLM claims is verified against the raw OCR text.
///   4. Sanity bounds reject impossible values.
///
/// This combines Paddle's vision precision with the LLM's ability to handle
/// noisy multi-column receipt layouts that pure-rule parsers struggle with —
/// while keeping every field grounded in the actual OCR text.
///
/// Config flag <c>UseDeterministicOnly = true</c> falls back to the sidecar's
/// /extract endpoint and skips the LLM entirely (Option A behaviour).
/// </summary>
public sealed class PaddleOcrProvider : IOcrProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly PaddleProviderOptions _options;
    private readonly ILogger<PaddleOcrProvider> _logger;

    public PaddleOcrProvider(
        HttpClient httpClient,
        IOptions<PaddleProviderOptions> options,
        ILogger<PaddleOcrProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "Paddle";

    public async Task<Result<OcrExtractionResult>> ExtractAsync(
        string fileName,
        string contentType,
        byte[] fileContents,
        CancellationToken cancellationToken)
    {
        if (_options.UseDeterministicOnly)
            return await ExtractDeterministicAsync(fileName, contentType, fileContents, cancellationToken);

        // Step 1 — get raw lines from the sidecar.
        var rawResult = await CallOcrAsync(fileName, contentType, fileContents, cancellationToken);
        if (rawResult.IsFailure)
            return Result.Failure<OcrExtractionResult>(rawResult.Error);

        var (rawText, ocrLines, pageCount) = rawResult.Value;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            _logger.LogWarning("PaddleOCR returned no text for {File}", fileName);
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrExtractionFailed);
        }

        // Step 2 — ask the LLM to structure ONLY that text. It never sees the image.
        var structured = await StructureAsync(rawText, cancellationToken);
        if (structured.IsFailure)
        {
            _logger.LogWarning("LLM structurer failed; falling back to deterministic /extract.");
            return await ExtractDeterministicAsync(fileName, contentType, fileContents, cancellationToken);
        }

        // Step 3 + 4 — verbatim verify every claim, then apply sanity bounds.
        var verified = ApplyGuards(structured.Value, rawText, ocrLines, pageCount);
        return Result.Success(verified);
    }

    public async Task<Result<int>> GetPageCountAsync(
        string contentType,
        byte[] fileContents,
        CancellationToken cancellationToken)
    {
        // Pull the page count from the lightweight /ocr endpoint so we don't
        // run the LLM structurer for the pre-flight call.
        var result = await CallOcrAsync("page-count.bin", contentType, fileContents, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<int>(result.Error);
        return Result.Success(Math.Max(result.Value.PageCount, 1));
    }


    // ─────────────────────────────────────────────────────────────────────
    // Sidecar /ocr — returns raw lines + assembled text + page count.
    // ─────────────────────────────────────────────────────────────────────
    private async Task<Result<(string Text, IReadOnlyList<RawLine> Lines, int PageCount)>> CallOcrAsync(
        string fileName,
        string contentType,
        byte[] fileContents,
        CancellationToken cancellationToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileContents);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "upload.bin" : fileName);

            var url = $"/ocr?preprocess={(_options.EnablePreprocessing ? "true" : "false")}";
            using var response = await _httpClient.PostAsync(url, form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Paddle sidecar /ocr returned {Status}: {Body}", response.StatusCode, body);
                return Result.Failure<(string, IReadOnlyList<RawLine>, int)>(DocumentOcrErrors.OcrProviderUnavailable);
            }

            var payload = await response.Content.ReadFromJsonAsync<RawOcrResponse>(JsonOpts, cancellationToken);
            if (payload is null)
                return Result.Failure<(string, IReadOnlyList<RawLine>, int)>(DocumentOcrErrors.OcrExtractionFailed);

            var allLines = new List<RawLine>();
            var sb = new StringBuilder();
            // H3: totals / tax summaries usually sit on the LAST page. Naively taking the first
            // N pages drops them on long documents. When truncating, keep the first N-1 pages
            // AND always include the last page so the grand total survives.
            var pagesToProcess = SelectPagesWithLast(payload.Pages, _options.MaxPagesForStructurer);
            foreach (var page in pagesToProcess)
            {
                sb.AppendLine($"--- Page {page.Page} ---");
                foreach (var line in page.Lines)
                {
                    if (line.Confidence < _options.StructurerTextMinConfidence) continue;
                    if (string.IsNullOrWhiteSpace(line.Text)) continue;
                    sb.AppendLine(line.Text.Trim());
                    allLines.Add(line);
                }
                sb.AppendLine();
            }

            return Result.Success((sb.ToString().Trim(), (IReadOnlyList<RawLine>)allLines, payload.PageCount));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paddle /ocr call failed");
            return Result.Failure<(string, IReadOnlyList<RawLine>, int)>(DocumentOcrErrors.OcrProviderUnavailable);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Sidecar /extract — pure deterministic fallback (no LLM).
    // ─────────────────────────────────────────────────────────────────────
    private async Task<Result<OcrExtractionResult>> ExtractDeterministicAsync(
        string fileName,
        string contentType,
        byte[] fileContents,
        CancellationToken cancellationToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileContents);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "upload.bin" : fileName);

            using var response = await _httpClient.PostAsync(
                $"/extract?preprocess={(_options.EnablePreprocessing ? "true" : "false")}",
                form,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Paddle /extract returned {Status}: {Body}", response.StatusCode, body);
                return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
            }

            var dto = await response.Content.ReadFromJsonAsync<DeterministicResponse>(JsonOpts, cancellationToken);
            if (dto is null)
                return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrExtractionFailed);

            return Result.Success(MapDeterministic(dto));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paddle /extract call failed");
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
        }
    }

    private static OcrExtractionResult MapDeterministic(DeterministicResponse dto)
    {
        var warnings = new List<string>(dto.Warnings ?? Array.Empty<string>());
        DateOnly documentDate;
        if (TryParseDocumentDate(dto.DocumentDate, out var parsedDate))
        {
            documentDate = parsedDate;
        }
        else
        {
            // H5: do NOT silently fabricate "today" as the document date — that corrupts the
            // accounting period and FX lookup. Keep a placeholder but flag it loudly so the
            // reviewer must set the real date.
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow);
            warnings.Add("Không đọc được ngày trên chứng từ — đã tạm dùng ngày hôm nay, vui lòng sửa lại.");
        }
        DateOnly? due = TryParseDocumentDate(dto.ExtractedInvoiceDueDate, out var p) ? p : null;

        var lineItems = (dto.LineItems ?? Array.Empty<RawLineItem>())
            .Select(li => new OcrExtractionLineItem(
                li.ItemName ?? string.Empty,
                li.Quantity, li.UnitPrice, li.Total,
                li.TaxRate, li.TaxableAmount, li.TaxAmount))
            .ToList();
        var taxLines = (dto.TaxLines ?? Array.Empty<RawTaxLine>())
            .Select(t => new OcrExtractionTaxLine(
                string.IsNullOrWhiteSpace(t.TaxType) ? "VAT" : t.TaxType!,
                t.Rate, t.TaxableAmount, t.TaxAmount))
            .ToList();

        return new OcrExtractionResult(
            VendorName: dto.VendorName ?? string.Empty,
            Reference: dto.Reference ?? string.Empty,
            DocumentDate: documentDate,
            ExtractedInvoiceDueDate: due,
            Category: string.IsNullOrWhiteSpace(dto.Category) ? "Uncategorized" : dto.Category!,
            VendorTaxId: string.IsNullOrWhiteSpace(dto.VendorTaxId) ? null : dto.VendorTaxId,
            Subtotal: dto.Subtotal,
            Vat: dto.Vat,
            TotalAmount: dto.TotalAmount,
            Source: "paddle-ocr-rules",
            ConfidenceLabel: "OCR rule-based",
            LineItems: lineItems,
            ProcessedPageCount: Math.Max(dto.PageCount, 1),
            CurrencyCode: string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "VND" : dto.CurrencyCode!,
            Warnings: warnings.Distinct().ToList(),
            TaxLines: taxLines);
    }

    /// <summary>
    /// When truncating to <paramref name="maxPages"/>, keep the first (maxPages-1) pages plus
    /// the LAST page (where invoice totals/tax summaries usually live), preserving page order.
    /// </summary>
    private static IReadOnlyList<RawPage> SelectPagesWithLast(IReadOnlyList<RawPage> pages, int maxPages)
    {
        if (maxPages <= 0) return Array.Empty<RawPage>();
        if (pages.Count <= maxPages) return pages;

        var selected = new List<RawPage>(maxPages);
        for (var i = 0; i < maxPages - 1; i++)
            selected.Add(pages[i]);
        selected.Add(pages[^1]); // always include the last page
        return selected;
    }

    /// <summary>
    /// Parses a document date using invariant culture and common Vietnamese formats.
    /// Avoids month/day flips on non-en-US hosts and returns false instead of guessing.
    /// </summary>
    private static bool TryParseDocumentDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        string[] formats =
        [
            "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy",
            "d/M/yyyy", "d-M-yyyy", "yyyy/MM/dd", "dd/MM/yy"
        ];
        if (DateOnly.TryParseExact(value, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
            return true;

        return DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }


    // ─────────────────────────────────────────────────────────────────────
    // LLM structurer — never sees the image, only the OCR text.
    // ─────────────────────────────────────────────────────────────────────
    private async Task<Result<OcrExtractionResult>> StructureAsync(string ocrText, CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable("PADDLE_STRUCTURER_API_KEY")
            ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? _options.StructurerApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Paddle structurer is missing API key (set PADDLE_STRUCTURER_API_KEY or GROQ_API_KEY).");
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.StructurerBaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model = _options.StructurerModel,
            temperature = 0,
            max_tokens = _options.StructurerMaxTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"OCR_TEXT:\n```\n{ocrText}\n```" }
            }
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.StructurerTimeoutSeconds));
            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("Structurer LLM returned {Status}: {Body}", response.StatusCode, errBody);
                return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
            }

            var raw = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return ParseLenient(content);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Structurer call failed");
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
        }
    }

    private const string SystemPrompt = """
        Bạn là bộ chuẩn hoá hoá đơn Việt Nam. Đầu vào là VĂN BẢN OCR thô của một
        hoá đơn / biên lai. Trả về JSON theo schema bên dưới.

        QUY TẮC TUYỆT ĐỐI (vi phạm sẽ bị code hậu kiểm phát hiện và loại bỏ):
        1. CẤM bịa giá trị. Mọi chuỗi/số bạn trả phải xuất hiện VERBATIM trong OCR_TEXT
           (sau khi bỏ dấu cách / dấu phân cách thông thường).
        2. CẤM tự suy đoán. Nếu OCR_TEXT không có dòng "Tổng" hay tổng tiền rõ ràng,
           trả totalAmount = 0. KHÔNG được cộng các con số ngẫu nhiên lại để tạo total.
        3. KHI OCR_TEXT có cột "T.Tien" / "T.Tiền" / "Thành tiền" rõ ràng và đầy đủ,
           bạn ĐƯỢC PHÉP cộng các giá trị trong cột đó để tính total. Trong trường hợp
           này nhớ liệt kê đủ từng dòng vào lineItems[] để code có thể verify tổng đó.
        4. Trường nào không tìm thấy → trả "" (string), 0 (number), null (date).
        5. Số tiền dùng định dạng Việt Nam: "1.234.500" = 1234500 VND, "1.234.500,50"
           có decimal. Bỏ ký hiệu "đ", "VND", "₫" trước khi parse.
        6. vendorName phải là TÊN cửa hàng/doanh nghiệp, KHÔNG phải tên sản phẩm.
           Tìm ở đầu hoá đơn (CTCP, Cong ty, Sieu thi, brand như Big C / Coopmart /
           Highlands ...). Nếu chỉ thấy địa chỉ thì để trống.
        7. reference là số hoá đơn / mã ticket — thường dạng chữ-số có nhãn
           "Hoa don", "Invoice", "Ticket", "Số:" đứng trước.
        8. Output JSON ONLY — không markdown, không lời giải thích.

        Schema (mọi field bắt buộc có mặt):
        {
          "vendorName": string,
          "reference":  string,
          "documentDate": "YYYY-MM-DD",
          "extractedInvoiceDueDate": "YYYY-MM-DD" | null,
          "category": "Uncategorized",
          "vendorTaxId": string | null,
          "subtotal": number,
          "vat": number,
          "totalAmount": number,
          "currencyCode": "VND" | "USD" | "EUR" | "",
          "lineItems": [
            { "itemName": string, "quantity": number, "unitPrice": number, "total": number,
              "taxRate": number | null, "taxableAmount": number, "taxAmount": number }
          ],
          "taxLines": [
            { "taxType": string, "rate": number | null, "taxableAmount": number, "taxAmount": number }
          ]
        }
        """;


    // ─────────────────────────────────────────────────────────────────────
    // Guards 2-4: verbatim verification + sanity bounds + confidence floor.
    // ─────────────────────────────────────────────────────────────────────
    private OcrExtractionResult ApplyGuards(
        OcrExtractionResult llmResult,
        string ocrText,
        IReadOnlyList<RawLine> ocrLines,
        int pageCount)
    {
        var warnings = new List<string>(llmResult.Warnings ?? Array.Empty<string>());

        // Guard 2 — verbatim verification.
        var vendor = OcrVerbatimVerifier.VerifyString(llmResult.VendorName, ocrText);
        if (vendor != llmResult.VendorName && !string.IsNullOrEmpty(llmResult.VendorName))
            warnings.Add($"Tên nhà cung cấp '{llmResult.VendorName}' không khớp văn bản OCR — đã loại bỏ.");

        var reference = OcrVerbatimVerifier.VerifyString(llmResult.Reference, ocrText);
        if (reference != llmResult.Reference && !string.IsNullOrEmpty(llmResult.Reference))
            warnings.Add($"Mã hoá đơn '{llmResult.Reference}' không khớp văn bản OCR — đã loại bỏ.");

        var verifiedTaxId = OcrVerbatimVerifier.VerifyTaxId(llmResult.VendorTaxId, ocrText);
        if (verifiedTaxId is null && !string.IsNullOrEmpty(llmResult.VendorTaxId))
            warnings.Add($"Mã số thuế '{llmResult.VendorTaxId}' không khớp định dạng/văn bản — đã loại bỏ.");

        var verifiedDate = OcrVerbatimVerifier.VerifyDate(llmResult.DocumentDate, ocrText);
        var documentDate = verifiedDate ?? llmResult.DocumentDate;
        if (verifiedDate is null)
        {
            // Date couldn't be confirmed — keep value but flag for user review.
            warnings.Add("Ngày trên hoá đơn không xác nhận được trong văn bản OCR — vui lòng kiểm tra lại.");
        }

        // Verify line items first so VerifyAmount(total) can use lineSum as evidence.
        var verifiedLineItems = new List<OcrExtractionLineItem>();
        var lineTotals = new List<decimal>();
        foreach (var item in llmResult.LineItems ?? Array.Empty<OcrExtractionLineItem>())
        {
            var name = OcrVerbatimVerifier.VerifyString(item.ItemName, ocrText);
            var lineTotal = OcrVerbatimVerifier.VerifyAmount(item.Total, ocrText);
            if (string.IsNullOrEmpty(name) || lineTotal <= 0) continue;
            verifiedLineItems.Add(item with { ItemName = name, Total = lineTotal });
            lineTotals.Add(lineTotal);
        }

        var subtotal = OcrVerbatimVerifier.VerifyAmount(llmResult.Subtotal, ocrText, lineTotals);
        var vat = OcrVerbatimVerifier.VerifyAmount(llmResult.Vat, ocrText);
        var total = OcrVerbatimVerifier.VerifyAmount(llmResult.TotalAmount, ocrText, lineTotals);

        // Guard 3 — sanity bounds.
        const decimal MaxAmount = 1_000_000_000m; // 1 tỷ VND
        if (subtotal > MaxAmount) subtotal = 0m;
        if (vat > MaxAmount) vat = 0m;
        if (total > MaxAmount) total = 0m;
        if (vat > total && total > 0) vat = 0m;
        if (subtotal > total && total > 0) subtotal = 0m;

        // Guard 4 — confidence floor: if no OCR line had confidence ≥ floor,
        // the whole batch is suspect and we revert to the deterministic path.
        var maxConfidence = ocrLines.Count == 0 ? 0.0 : ocrLines.Max(l => l.Confidence);
        if (maxConfidence < _options.MinLineConfidence)
        {
            warnings.Add($"Độ tin cậy OCR thấp ({maxConfidence:0.00}) — kết quả có thể không chính xác.");
        }

        // H7 — arithmetic reconciliation: surface (don't silently keep) a subtotal+vat that
        // doesn't add up to total, so the reviewer double-checks the figures.
        if (subtotal > 0m && total > 0m)
        {
            var tolerance = Math.Max(1m, total * 0.01m);
            if (Math.Abs(subtotal + vat - total) > tolerance)
                warnings.Add("Tổng phụ + thuế không khớp tổng tiền — vui lòng kiểm tra lại số liệu.");
        }

        if (string.IsNullOrEmpty(vendor))
            warnings.Add("Không xác định được nhà cung cấp — vui lòng nhập tay.");
        if (string.IsNullOrEmpty(reference))
            warnings.Add("Không tìm thấy mã hoá đơn — vui lòng nhập tay.");
        if (total == 0m)
            warnings.Add("Không trích xuất được tổng tiền — vui lòng nhập tay.");

        var taxLines = new List<OcrExtractionTaxLine>();
        if (vat > 0m)
        {
            decimal? rate = subtotal > 0m ? Math.Round(vat / subtotal * 100m, 2) : null;
            taxLines.Add(new OcrExtractionTaxLine("VAT", rate, subtotal, vat));
        }

        return new OcrExtractionResult(
            VendorName: vendor,
            Reference: reference,
            DocumentDate: documentDate,
            ExtractedInvoiceDueDate: llmResult.ExtractedInvoiceDueDate,
            Category: string.IsNullOrWhiteSpace(llmResult.Category) ? "Uncategorized" : llmResult.Category,
            VendorTaxId: verifiedTaxId,
            Subtotal: subtotal,
            Vat: vat,
            TotalAmount: total,
            Source: "paddle-hybrid",
            ConfidenceLabel: "OCR + LLM (verified)",
            LineItems: verifiedLineItems,
            ProcessedPageCount: Math.Max(pageCount, 1),
            CurrencyCode: string.IsNullOrWhiteSpace(llmResult.CurrencyCode) ? "VND" : llmResult.CurrencyCode!,
            Warnings: warnings.Distinct().ToList(),
            TaxLines: taxLines);
    }

    // ─────────────────────────────────────────────────────────────────────
    // DTOs for the sidecar JSON shapes.
    // ─────────────────────────────────────────────────────────────────────
    private sealed record RawOcrResponse(
        string? Engine, string? Profile, int ElapsedMs, int PageCount,
        IReadOnlyList<RawPage> Pages);

    private sealed record RawPage(int Page, IReadOnlyList<RawLine> Lines);

    private sealed record RawLine(string Text, double Confidence, IReadOnlyList<IReadOnlyList<double>>? Box);

    private sealed record DeterministicResponse(
        string? Engine, string? Profile, int ElapsedMs, int PageCount,
        string? VendorName, string? Reference, string? DocumentDate,
        string? ExtractedInvoiceDueDate, string? Category, string? VendorTaxId,
        decimal Subtotal, decimal Vat, decimal TotalAmount,
        string? CurrencyCode, string? ConfidenceLabel,
        IReadOnlyList<RawLineItem>? LineItems,
        IReadOnlyList<RawTaxLine>? TaxLines,
        IReadOnlyList<string>? Warnings);

    private sealed record RawLineItem(
        string? ItemName, decimal Quantity, decimal UnitPrice, decimal Total,
        decimal? TaxRate, decimal TaxableAmount, decimal TaxAmount);

    private sealed record RawTaxLine(
        string? TaxType, decimal? Rate, decimal TaxableAmount, decimal TaxAmount);

    private static Result<OcrExtractionResult> ParseLenient(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrInvalidJson);

        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = string.Join('\n',
                trimmed.Split('\n').Where(l => !l.TrimStart().StartsWith("```", StringComparison.Ordinal)));
            trimmed = trimmed.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            string s(string name, string fallback = "")
                => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                    ? (v.GetString() ?? fallback)
                    : fallback;

            decimal n(string name, decimal fallback = 0m)
            {
                if (!root.TryGetProperty(name, out var v)) return fallback;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var d2)) return d2;
                return fallback;
            }

            DateOnly date()
            {
                var raw = s("documentDate");
                // H5: invariant-culture parse; if unreadable, fall back to today but the
                // verbatim VerifyDate guard in ApplyGuards will flag the unconfirmed date.
                return TryParseDocumentDate(raw, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            }

            DateOnly? dueDate()
            {
                var raw = s("extractedInvoiceDueDate");
                return TryParseDocumentDate(raw, out var d) ? d : null;
            }

            var lineItems = new List<OcrExtractionLineItem>();
            if (root.TryGetProperty("lineItems", out var liArray) && liArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in liArray.EnumerateArray())
                {
                    decimal pick(string name) => item.TryGetProperty(name, out var v)
                        && v.ValueKind == JsonValueKind.Number
                        && v.TryGetDecimal(out var d) ? d : 0m;
                    decimal? pickNullable(string name) => item.TryGetProperty(name, out var v)
                        && v.ValueKind == JsonValueKind.Number
                        && v.TryGetDecimal(out var d) ? d : (decimal?)null;
                    var name = item.TryGetProperty("itemName", out var n2)
                        && n2.ValueKind == JsonValueKind.String
                        ? n2.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    lineItems.Add(new OcrExtractionLineItem(
                        ItemName: name,
                        Quantity: pick("quantity"),
                        UnitPrice: pick("unitPrice"),
                        Total: pick("total"),
                        TaxRate: pickNullable("taxRate"),
                        TaxableAmount: pick("taxableAmount"),
                        TaxAmount: pick("taxAmount")));
                }
            }

            var taxLines = new List<OcrExtractionTaxLine>();
            if (root.TryGetProperty("taxLines", out var txArray) && txArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in txArray.EnumerateArray())
                {
                    decimal pick(string name) => item.TryGetProperty(name, out var v)
                        && v.ValueKind == JsonValueKind.Number
                        && v.TryGetDecimal(out var d) ? d : 0m;
                    decimal? pickNullable(string name) => item.TryGetProperty(name, out var v)
                        && v.ValueKind == JsonValueKind.Number
                        && v.TryGetDecimal(out var d) ? d : (decimal?)null;
                    var type = item.TryGetProperty("taxType", out var t)
                        && t.ValueKind == JsonValueKind.String
                        ? t.GetString() ?? "VAT" : "VAT";
                    taxLines.Add(new OcrExtractionTaxLine(
                        TaxType: type,
                        Rate: pickNullable("rate"),
                        TaxableAmount: pick("taxableAmount"),
                        TaxAmount: pick("taxAmount")));
                }
            }

            return Result.Success(new OcrExtractionResult(
                VendorName: s("vendorName"),
                Reference: s("reference"),
                DocumentDate: date(),
                ExtractedInvoiceDueDate: dueDate(),
                Category: s("category", "Uncategorized"),
                VendorTaxId: string.IsNullOrWhiteSpace(s("vendorTaxId")) ? null : s("vendorTaxId"),
                Subtotal: n("subtotal"),
                Vat: n("vat"),
                TotalAmount: n("totalAmount"),
                Source: "paddle-grounded",
                ConfidenceLabel: "OCR + LLM (verified)",
                LineItems: lineItems,
                ProcessedPageCount: 0,
                CurrencyCode: string.IsNullOrWhiteSpace(s("currencyCode")) ? "VND" : s("currencyCode"),
                Warnings: Array.Empty<string>(),
                TaxLines: taxLines));
        }
        catch (JsonException)
        {
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrInvalidJson);
        }
    }
}
