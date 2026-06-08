using FinFlow.Infrastructure.Chat;
using Xunit;

namespace FinFlow.UnitTests.Infrastructure.Chat;

public class RagEvalScoringTests
{
    [Fact]
    public void Normalize_strips_accents_case_and_separators()
    {
        Assert.Equal("microsoftvietnam", RagEvalScoring.Normalize("Microsoft Vietnam"));
        Assert.Equal("3393500", RagEvalScoring.Normalize("3.393.500"));
        Assert.Equal("daotaoskillhub", RagEvalScoring.Normalize("Đào tạo SkillHub"));
        Assert.Equal("quangcaosangtao", RagEvalScoring.Normalize("Quảng Cáo Sáng Tạo"));
    }

    [Fact]
    public void Normalize_handles_null_and_empty()
    {
        Assert.Equal("", RagEvalScoring.Normalize(null));
        Assert.Equal("", RagEvalScoring.Normalize(""));
    }

    [Fact]
    public void IsRelevant_requires_every_expectAll_anchor()
    {
        var content = "Merchant: Microsoft Vietnam\nTotal: 3393500\nStatus: Approved";

        Assert.True(RagEvalScoring.IsRelevant(["Microsoft Vietnam", "3393500"], [], content));
        // Missing one anchor -> not relevant.
        Assert.False(RagEvalScoring.IsRelevant(["Microsoft Vietnam", "9999999"], [], content));
    }

    [Fact]
    public void IsRelevant_matches_accent_insensitively_against_diacritic_content()
    {
        var content = "Merchant: Đào tạo SkillHub\nCategory: Đào tạo & Phát triển";

        // Query author wrote anchors WITHOUT diacritics; chunk content HAS them.
        Assert.True(RagEvalScoring.IsRelevant(["Dao tao SkillHub"], [], content));
    }

    [Fact]
    public void IsRelevant_matches_amount_regardless_of_grouping_separators()
    {
        var content = "Total: 6699000";
        Assert.True(RagEvalScoring.IsRelevant(["6.699.000"], [], content));
    }

    [Fact]
    public void IsRelevant_expectAny_requires_at_least_one()
    {
        var content = "Category: Bảo hiểm\nMerchant: Viettel Solutions";

        Assert.True(RagEvalScoring.IsRelevant(["Category: Bao hiem"], ["Viettel Solutions", "Pizza 4Ps"], content));
        // expectAll passes but none of expectAny present.
        Assert.False(RagEvalScoring.IsRelevant(["Category: Bao hiem"], ["Grab", "Tiki"], content));
    }

    [Fact]
    public void IsRelevant_empty_matcher_never_matches()
    {
        Assert.False(RagEvalScoring.IsRelevant([], [], "any content"));
    }

    [Fact]
    public void RecallAtK_counts_ranks_at_or_below_cutoff()
    {
        var ranks = new int?[] { 1, 3, 8, 15, null };

        Assert.Equal(2, RagEvalScoring.RecallAtK(ranks, 5));   // ranks 1, 3
        Assert.Equal(3, RagEvalScoring.RecallAtK(ranks, 10));  // ranks 1, 3, 8
        Assert.Equal(4, RagEvalScoring.RecallAtK(ranks, 20));  // ranks 1, 3, 8, 15
        Assert.Equal(0, RagEvalScoring.RecallAtK(ranks, 0));
    }

    [Fact]
    public void MeanReciprocalRank_averages_reciprocals_with_misses_as_zero()
    {
        // ranks: 1 -> 1.0, 2 -> 0.5, miss -> 0.0  => (1.0 + 0.5 + 0) / 3
        var ranks = new int?[] { 1, 2, null };
        Assert.Equal((1.0 + 0.5 + 0.0) / 3.0, RagEvalScoring.MeanReciprocalRank(ranks), 6);
    }

    [Fact]
    public void MeanReciprocalRank_all_top1_is_one()
    {
        var ranks = new int?[] { 1, 1, 1 };
        Assert.Equal(1.0, RagEvalScoring.MeanReciprocalRank(ranks), 6);
    }

    [Fact]
    public void MeanReciprocalRank_empty_is_zero()
    {
        Assert.Equal(0.0, RagEvalScoring.MeanReciprocalRank([]));
    }
}
