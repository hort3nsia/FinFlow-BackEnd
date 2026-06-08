using FinFlow.Infrastructure.Chat;
using Npgsql;
using NpgsqlTypes;

namespace FinFlow.UnitTests.Infrastructure.Chat;

public class AmountContentMatchBuilderTests
{
    [Fact]
    public void Build_SingleToken_ProducesLikeClauseAndWildcardParameter()
    {
        var parameters = new List<NpgsqlParameter>();

        var predicate = AmountContentMatchBuilder.Build(new[] { "1661000" }, parameters);

        Assert.Equal("c.\"Content\" LIKE @amount0", predicate);
        var param = Assert.Single(parameters);
        Assert.Equal("amount0", param.ParameterName);
        Assert.Equal(NpgsqlDbType.Text, param.NpgsqlDbType);
        Assert.Equal("%1661000%", param.Value);
    }

    [Fact]
    public void Build_MultipleTokens_AreOredTogether()
    {
        var parameters = new List<NpgsqlParameter>();

        var predicate = AmountContentMatchBuilder.Build(new[] { "1661000", "2500000" }, parameters);

        Assert.Equal("c.\"Content\" LIKE @amount0 OR c.\"Content\" LIKE @amount1", predicate);
        Assert.Equal(2, parameters.Count);
        Assert.Equal("%1661000%", parameters[0].Value);
        Assert.Equal("%2500000%", parameters[1].Value);
    }

    [Fact]
    public void Build_EmptyTokens_ReturnsNullAndAddsNoParameters()
    {
        var parameters = new List<NpgsqlParameter>();

        var predicate = AmountContentMatchBuilder.Build(Array.Empty<string>(), parameters);

        Assert.Null(predicate);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Build_TokenWithNonDigits_Throws()
    {
        var parameters = new List<NpgsqlParameter>();

        Assert.Throws<ArgumentException>(() =>
            AmountContentMatchBuilder.Build(new[] { "166%1000" }, parameters));
    }
}
