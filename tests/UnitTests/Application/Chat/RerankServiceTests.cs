using FinFlow.Application.Chat.Services;
using FinFlow.Domain.Documents;

namespace FinFlow.UnitTests.Application.Chat;

public class RerankServiceTests
{
    private static DocumentChunk Chunk(string content)
    {
        return DocumentChunk.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            content,
            Guid.NewGuid().ToString("N"),
            0,
            [0.1f, 0.2f],
            DocumentChunkType.Policy);
    }

    // H4: the answer is found by VECTOR search via semantics, but the query is a
    // paraphrase that shares almost no surface terms with the chunk. Pure BM25 scores
    // that chunk ~0 and drops it from the top-N, throwing away the vector recall.
    // The fused input order (chunks arrive in RRF rank order) must remain the primary
    // signal so a top-ranked semantic chunk survives reranking.
    [Fact]
    public async Task RerankAsync_KeepsTopRankedSemanticChunk_EvenWithZeroKeywordOverlap()
    {
        var rerank = new RerankService();

        // Paraphrased question. None of its terms appear in the semantic answer chunk.
        const string query = "Ai chịu trách nhiệm phê duyệt khoản chi vượt ngân sách";

        // Answer chunk: semantically answers the question but uses different words.
        // Vector search ranked it #1 (it leads the fused list). BM25 overlap is zero.
        var answer = Chunk("Giám đốc tài chính là người ký các đề nghị thanh toán lớn hơn hạn mức cho phép.");

        // Lexical decoys that share many query terms => high BM25, low semantic value.
        var decoy1 = Chunk("Quy trình phê duyệt khoản chi vượt ngân sách yêu cầu nhiều cấp.");
        var decoy2 = Chunk("Trách nhiệm phê duyệt khoản chi thuộc về cấp quản lý.");
        var decoy3 = Chunk("Ngân sách phòng ban được phân bổ theo quý.");
        var decoy4 = Chunk("Khoản chi vượt mức cần giải trình.");

        // Input order == fused RRF order: the semantic answer is first.
        var fused = new[] { answer, decoy1, decoy2, decoy3, decoy4 };

        var result = await rerank.RerankAsync(query, fused, topN: 3);

        var topIds = result.Select(r => r.Chunk.Id).ToList();
        Assert.Contains(answer.Id, topIds);
    }
}
