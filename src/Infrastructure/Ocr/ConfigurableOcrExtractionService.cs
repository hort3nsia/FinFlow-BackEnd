using System.Security.Cryptography;
using FinFlow.Application.Common.Abstractions;
using FinFlow.Application.Documents.Ocr;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinFlow.Infrastructure.Ocr;

public sealed class ConfigurableOcrExtractionService : IOcrExtractionService
{
    private static readonly TimeSpan ContentHashCacheTtl = TimeSpan.FromHours(1);

    private readonly IReadOnlyDictionary<string, IOcrProvider> _providers;
    private readonly OcrOptions _options;
    private readonly ICacheService? _cacheService;
    private readonly ILogger<ConfigurableOcrExtractionService>? _logger;

    public ConfigurableOcrExtractionService(
        IEnumerable<IOcrProvider> providers,
        IOptions<OcrOptions> options,
        ICacheService? cacheService = null,
        ILogger<ConfigurableOcrExtractionService>? logger = null)
    {
        _providers = providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<OcrExtractionResult>> ExtractAsync(
        string fileName,
        string contentType,
        byte[] fileContents,
        CancellationToken cancellationToken)
    {
        // Content-hash cache hit short-circuits provider call entirely.
        var cacheKey = BuildCacheKey(fileContents);
        if (_options.EnableContentHashCache && _cacheService is not null && cacheKey is not null)
        {
            var cached = await _cacheService.GetAsync<OcrExtractionResult>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                _logger?.LogInformation("OCR cache HIT for content hash {Key}", cacheKey);
                return Result.Success(cached);
            }
        }

        var providerChain = BuildProviderChain();
        if (providerChain.Count == 0)
            return Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);

        Result<OcrExtractionResult> lastResult = Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
        // Best-effort fallback: a provider may return Success but with degraded content
        // (no total / no vendor). Instead of accepting it blindly (and caching it), we keep
        // it as a candidate and try the next provider in the chain, which may do better.
        // If no provider yields a clean result, we return the best degraded candidate.
        OcrExtractionResult? bestDegraded = null;
        foreach (var providerName in providerChain)
        {
            if (!_providers.TryGetValue(providerName, out var provider))
            {
                _logger?.LogWarning("OCR provider {Provider} configured but not registered; skipping", providerName);
                continue;
            }

            using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            providerCts.CancelAfter(TimeSpan.FromSeconds(_options.ProviderTimeoutSeconds));

            try
            {
                var startedAt = DateTime.UtcNow;
                lastResult = await provider.ExtractAsync(fileName, contentType, fileContents, providerCts.Token);
                var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;

                if (lastResult.IsSuccess)
                {
                    if (IsDegraded(lastResult.Value))
                    {
                        _logger?.LogWarning(
                            "OCR provider {Provider} returned a DEGRADED result in {ElapsedMs:0}ms (total={Total}, vendor='{Vendor}'); trying next provider for a better extraction",
                            providerName, elapsedMs, lastResult.Value.TotalAmount, lastResult.Value.VendorName);
                        bestDegraded ??= lastResult.Value;
                        continue;
                    }

                    _logger?.LogInformation(
                        "OCR provider {Provider} succeeded in {ElapsedMs:0}ms",
                        providerName, elapsedMs);

                    if (_options.EnableContentHashCache && _cacheService is not null && cacheKey is not null)
                    {
                        await _cacheService.SetAsync(cacheKey, lastResult.Value, ContentHashCacheTtl, cancellationToken);
                    }
                    return lastResult;
                }

                _logger?.LogWarning(
                    "OCR provider {Provider} failed: {Error}; trying next in fallback chain",
                    providerName, lastResult.Error.Description);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(
                    "OCR provider {Provider} timed out after {Timeout}s; trying next in fallback chain",
                    providerName, _options.ProviderTimeoutSeconds);
                lastResult = Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "OCR provider {Provider} threw; trying next in fallback chain",
                    providerName);
                lastResult = Result.Failure<OcrExtractionResult>(DocumentOcrErrors.OcrProviderUnavailable);
            }
        }

        // No provider produced a clean result. Prefer the best degraded candidate (so the
        // user still gets partial data to review) over a hard failure. Degraded results are
        // intentionally NOT cached — a later upload of the same file should retry providers.
        if (bestDegraded is not null)
        {
            _logger?.LogWarning("All OCR providers exhausted; returning best degraded result (not cached).");
            return Result.Success(bestDegraded);
        }

        return lastResult;
    }

    /// <summary>
    /// A result is "degraded" when the two fields that matter most for an expense document
    /// — the total amount and the vendor — are both unusable. We let a degraded result
    /// trigger a fallback to the next provider rather than accept/cache it immediately.
    /// </summary>
    private static bool IsDegraded(OcrExtractionResult result) =>
        result.TotalAmount <= 0m && string.IsNullOrWhiteSpace(result.VendorName);

    public async Task<Result<int>> GetPageCountAsync(
        string contentType,
        byte[] fileContents,
        CancellationToken cancellationToken)
    {
        // H1: page-count must follow the SAME fallback chain as ExtractAsync. Previously it
        // only used ActiveProvider, so when the Paddle sidecar was down the pre-flight
        // page-count call failed and the request returned an error BEFORE ExtractAsync (the
        // only place with provider fallback) ever ran — defeating the fallback entirely.
        var providerChain = BuildProviderChain();
        Result<int> lastResult = Result.Failure<int>(DocumentOcrErrors.OcrProviderUnavailable);

        foreach (var providerName in providerChain)
        {
            if (!_providers.TryGetValue(providerName, out var provider))
                continue;

            using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            providerCts.CancelAfter(TimeSpan.FromSeconds(_options.ProviderTimeoutSeconds));

            try
            {
                lastResult = await provider.GetPageCountAsync(contentType, fileContents, providerCts.Token);
                if (lastResult.IsSuccess)
                    return lastResult;

                _logger?.LogWarning(
                    "OCR page-count via {Provider} failed: {Error}; trying next provider",
                    providerName, lastResult.Error.Description);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning("OCR page-count via {Provider} timed out; trying next provider", providerName);
                lastResult = Result.Failure<int>(DocumentOcrErrors.OcrProviderUnavailable);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OCR page-count via {Provider} threw; trying next provider", providerName);
                lastResult = Result.Failure<int>(DocumentOcrErrors.OcrProviderUnavailable);
            }
        }

        return lastResult;
    }

    private List<string> BuildProviderChain()
    {
        var chain = new List<string> { _options.ActiveProvider };
        if (_options.ProviderFallbackChain is { Length: > 0 })
        {
            foreach (var fallback in _options.ProviderFallbackChain)
            {
                if (!string.IsNullOrWhiteSpace(fallback) && !chain.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                    chain.Add(fallback);
            }
        }
        return chain;
    }

    private static string? BuildCacheKey(byte[] contents)
    {
        if (contents is not { Length: > 0 }) return null;
        var hash = SHA256.HashData(contents);
        return "ocr:" + Convert.ToHexString(hash);
    }
}
