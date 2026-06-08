using FinFlow.Application.Common.Abstractions;
using FinFlow.Application.Documents.Ocr;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using PDFtoImage;
using SkiaSharp;

namespace FinFlow.Infrastructure.Ocr.Pdf;

public sealed class PdfPageRenderer : IPdfPageRenderer
{
    // OCR stays accurate well below full 300-DPI A4; capping the long edge keeps small
    // figures legible while drastically shrinking PNG size (H8).
    private const int MaxLongEdgePixels = 2200;

    public Task<Result<PdfRenderResult>> RenderAsync(
        byte[] pdfBytes,
        int maxPages,
        CancellationToken cancellationToken)
    {
        if (pdfBytes.Length == 0 || maxPages <= 0)
            return Task.FromResult(Result.Failure<PdfRenderResult>(DocumentOcrErrors.OcrPdfRenderFailed));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows()
                && !OperatingSystem.IsLinux()
                && !OperatingSystem.IsMacOS())
            {
                return Task.FromResult(Result.Failure<PdfRenderResult>(DocumentOcrErrors.OcrPdfRenderFailed));
            }

            var pageCount = Conversion.GetPageCount(pdfBytes, null);
            // H3: when truncating, render the first (maxPages-1) pages PLUS the last page —
            // invoice totals/tax summaries usually live on the final page.
            var pageIndices = SelectPageIndicesWithLast(pageCount, maxPages);
            var renderedPages = new List<OcrPageImage>(pageIndices.Count);
            var failedPages = 0;

            foreach (var pageIndex in pageIndices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var bitmap = Conversion.ToImage(pdfBytes, pageIndex, null, new RenderOptions());
                    // H8: downscale very large pages instead of letting the caller hard-reject
                    // them (> max bytes). Keeps the long edge within a bound that is still sharp
                    // enough for OCR while cutting PNG size dramatically.
                    using var prepared = Downscale(bitmap, MaxLongEdgePixels);
                    using var encoded = prepared.Encode(SKEncodedImageFormat.Png, 100);

                    if (encoded is null)
                    {
                        failedPages++;
                        continue;
                    }

                    renderedPages.Add(new OcrPageImage(
                        pageIndex + 1,
                        "image/png",
                        Convert.ToBase64String(encoded.ToArray())));
                }
                catch
                {
                    // Fix #7: per-page render error does not kill the whole document.
                    // If at least one page succeeds we surface a truncated result.
                    failedPages++;
                }
            }

            if (renderedPages.Count == 0)
                return Task.FromResult(Result.Failure<PdfRenderResult>(DocumentOcrErrors.OcrPdfRenderFailed));

            return Task.FromResult(Result.Success(PdfRenderResult.Success(renderedPages, pageCount)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Task.FromResult(Result.Failure<PdfRenderResult>(DocumentOcrErrors.OcrPdfRenderFailed));
        }
    }

    public Task<Result<int>> GetPageCountAsync(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        if (pdfBytes.Length == 0)
            return Task.FromResult(Result.Failure<int>(DocumentOcrErrors.OcrPdfRenderFailed));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageCount = Conversion.GetPageCount(pdfBytes, null);
            return Task.FromResult(Result.Success(pageCount));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Task.FromResult(Result.Failure<int>(DocumentOcrErrors.OcrPdfRenderFailed));
        }
    }

    /// <summary>
    /// Returns 0-based page indices to render: when the document has more pages than
    /// <paramref name="maxPages"/>, take the first (maxPages-1) plus the LAST page (H3).
    /// </summary>
    private static IReadOnlyList<int> SelectPageIndicesWithLast(int pageCount, int maxPages)
    {
        if (pageCount <= 0 || maxPages <= 0) return Array.Empty<int>();
        if (pageCount <= maxPages)
            return Enumerable.Range(0, pageCount).ToList();

        var indices = new List<int>(maxPages);
        for (var i = 0; i < maxPages - 1; i++)
            indices.Add(i);
        indices.Add(pageCount - 1); // always include the last page
        return indices;
    }

    /// <summary>
    /// Downscales a bitmap so its long edge is at most <paramref name="maxLongEdge"/> pixels
    /// (preserving aspect ratio). Returns the original bitmap when already within bounds (H8).
    /// </summary>
    private static SKBitmap Downscale(SKBitmap bitmap, int maxLongEdge)
    {
        var longEdge = Math.Max(bitmap.Width, bitmap.Height);
        if (longEdge <= maxLongEdge)
            return bitmap;

        var scale = (double)maxLongEdge / longEdge;
        var newWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var newHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        var resized = bitmap.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (resized is null)
            return bitmap;

        bitmap.Dispose();
        return resized;
    }
}
