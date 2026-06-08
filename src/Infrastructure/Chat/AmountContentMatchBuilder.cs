using Npgsql;
using NpgsqlTypes;

namespace FinFlow.Infrastructure.Chat;

/// <summary>
/// Builds an OR'd set of <c>"Content" LIKE @amountN</c> clauses for monetary-amount
/// exact matching, plus the matching parameters. Neural embeddings cannot distinguish
/// specific numbers, so amount-lookup queries are folded into keyword search via a
/// literal digit-string LIKE against chunk content (which stores both the raw
/// "1661000" and the separated "1.661.000" forms).
/// </summary>
internal static class AmountContentMatchBuilder
{
    /// <summary>
    /// Returns the OR'd LIKE predicate fragment (without surrounding parentheses) and
    /// appends the corresponding <c>@amountN</c> parameters to <paramref name="parameters"/>.
    /// Returns null when there are no tokens.
    /// </summary>
    public static string? Build(IReadOnlyList<string> amountTokens, IList<NpgsqlParameter> parameters)
    {
        if (amountTokens is not { Count: > 0 })
            return null;

        var clauses = new List<string>(amountTokens.Count);
        for (var i = 0; i < amountTokens.Count; i++)
        {
            var token = amountTokens[i];
            if (string.IsNullOrEmpty(token) || !token.All(char.IsAsciiDigit))
                throw new ArgumentException("Amount tokens must contain digits only.", nameof(amountTokens));

            var paramName = $"amount{i}";
            clauses.Add($"c.\"Content\" LIKE @{paramName}");
            parameters.Add(new NpgsqlParameter(paramName, NpgsqlDbType.Text) { Value = $"%{token}%" });
        }

        return string.Join(" OR ", clauses);
    }
}
