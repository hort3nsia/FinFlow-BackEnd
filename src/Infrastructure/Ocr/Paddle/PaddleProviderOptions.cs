namespace FinFlow.Infrastructure.Ocr.Paddle;

public sealed class PaddleProviderOptions
{
    /// <summary>Base URL of the PaddleOCR sidecar (e.g. "http://localhost:8081").</summary>
    public string BaseUrl { get; init; } = "http://localhost:8081";

    /// <summary>Per-request HTTP timeout (sidecar OCR + LLM structurer combined).</summary>
    public int RequestTimeoutSeconds { get; init; } = 60;

    /// <summary>Send the deskew/denoise preprocess flag to the sidecar.</summary>
    public bool EnablePreprocessing { get; init; } = true;

    /// <summary>
    /// When true, calls the sidecar /extract endpoint (pure rule-based, no LLM).
    /// When false (default), uses the hybrid pipeline: /ocr → LLM structurer → guards.
    /// </summary>
    public bool UseDeterministicOnly { get; init; }

    /// <summary>Min line confidence used to ASSESS overall OCR trust (guard 4 / low-confidence warning).</summary>
    public double MinLineConfidence { get; init; } = 0.50;

    /// <summary>
    /// H4: lower floor for INCLUDING a line in the text fed to the structurer. Faint figures
    /// (totals, tax IDs on thermal paper / phone photos) often sit at 0.3–0.5 confidence;
    /// dropping them before the structurer means the verbatim verifier can never recover them.
    /// We keep them in the feed (verbatim verification still gates what is trusted) but use
    /// the higher MinLineConfidence to decide whether to WARN about overall low confidence.
    /// </summary>
    public double StructurerTextMinConfidence { get; init; } = 0.30;

    /// <summary>How many pages of OCR text to feed into the structurer.</summary>
    public int MaxPagesForStructurer { get; init; } = 3;

    /// <summary>
    /// Structurer LLM details. The structurer NEVER sees the image, only the
    /// raw OCR text — so it cannot invent vendor names / amounts the OCR
    /// engine did not actually read. Hậu kiểm verbatim trong .NET cũng loại
    /// các giá trị không xuất hiện trong text gốc.
    /// </summary>
    public string StructurerBaseUrl { get; init; } = "https://api.groq.com/openai/v1";
    public string StructurerApiKey { get; init; } = string.Empty;
    public string StructurerModel { get; init; } = "llama-3.3-70b-versatile";
    public int StructurerTimeoutSeconds { get; init; } = 30;
    public int StructurerMaxTokens { get; init; } = 1500;
}
