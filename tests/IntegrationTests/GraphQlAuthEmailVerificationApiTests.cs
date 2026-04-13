using System.Text.Json;
using FinFlow.Application.Common.Abstractions;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinFlow.IntegrationTests;

public sealed class GraphQlAuthEmailVerificationApiTests
{
    [Fact]
    public async Task VerifyEmailByToken_Mutation_VerifiesAccount_AndConsumesChallenge()
    {
        await using var factory = new GraphQlApiTestFactory();
        using var secretScope = factory.Services.CreateScope();
        var secretService = secretScope.ServiceProvider.GetRequiredService<IEmailChallengeSecretService>();

        var account = Account.Create("graphql.verify.token@finflow.test", BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!")).Value;
        const string rawToken = "graphql-verify-token";
        const string rawOtp = "654321";
        var nowUtc = DateTime.UtcNow;

        var challenge = EmailChallenge.Create(
            account.Id,
            EmailChallengePurpose.VerifyEmail,
            nowUtc.AddMinutes(-5),
            nowUtc.AddMinutes(15),
            email: account.Email,
            tokenHash: secretService.HashChallengeToken(rawToken),
            otpHash: secretService.HashChallengeOtp(rawOtp),
            lastSentAtUtc: nowUtc.AddMinutes(-2)).Value;

        await factory.SeedAsync(db =>
        {
            db.Add(account);
            db.Add(challenge);
        });

        const string mutation = """
            mutation($token: String!) {
              verifyEmailByToken(token: $token)
            }
            """;

        using var json = await GraphQlApiTestFactory.PostGraphQlAsync(factory.CreateClient(), mutation, new { token = rawToken });

        Assert.False(json.RootElement.TryGetProperty("errors", out _), json.RootElement.ToString());
        Assert.True(json.RootElement.GetProperty("data").GetProperty("verifyEmailByToken").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var verifiedAccount = await dbContext.Set<Account>().IgnoreQueryFilters().SingleAsync(x => x.Email == account.Email);
        var consumedChallenge = await dbContext.Set<EmailChallenge>().IgnoreQueryFilters().SingleAsync(x => x.AccountId == account.Id);

        Assert.True(verifiedAccount.IsEmailVerified);
        Assert.True(consumedChallenge.IsConsumed);
    }

    [Fact]
    public async Task VerifyEmailByOtp_Mutation_VerifiesAccount_AndConsumesChallenge()
    {
        await using var factory = new GraphQlApiTestFactory();
        using var secretScope = factory.Services.CreateScope();
        var secretService = secretScope.ServiceProvider.GetRequiredService<IEmailChallengeSecretService>();

        var account = Account.Create("graphql.verify.otp@finflow.test", BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!")).Value;
        const string rawToken = "graphql-verify-otp-token";
        const string rawOtp = "123456";
        var nowUtc = DateTime.UtcNow;

        var challenge = EmailChallenge.Create(
            account.Id,
            EmailChallengePurpose.VerifyEmail,
            nowUtc.AddMinutes(-5),
            nowUtc.AddMinutes(15),
            email: account.Email,
            tokenHash: secretService.HashChallengeToken(rawToken),
            otpHash: secretService.HashChallengeOtp(rawOtp),
            lastSentAtUtc: nowUtc.AddMinutes(-2)).Value;

        await factory.SeedAsync(db =>
        {
            db.Add(account);
            db.Add(challenge);
        });

        const string mutation = """
            mutation($email: String!, $otp: String!) {
              verifyEmailByOtp(email: $email, otp: $otp)
            }
            """;

        using var json = await GraphQlApiTestFactory.PostGraphQlAsync(factory.CreateClient(), mutation, new
        {
            email = account.Email,
            otp = rawOtp
        });

        Assert.False(json.RootElement.TryGetProperty("errors", out _), json.RootElement.ToString());
        Assert.True(json.RootElement.GetProperty("data").GetProperty("verifyEmailByOtp").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var verifiedAccount = await dbContext.Set<Account>().IgnoreQueryFilters().SingleAsync(x => x.Email == account.Email);
        var consumedChallenge = await dbContext.Set<EmailChallenge>().IgnoreQueryFilters().SingleAsync(x => x.AccountId == account.Id);

        Assert.True(verifiedAccount.IsEmailVerified);
        Assert.True(consumedChallenge.IsConsumed);
    }

    [Fact]
    public async Task ResendEmailVerification_Mutation_RotatesAndSendsChallenge()
    {
        await using var factory = new GraphQlApiTestFactory();
        using var secretScope = factory.Services.CreateScope();
        var secretService = secretScope.ServiceProvider.GetRequiredService<IEmailChallengeSecretService>();

        var account = Account.Create("graphql.resend@finflow.test", BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!")).Value;
        const string rawToken = "graphql-resend-old-token";
        const string rawOtp = "222222";
        var nowUtc = DateTime.UtcNow;

        var existingChallenge = EmailChallenge.Create(
            account.Id,
            EmailChallengePurpose.VerifyEmail,
            nowUtc.AddMinutes(-10),
            nowUtc.AddMinutes(15),
            email: account.Email,
            tokenHash: secretService.HashChallengeToken(rawToken),
            otpHash: secretService.HashChallengeOtp(rawOtp),
            lastSentAtUtc: nowUtc.AddMinutes(-2)).Value;

        await factory.SeedAsync(db =>
        {
            db.Add(account);
            db.Add(existingChallenge);
        });

        const string mutation = """
            mutation($email: String!) {
              resendEmailVerification(email: $email) {
                accepted
                cooldownSeconds
              }
            }
            """;

        using var json = await GraphQlApiTestFactory.PostGraphQlAsync(factory.CreateClient(), mutation, new { email = account.Email });

        Assert.False(json.RootElement.TryGetProperty("errors", out _), json.RootElement.ToString());
        var payload = json.RootElement.GetProperty("data").GetProperty("resendEmailVerification");
        Assert.True(payload.GetProperty("accepted").GetBoolean());
        Assert.Equal(90, payload.GetProperty("cooldownSeconds").GetInt32());
        Assert.Single(factory.EmailSender.VerificationEmails);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var challenges = await dbContext.Set<EmailChallenge>()
            .IgnoreQueryFilters()
            .Where(x => x.AccountId == account.Id && x.Purpose == EmailChallengePurpose.VerifyEmail)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, challenges.Count);
        Assert.True(challenges[0].IsRevoked);
        Assert.False(challenges[1].IsRevoked);
    }
}
