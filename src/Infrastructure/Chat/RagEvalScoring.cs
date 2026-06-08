using System.Globalization;
using System.Text;

namespace FinFlow.Infrastructure.Chat;

/// <summary>
/// Pure, DB-free scoring primitives for <see cref="RagEvalHarness"/>: text normalization, the
/// golden-case relevance matcher, and the retrieval metrics (Recall@K, MRR). Kept separate and
/// <c>internal static</c> so the math can be unit-tested without spinning up the retrieval stack.
/// </summary>
internal static class RagEvalScoring
{
    /// <summary>
    /// Lower-cased, accent-stripped form with '.', ',' and whitespace removed, so a query author
    /// can write "Microsoft" or "3.393.500" and match chunk content regardless of diacritics or
    /// digit-grouping separators. Vietnamese đ/Đ is folded to d (FormD does not decompose it).
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is '.' or ',' or ' ' or '\t' or '\n' or '\r')
                continue;
            sb.Append(ch == 'đ' ? 'd' : ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// A chunk is relevant when its content contains EVERY <paramref name="expectAll"/> anchor and,
    /// when <paramref name="expectAny"/> is non-empty, AT LEAST ONE of those. Accent/case/separator
    /// insensitive via <see cref="Normalize"/>. An empty expectAll with empty expectAny matches
    /// nothing (a case with no anchors can never score a hit — guards against silent empty matchers).
    /// </summary>
    public static bool IsRelevant(
        IReadOnlyList<string> expectAll,
        IReadOnlyList<string> expectAny,
        string chunkContent)
    {
        if (expectAll.Count == 0 && expectAny.Count == 0)
            return false;

        var content = Normalize(chunkContent);

        foreach (var token in expectAll)
        {
            if (!content.Contains(Normalize(token), StringComparison.Ordinal))
                return false;
        }

        if (expectAny.Count > 0 &&
            !expectAny.Any(token => content.Contains(Normalize(token), StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Fraction of queries whose first relevant chunk landed at rank &lt;= <paramref name="k"/>,
    /// expressed as a count. A null rank means the query had no relevant chunk retrieved.
    /// </summary>
    public static int RecallAtK(IReadOnlyList<int?> firstRelevantRanks, int k) =>
        firstRelevantRanks.Count(rank => rank is { } r && r <= k);

    /// <summary>
    /// Mean reciprocal rank: average of 1/rank over all queries, contributing 0 for misses.
    /// Returns 0 for an empty set.
    /// </summary>
    public static double MeanReciprocalRank(IReadOnlyList<int?> firstRelevantRanks)
    {
        if (firstRelevantRanks.Count == 0)
            return 0.0;

        return firstRelevantRanks.Average(rank => rank is { } r && r > 0 ? 1.0 / r : 0.0);
    }
}
