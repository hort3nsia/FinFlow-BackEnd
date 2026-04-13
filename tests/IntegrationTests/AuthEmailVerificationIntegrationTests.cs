using FinFlow.Application.Auth.Commands.ResendEmailVerification;
using FinFlow.Application.Auth.Commands.VerifyEmailByOtp;
using FinFlow.Application.Auth.Commands.VerifyEmailByToken;
using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinFlow.IntegrationTests;

public sealed class AuthEmailVerificationIntegrationTests
{
    private readonly AuthFlowTestFixture _fixture = new();

    [Fact]
    public async Task VerifyEmailByTokenCommandHandler_MarksAccountVerified_AndConsumesChallenge()
    {
        using var scope = _fixture.CreateScope();

        var account = scope.SeedAccount("handler.verify.token@finflow.test", "P@ssw0rd!");
        const string rawToken = "verify-token-123";
        const string rawOtp = "123456";
        var nowUtc = scope.Clock.UtcNow;

        var challenge = EmailChallenge.Create(
            account.Id,
            EmailChallengePurpose.VerifyEmail,
            nowUtc.AddMinutes(-5),
            nowUtc.AddMinutes(10),
            email: account.Email,
            tokenHash: scope.SecretService.HashChallengeToken(rawToken),
            otpHash: scope.SecretService.HashChallengeOtp(rawOtp),
            lastSentAtUtc: nowUtc.AddMinutes(-1)).Value;

        scope.DbContext.Add(challenge);
        await scope.SaveSeedAsync();

        var handler = ActivatorUtilities.CreateInstance<VerifyEmailByTokenCommandHandler>(scope.ServiceProvider);

        var result = await handler.Handle(
            new VerifyEmailByTokenCommand(new VerifyEmailByTokenRequest(rawToken)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var updatedAccount = await scope.DbContext.Set<Account>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == account.Id);
        var updatedChallenge = await scope.DbContext.Set<EmailChallenge>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == challenge.Id);

        Assert.True(updatedAccount.IsEmailVerified);
        Assert.Equal(nowUtc, updatedAccount.EmailVerifiedAt);
        Assert.True(updatedChallenge.IsConsumed);
        Assert.Equal(nowUtc, updatedChallenge.ConsumedAt);
    }

    [Fact]
    public async Task VerifyEmailByOtpCommandHandler_MarksAccountVerified_AndConsumesChallenge()
    {
        using var scope = _fixture.CreateScope();

        var account = scope.SeedAccount("handler.verify.otp@finflow.test", "P@ssw0rd!");
        const string rawToken = "verify-otp-token-123";
        const string rawOtp = "654321";
        var nowUtc = scope.Clock.UtcNow;

        var challenge = EmailChallenge.Create(
            account.Id,
            EmailChallengePurpose.VerifyEmail,
            nowUtc.AddMinutes(-5),
            nowUtc.AddMinutes(10),
            email: account.Email,
            tokenHash: scope.SecretService.HashChallengeToken(rawToken),
            otpHash: scope.SecretService.HashChallengeOtp(rawOtp),
            lastSentAtUtc: nowUtc.AddMinutes(-1)).Value;

        scope.DbContext.Add(challenge);
        await scope.SaveSeedAsync();

        var handler = ActivatorUtilities.CreateInstance<VerifyEmailByOtpCommandHandler>(scope.ServiceProvider);

        var result = await handler.Handle(
            new VerifyEmailByOtpCommand(new VerifyEmailByOtpRequest(account.Email, rawOtp)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var updatedAccount = await scope.DbContext.Set<Account>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == account.Id);
        var updatedChallenge = await scope.DbContext.Set<EmailChallenge>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == challenge.Id);

        Assert.True(updatedAccount.IsEmailVerified);
        Assert.Equal(nowUtc, updatedAccount.EmailVerifiedAt);
        Assert.True(updatedChallenge.IsConsumed);
        Assert.Equal(nowUtc, updatedChallenge.ConsumedAt);
    }

    [Fact]
    public async Task ResendEmailVerificationCommandHandler_RotatesChallenge_AndSendsFreshVerificationEmail()
    {
        using var scope = _fixture.CreateScope();

        var account = scope.SeedAccount("handler.resend@finflow.test", "P@ssw0rd!");
        const string oldToken = "resend-old-token";
        const string oldOtp = "111111";
        var nowUtc = scope.Clock.UtcNow;

        var existingChallenge = EmailChallenge.Create(
            account.Id,
            EmailChallengePurpose.VerifyEmail,
            nowUtc.AddMinutes(-10),
            nowUtc.AddMinutes(10),
            email: account.Email,
            tokenHash: scope.SecretService.HashChallengeToken(oldToken),
            otpHash: scope.SecretService.HashChallengeOtp(oldOtp),
            lastSentAtUtc: nowUtc.AddMinutes(-2)).Value;

        scope.DbContext.Add(existingChallenge);
        await scope.SaveSeedAsync();

        var handler = ActivatorUtilities.CreateInstance<ResendEmailVerificationCommandHandler>(scope.ServiceProvider);

        var result = await handler.Handle(
            new ResendEmailVerificationCommand(new ResendEmailVerificationRequest(account.Email)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(90, result.Value.CooldownSeconds);
        Assert.Single(scope.EmailSender.VerificationEmails);

        var challenges = await scope.DbContext.Set<EmailChallenge>()
            .IgnoreQueryFilters()
            .Where(x => x.AccountId == account.Id && x.Purpose == EmailChallengePurpose.VerifyEmail)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, challenges.Count);
        Assert.True(challenges[0].IsRevoked);
        Assert.False(challenges[1].IsRevoked);
        Assert.False(challenges[1].IsConsumed);
        Assert.NotEqual(challenges[0].TokenHash, challenges[1].TokenHash);

        var verificationEmail = scope.EmailSender.VerificationEmails.Single();
        Assert.Equal(account.Email, verificationEmail.Email);
        Assert.Contains("verification-token-1", verificationEmail.VerificationLink);
        Assert.Equal("100001", verificationEmail.Otp);
    }
}
