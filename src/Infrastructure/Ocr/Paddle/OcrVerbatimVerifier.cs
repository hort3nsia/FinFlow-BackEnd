using System.Globalization;
using System.Text;

namespace FinFlow.Infrastructure.Ocr.Paddle;

/// <summary>
/// Verifies that values claimed by the LLM structurer actually appear in the
/// raw OCR text the structurer was given. This is the second layer of the
/// 4-layer hybrid guard:
///
///   1. Strict prompt forbidding hallucination (in PaddleOcrProvider).
///   2. Verbatim verification — THIS file.
///   3. Sanity bounds (PaddleOcrProvider.ApplySanityBounds).
///   4. Confidence floor — handled inside the sidecar parser.
///
/// Verbatim verification works on a "soft normalized" haystack:
///   - Diacritics stripped, lowercased, all separators (.,/ etc.) removed.
///   - Numbers compared via canonical digit string ("1.234.500" -> "1234500").
///   - Strings of length >= 3 compared as substring.
/// Anything that fails verification is dropped (string -> empty, number -> 0).
/// </summary>
public static class OcrVerbatimVerifier
{
    /// <summary>
    /// Returns the value if it is grounded in the OCR text, otherwise empty.
    /// </summary>
    public static string VerifyString(string value, string ocrText)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (string.IsNullOrWhiteSpace(ocrText)) return string.Empty;

        var needle = NormalizeText(value);
        if (needle.Length < 3) return string.Empty;

        var haystack = NormalizeText(ocrText);
        if (haystack.Contains(needle, StringComparison.Ordinal))
            return value.Trim();

        return string.Empty;
    }

    /// <summary>
    /// Returns the value if its canonical digit string (or a sum of canonical
    /// digit substrings) appears in the OCR text. The sum case allows the
    /// model to legitimately add up a column ("T.Tien") of line items even
    /// when the raw "Total" line was not OCR'd.
    /// </summary>
    public static decimal VerifyAmount(decimal value, string ocrText, IReadOnlyList<decimal>? lineItemTotals = null)
    {
        if (value == 0m) return 0m;
        if (string.IsNullOrWhiteSpace(ocrText)) return 0m;

        var canonical = ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

        // Build the set of WHOLE number tokens on the page. A number token is a run of
        // digits that may contain in-number separators ('.', ',', or a single space used
        // as a thousand separator), e.g. "1.234.500" / "22,410" / "1 234 500". The token's
        // canonical form drops those separators. We then match the value against a whole
        // token with '==' — NOT a substring of the all-digits blob. The old collapse-all
        // approach accepted 410 inside "224107" and bridged digits across unrelated numbers
        // (invoice codes, phone numbers), letting hallucinated amounts through.
        var tokens = ExtractCanonicalNumberTokens(ocrText);
        if (tokens.Contains(canonical))
            return value;

        // Allow a small rounding wiggle — last 2 digits may differ by ±5
        // because of the 10% VAT half-rounding receipts apply.
        if (canonical.Length >= 4 && tokens.Any(token => CloseEnough(token, canonical)))
            return value;

        // Allow legitimate column sums (line items add up to a total that
        // doesn't appear verbatim on the receipt).
        if (lineItemTotals is { Count: > 0 })
        {
            var lineSum = lineItemTotals.Sum();
            if (Math.Abs(lineSum - value) <= Math.Max(1m, value * 0.005m))
                return value;
        }

        return 0m;
    }

    /// <summary>
    /// Extracts whole number tokens and returns each token's canonical digit string
    /// (in-number separators '.', ',' and single internal spaces removed; leading zeros
    /// stripped). A run is terminated by any character that is not a digit / in-number
    /// separator, so digits belonging to two different printed numbers never merge.
    /// </summary>
    private static HashSet<string> ExtractCanonicalNumberTokens(string ocrText)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length == 0) return;
            // Strip in-number separators; trailing separators are harmless.
            var digits = new string(current.ToString().Where(char.IsDigit).ToArray());
            current.Clear();
            if (digits.Length == 0) return;
            var trimmed = digits.TrimStart('0');
            result.Add(trimmed.Length == 0 ? "0" : trimmed);
            result.Add(digits); // keep zero-padded form too
        }

        for (var i = 0; i < ocrText.Length; i++)
        {
            var ch = ocrText[i];
            if (char.IsDigit(ch))
            {
                current.Append(ch);
                continue;
            }

            // A separator only stays inside the number if it sits BETWEEN two digits
            // (e.g. "1.234", "22,410", "1 234"). Otherwise it terminates the run.
            var isInNumberSeparator = (ch == '.' || ch == ',' || ch == ' ')
                && current.Length > 0
                && i + 1 < ocrText.Length
                && char.IsDigit(ocrText[i + 1]);

            if (isInNumberSeparator)
                current.Append(ch);
            else
                Flush();
        }
        Flush();

        return result;
    }

    /// <summary>
    /// Returns the date if it appears in the OCR text in any common Vietnamese
    /// format (dd/MM/yyyy, dd-MM-yyyy, dd.MM.yyyy, yyyy-MM-dd). Otherwise null.
    /// </summary>
    public static DateOnly? VerifyDate(DateOnly? value, string ocrText)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(ocrText)) return null;

        var d = value.Value;
        // Strip every non-digit so OCR layout quirks (spaces, line breaks)
        // don't defeat the match. We then look for the digit triple in any order.
        var digits = new string(ocrText.Where(char.IsDigit).ToArray());
        var candidates = new[]
        {
            $"{d.Day:00}{d.Month:00}{d.Year:0000}",
            $"{d.Year:0000}{d.Month:00}{d.Day:00}",
            $"{d.Day:0}{d.Month:0}{d.Year:0000}",
            $"{d.Year:0000}{d.Month:0}{d.Day:0}",
            // 2-digit year fallback ("16/07/21").
            $"{d.Day:00}{d.Month:00}{d.Year % 100:00}",
        };
        return candidates.Any(c => digits.Contains(c, StringComparison.Ordinal)) ? value : null;
    }

    /// <summary>
    /// Tax IDs in Vietnam are 10 digits or 10-3. Verify the digit run appears
    /// somewhere in the OCR text.
    /// </summary>
    public static string? VerifyTaxId(string? value, string ocrText)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (string.IsNullOrWhiteSpace(ocrText)) return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is not (10 or 13)) return null;

        var haystack = new string(ocrText.Where(char.IsDigit).ToArray());
        return haystack.Contains(digits, StringComparison.Ordinal) ? value : null;
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch == 'đ' || ch == 'Đ' ? 'd' : ch));
        }
        return builder.ToString();
    }

    private static bool CloseEnough(string a, string b)
    {
        // Same-length digit strings differing only in the last 2 chars by <= 5.
        if (a.Length != b.Length) return false;
        if (a.Length < 4) return false;
        var prefixA = a[..^2];
        var prefixB = b[..^2];
        if (prefixA != prefixB) return false;
        if (!int.TryParse(a[^2..], out var tailA)) return false;
        if (!int.TryParse(b[^2..], out var tailB)) return false;
        return Math.Abs(tailA - tailB) <= 5;
    }
}
