using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinFlow.Application.Chat.Interfaces;
using FinFlow.Application.Chat.Services;
using FinFlow.Domain.Documents;
using FinFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinFlow.Infrastructure.Chat;

/// <summary>
/// Offline RETRIEVAL evaluation harness for the RAG pipeline. For every golden-set query it
/// runs the real retrieval stack — query embedding + pgvector ANN search + keyword (FTS) search
/// + Reciprocal Rank Fusion + BM25 rerank — exactly as <c>ChatService.PrepareRagExecutionAsync</c>
/// does, but STOPS before answer generation. No Groq/OpenRouter chat-completion call is made, so
/// the harness is deterministic and never hits the LLM rate limiter.
///
/// It then checks whether the chunk(s) a human marked as the expected answer for that query land
/// in the retrieved top-K, and reports Recall@K (K=5,10,20), MRR, hit-rate, and a per-query-type
/// breakdown so retrieval regressions surface as objective numbers instead of manual spot-checks.
///
/// Runs via CLI: <c>dotnet run -- eval-rag [goldenPath] [outFile]</c>. Tenant scope is resolved
/// from a fixed membership recorded in the golden-set config (a real TenantAdmin of the tenant
/// whose data the queries are written against), pushed via <see cref="ICurrentTenant.BeginScope"/>
/// because there is no HTTP request context in the CLI path.
/// </summary>
public sealed class RagEvalHarness
{
    private static readonly int[] RecallCutoffs = [5, 10, 20];
    private const int RetrievalPoolSize = 20;

    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IRerankService _rerankService;
    private readonly IChatAuthorizationService _authorizationService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<RagEvalHarness> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public RagEvalHarness(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IRerankService rerankService,
        IChatAuthorizationService authorizationService,
        ICurrentTenant currentTenant,
        ILogger<RagEvalHarness> logger)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _rerankService = rerankService;
        _authorizationService = authorizationService;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<int> RunAsync(string goldenPath, string? outFile, CancellationToken ct)
    {
        if (!File.Exists(goldenPath))
        {
            Console.WriteLine($"[eval-rag] Golden set file not found: {goldenPath}");
            return 1;
        }

        RagGoldenSet? goldenSet;
        try
        {
            var json = await File.ReadAllTextAsync(goldenPath, ct);
            goldenSet = JsonSerializer.Deserialize<RagGoldenSet>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eval-rag] Failed to parse golden set: {ex.Message}");
            return 1;
        }

        if (goldenSet is null || goldenSet.Cases.Count == 0)
        {
            Console.WriteLine("[eval-rag] Golden set is empty.");
            return 1;
        }

        if (!Guid.TryParse(goldenSet.TenantId, out var tenantId) ||
            !Guid.TryParse(goldenSet.MembershipId, out var membershipId))
        {
            Console.WriteLine("[eval-rag] Golden set must specify valid tenantId + membershipId (a TenantAdmin membership of the tenant whose data the queries target).");
            return 1;
        }

        Console.WriteLine($"[eval-rag] Loaded {goldenSet.Cases.Count} queries.");
        Console.WriteLine($"[eval-rag] Tenant {tenantId} via membership {membershipId}.");
        Console.WriteLine();

        // Push tenant context for the CLI path so authorization + query filters resolve.
        using var scope = _currentTenant.BeginScope(tenantId, membershipId, isSuperAdmin: false);

        ChatAccessScope accessScope;
        try
        {
            accessScope = await _authorizationService.GetChatAccessScopeAsync(membershipId, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eval-rag] Failed to resolve access scope for membership {membershipId}: {ex.Message}");
            return 1;
        }

        var results = new List<EvalResult>();
        foreach (var c in goldenSet.Cases)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(c.Query))
                continue;

            try
            {
                var topChunks = await RetrieveAsync(c.Query, tenantId, accessScope, ct);
                var firstRelevantRank = FindFirstRelevantRank(c, topChunks);
                results.Add(new EvalResult(c, firstRelevantRank, topChunks.Count));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retrieval failed for {Id}", c.Id);
                Console.WriteLine($"[eval-rag] Query {c.Id} failed: {ex.Message}");
                results.Add(new EvalResult(c, null, 0));
            }
        }

        var report = BuildReport(results, goldenSet, tenantId, membershipId);
        Console.WriteLine(report);

        if (!string.IsNullOrWhiteSpace(outFile))
        {
            await File.WriteAllTextAsync(outFile, report, ct);
            Console.WriteLine($"[eval-rag] Report written to {outFile}");
        }

        return 0;
    }

    /// <summary>
    /// Mirrors <c>ChatService.PrepareRagExecutionAsync</c> minus the answer-gen LLM: embed the
    /// query, run vector + keyword search, fuse with RRF, then rerank. Returns the reranked
    /// ordering trimmed to <see cref="RetrievalPoolSize"/> so Recall@5/10/20 read off one list.
    /// </summary>
    private async Task<IReadOnlyList<DocumentChunk>> RetrieveAsync(
        string query,
        Guid tenantId,
        ChatAccessScope accessScope,
        CancellationToken ct)
    {
        var queryEmbedding = await _embeddingService.EmbedAsync(query, ct);
        if (queryEmbedding is null || queryEmbedding.Length == 0)
            throw new InvalidOperationException("Failed to generate embedding for query.");

        var vectorChunks = await _vectorStore.SearchAsync(
            queryEmbedding,
            tenantId,
            departmentId: null,
            ownerId: null,
            accessScope.AllowedChunkTypes,
            RetrievalPoolSize,
            ct);

        IReadOnlyList<DocumentChunk> keywordChunks;
        try
        {
            keywordChunks = await _vectorStore.KeywordSearchAsync(
                query,
                tenantId,
                departmentId: null,
                ownerId: null,
                accessScope.AllowedChunkTypes,
                RetrievalPoolSize,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyword search failed; vector-only for this query.");
            keywordChunks = Array.Empty<DocumentChunk>();
        }

        var fused = ReciprocalRankFusion.Fuse(vectorChunks, keywordChunks, RetrievalPoolSize);

        // Rerank the whole fused pool (not just top-5) so we can read Recall@5/10/20 off the
        // reranked ordering — the order the user-facing pipeline would present.
        var reranked = await _rerankService.RerankAsync(query, fused, RetrievalPoolSize, ct);
        return reranked.Select(r => r.Chunk).ToList();
    }

    /// <summary>
    /// 1-indexed rank of the first chunk that satisfies the case's matcher, or null if none in
    /// the retrieved list does.
    /// </summary>
    private static int? FindFirstRelevantRank(RagGoldenCase c, IReadOnlyList<DocumentChunk> chunks)
    {
        for (var i = 0; i < chunks.Count; i++)
        {
            if (RagEvalScoring.IsRelevant(c.ExpectAll, c.ExpectAny, chunks[i].Content))
                return i + 1;
        }
        return null;
    }

    private string BuildReport(
        List<EvalResult> results,
        RagGoldenSet goldenSet,
        Guid tenantId,
        Guid membershipId)
    {
        var sb = new StringBuilder();
        var total = results.Count;
        if (total == 0)
            return "No results.";

        var ranks = results.Select(r => r.FirstRelevantRank).ToList();
        var hits = results.Count(r => r.FirstRelevantRank.HasValue);
        var mrr = RagEvalScoring.MeanReciprocalRank(ranks);

        sb.AppendLine("=== FinFlow RAG Retrieval Eval — Recall / MRR ===");
        sb.AppendLine($"Tenant:      {tenantId}");
        sb.AppendLine($"Membership:  {membershipId}");
        if (!string.IsNullOrWhiteSpace(goldenSet.Description))
            sb.AppendLine($"Set:         {goldenSet.Description}");
        sb.AppendLine($"Today:       {DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        sb.AppendLine($"Total queries: {total}");
        sb.AppendLine();

        sb.AppendLine("--- Overall ---");
        foreach (var k in RecallCutoffs)
        {
            var recallK = RagEvalScoring.RecallAtK(ranks, k);
            sb.AppendLine($"Recall@{k,-3} {Pct(recallK, total),7}  ({recallK}/{total})");
        }
        sb.AppendLine($"Hit-rate   {Pct(hits, total),7}  ({hits}/{total})  (>=1 relevant chunk anywhere in top-{RetrievalPoolSize})");
        sb.AppendLine($"MRR        {mrr,7:F3}");
        sb.AppendLine();

        sb.AppendLine("--- By query type ---");
        sb.AppendLine($"{"Type",-20}{"N",4}{"R@5",8}{"R@10",8}{"R@20",8}{"MRR",8}");
        foreach (var g in results.GroupBy(r => string.IsNullOrWhiteSpace(r.Case.QueryType) ? "(none)" : r.Case.QueryType)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var gRanks = g.Select(r => r.FirstRelevantRank).ToList();
            var n = gRanks.Count;
            var r5 = RagEvalScoring.RecallAtK(gRanks, 5);
            var r10 = RagEvalScoring.RecallAtK(gRanks, 10);
            var r20 = RagEvalScoring.RecallAtK(gRanks, 20);
            var gmrr = RagEvalScoring.MeanReciprocalRank(gRanks);
            sb.AppendLine($"{g.Key,-20}{n,4}{Pct(r5, n),8}{Pct(r10, n),8}{Pct(r20, n),8}{gmrr,8:F3}");
        }
        sb.AppendLine();

        // Rank histogram for hits.
        sb.AppendLine("--- First-relevant-rank distribution (hits only) ---");
        foreach (var g in results.Where(r => r.FirstRelevantRank.HasValue)
                     .GroupBy(r => r.FirstRelevantRank!.Value)
                     .OrderBy(g => g.Key))
            sb.AppendLine($"rank {g.Key,2}: {g.Count()}");
        sb.AppendLine();

        var misses = results.Where(r => !r.FirstRelevantRank.HasValue).ToList();
        sb.AppendLine($"--- MISSES ({misses.Count}) — expected chunk not in top-{RetrievalPoolSize} ---");
        foreach (var r in misses.OrderBy(r => r.Case.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"[{r.Case.Id}] ({r.Case.QueryType}) \"{r.Case.Query}\"");
            sb.AppendLine($"    expectAll: [{string.Join(", ", r.Case.ExpectAll)}]");
            if (r.Case.ExpectAny.Count > 0)
                sb.AppendLine($"    expectAny: [{string.Join(", ", r.Case.ExpectAny)}]");
            sb.AppendLine($"    retrieved: {r.RetrievedCount} chunks, none matched");
            if (!string.IsNullOrWhiteSpace(r.Case.Notes))
                sb.AppendLine($"    notes:     {r.Case.Notes}");
        }

        return sb.ToString();
    }

    private static string Pct(int n, int d) => d == 0 ? "0%" : $"{100.0 * n / d:F1}%";

    private sealed class RagGoldenSet
    {
        [JsonPropertyName("tenantId")] public string TenantId { get; set; } = "";
        [JsonPropertyName("membershipId")] public string MembershipId { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("cases")] public List<RagGoldenCase> Cases { get; set; } = [];
    }

    private sealed class RagGoldenCase
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("query")] public string Query { get; set; } = "";
        [JsonPropertyName("queryType")] public string QueryType { get; set; } = "";
        [JsonPropertyName("expectAll")] public List<string> ExpectAll { get; set; } = [];
        [JsonPropertyName("expectAny")] public List<string> ExpectAny { get; set; } = [];
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    }

    private sealed record EvalResult(RagGoldenCase Case, int? FirstRelevantRank, int RetrievedCount);
}
