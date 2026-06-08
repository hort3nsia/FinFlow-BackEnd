using FinFlow.Application.Chat.Interfaces;
using FinFlow.Domain.Documents;

namespace FinFlow.Application.Chat.Services;

/// <summary>
/// Reranks retrieved document chunks by fusing the incoming hybrid-retrieval order
/// (Reciprocal Rank Fusion of vector + keyword search) with a BM25 lexical re-score.
///
/// IMPORTANT (H4 fix): the input list arrives already ordered by RRF, which encodes
/// the *semantic* recall of the vector branch. Earlier this stage let BM25 — a pure
/// surface-term-overlap score — decide the final top-N on its own. That silently
/// discarded chunks the vector branch surfaced via meaning when the user's query was
/// a paraphrase/synonym with little lexical overlap (BM25 ≈ 0), defeating the entire
/// point of hybrid retrieval. To prevent that, BM25 is now only a *secondary* signal:
/// we fuse the BM25 ranking with the original RRF ranking (again via reciprocal rank
/// fusion), so a chunk ranked highly by retrieval cannot be dropped purely because it
/// shares few words with the query.
///
/// BM25 (Okapi BM25) is still the industry-standard lexical relevance algorithm
/// (Lucene/Elasticsearch) and accounts for term-frequency saturation (k1), document
/// length normalization (b), and inverse document frequency (idf).
///
/// For production-grade RAG, consider replacing the BM25 leg with a cross-encoder
/// reranker (e.g., Cohere Rerank API, BGE-Reranker) which preserves semantics directly.
/// </summary>
public sealed class RerankService : IRerankService
{
    // BM25 parameters — standard Lucene defaults.
    private const float K1 = 1.5f;
    private const float B = 0.75f;

    // RRF constant for fusing the retrieval rank with the BM25 rank. 60 is the
    // canonical value (Cormack 2009), matching ReciprocalRankFusion used upstream.
    private const double RrfConstant = 60.0;

    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\''];

    public Task<IReadOnlyList<(DocumentChunk Chunk, float Score)>> RerankAsync(
        string query,
        IEnumerable<DocumentChunk> chunks,
        int topN = 5,
        CancellationToken ct = default)
    {
        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
            return Task.FromResult<IReadOnlyList<(DocumentChunk, float)>>([]);

        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0)
        {
            // Empty query — return original order.
            var fallback = chunkList.Take(topN).Select(c => (c, 0f)).ToList();
            return Task.FromResult<IReadOnlyList<(DocumentChunk, float)>>(fallback);
        }

        // Tokenize all chunks once.
        var tokenizedChunks = chunkList
            .Select(c => (Chunk: c, Terms: Tokenize(c.Content)))
            .ToList();

        // Compute average document length (in tokens).
        var avgDocLength = tokenizedChunks.Count == 0
            ? 0
            : tokenizedChunks.Average(t => (double)t.Terms.Count);

        // Compute IDF for each query term. IDF = log((N - df + 0.5) / (df + 0.5) + 1).
        var totalDocs = tokenizedChunks.Count;
        var idf = new Dictionary<string, double>();
        foreach (var term in queryTerms.Distinct())
        {
            var df = tokenizedChunks.Count(t => t.Terms.Contains(term));
            idf[term] = Math.Log(((totalDocs - df + 0.5) / (df + 0.5)) + 1.0);
        }

        // BM25 score per chunk, preserving the incoming (RRF) order via index.
        var bm25Scored = tokenizedChunks
            .Select((t, retrievalRank) => (
                t.Chunk,
                RetrievalRank: retrievalRank,
                Bm25: ComputeBm25Score(queryTerms, t.Terms, idf, avgDocLength)))
            .ToList();

        // Rank by BM25 (1-indexed). Chunks with no lexical overlap share the worst rank.
        var bm25Order = bm25Scored
            .OrderByDescending(x => x.Bm25)
            .ToList();
        var bm25RankById = new Dictionary<Guid, int>();
        for (int i = 0; i < bm25Order.Count; i++)
            bm25RankById[bm25Order[i].Chunk.Id] = i;

        // Fuse retrieval rank (semantic, primary) with BM25 rank (lexical, secondary)
        // via reciprocal rank fusion. A chunk highly ranked by hybrid retrieval keeps a
        // strong score even when BM25 is ~0, so paraphrase answers survive the top-N cut.
        var results = bm25Scored
            .Select(x =>
            {
                var rrf = (1.0 / (RrfConstant + x.RetrievalRank + 1))
                          + (1.0 / (RrfConstant + bm25RankById[x.Chunk.Id] + 1));
                return (x.Chunk, Score: rrf);
            })
            .OrderByDescending(x => x.Score)
            .Take(topN)
            .ToList();

        return Task.FromResult<IReadOnlyList<(DocumentChunk, float)>>(
            results.Select(r => (r.Chunk, (float)r.Score)).ToList());
    }

    private static double ComputeBm25Score(
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<string> docTerms,
        IReadOnlyDictionary<string, double> idf,
        double avgDocLength)
    {
        if (docTerms.Count == 0) return 0;

        // Count term frequency in the document.
        var termFreq = new Dictionary<string, int>();
        foreach (var term in docTerms)
        {
            termFreq[term] = termFreq.TryGetValue(term, out var count) ? count + 1 : 1;
        }

        double score = 0;
        var docLength = (double)docTerms.Count;
        if (avgDocLength == 0) avgDocLength = 1;
        var lengthNorm = K1 * (1 - B + B * (docLength / avgDocLength));

        foreach (var queryTerm in queryTerms.Distinct())
        {
            if (!termFreq.TryGetValue(queryTerm, out var tf)) continue;
            if (!idf.TryGetValue(queryTerm, out var termIdf)) continue;

            score += termIdf * (tf * (K1 + 1)) / (tf + lengthNorm);
        }

        return score;
    }

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        return text
            .ToLowerInvariant()
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1) // Skip single-char tokens (a, i, ...).
            .ToList();
    }
}
