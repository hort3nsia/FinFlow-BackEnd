using System.Reflection;
using FinFlow.Domain.Abstractions;
namespace FinFlow.UnitTests.Domain;

public sealed class EmailChallengeTests
{
    [Fact]
    public void Create_UsableChallenge_StartsPendingAndCanResend()
    {
        var challenge = CreateChallenge("VerifyEmail", DateTime.UtcNow.AddMinutes(10));

        Assert.True((bool)challenge.GetType().GetProperty("IsUsable")!.GetValue(challenge)!);
        Assert.True((bool)challenge.GetType().GetProperty("CanResend")!.GetValue(challenge)!);
        Assert.Equal(0, (int)challenge.GetType().GetProperty("OtpFailedAttemptCount")!.GetValue(challenge)!);
    }

    [Fact]
    public void RegisterFailedOtpAttempt_RevokesChallenge_AfterMaxAttempts()
    {
        var challenge = CreateChallenge("ResetPassword", DateTime.UtcNow.AddMinutes(10));
        var method = challenge.GetType().GetMethod("RegisterFailedOtpAttempt", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);

        Result? lastResult = null;
        for (var i = 0; i < 5; i++)
        {
            lastResult = (Result)method!.Invoke(challenge, [new DateTime(2026, 4, 13, 2, 30, i, DateTimeKind.Utc)])!;
        }

        Assert.NotNull(lastResult);
        Assert.True(lastResult!.IsSuccess);
        Assert.True((bool)challenge.GetType().GetProperty("IsRevoked")!.GetValue(challenge)!);
        Assert.False((bool)challenge.GetType().GetProperty("IsUsable")!.GetValue(challenge)!);
        Assert.Equal(5, (int)challenge.GetType().GetProperty("OtpFailedAttemptCount")!.GetValue(challenge)!);
    }

    [Fact]
    public void Consume_MakesChallenge_Unusable_ForSecondUse()
    {
        var challenge = CreateChallenge("VerifyEmail", DateTime.UtcNow.AddMinutes(10));
        var consumeMethod = challenge.GetType().GetMethod("Consume", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(consumeMethod);

        var firstResult = (Result)consumeMethod!.Invoke(challenge, [new DateTime(2026, 4, 13, 2, 30, 0, DateTimeKind.Utc)])!;
        var secondResult = (Result)consumeMethod.Invoke(challenge, [new DateTime(2026, 4, 13, 2, 31, 0, DateTimeKind.Utc)])!;

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsFailure);
        Assert.False((bool)challenge.GetType().GetProperty("IsUsable")!.GetValue(challenge)!);
    }

    private static object CreateChallenge(string purposeName, DateTime expiresAtUtc)
    {
        var challengeType = Type.GetType("FinFlow.Domain.Entities.EmailChallenge, FinFlow.Domain");

        Assert.NotNull(challengeType);

        var purposeType = Type.GetType("FinFlow.Domain.Enums.EmailChallengePurpose, FinFlow.Domain");

        Assert.NotNull(purposeType);

        var purpose = Enum.Parse(purposeType!, purposeName);

        var createMethod = challengeType!.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(createMethod);

        var result = createMethod!.Invoke(null, [Guid.NewGuid(), purpose, expiresAtUtc]);

        Assert.NotNull(result);

        var valueProperty = result.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(valueProperty);

        return valueProperty!.GetValue(result)!;
    }
}
