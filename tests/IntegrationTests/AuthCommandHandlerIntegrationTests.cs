using FinFlow.Application.Auth.Commands.ChangePassword;
using FinFlow.Application.Auth.Commands.Login;
using FinFlow.Application.Auth.Commands.Logout;
using FinFlow.Application.Auth.Commands.RefreshToken;
using FinFlow.Application.Auth.Commands.Register;
using FinFlow.Application.Auth.Commands.ResendEmailVerification;
using FinFlow.Application.Auth.Commands.VerifyEmailByOtp;
using FinFlow.Application.Auth.Commands.VerifyEmailByToken;
using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.IntegrationTests;

public sealed class AuthCommandHandlerIntegrationTests
{
    private readonly AuthFlowTestFixture _fixture = new();

    [Fact]
    public async Task LoginCommandHandler_ReturnsAccountSession_ForValidCredentialsWithoutWorkspaceState()
    {
        using var scope = _fixture.CreateScope();

        var account = scope.SeedAccount("handler.login@finflow.test", "P@ssw0rd!");

        await scope.SaveSeedAsync();

        var handler = scope.CreateLoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest(account.Email, "P@ssw0rd!", "127.0.0.1")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(account.Email, result.Value.Email);
        Assert.Equal(account.Id, result.Value.Id);
        Assert.Equal("account", result.Value.SessionKind);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));

        Assert.Equal(0, await scope.DbContext.Set<Tenant>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await scope.DbContext.Set<Department>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await scope.DbContext.Set<TenantMembership>().IgnoreQueryFilters().CountAsync());

        var refreshToken = await scope.DbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.AccountId == account.Id);

        Assert.Equal(account.Id, refreshToken.AccountId);
    }

    [Fact]
    public async Task RegisterCommandHandler_ReturnsVerificationPendingPayload_AndSendsVerificationEmail()
    {
        using var scope = _fixture.CreateScope();
        var handler = scope.CreateRegisterHandler();

        var result = await handler.Handle(
            new RegisterCommand(new RegisterRequest("handler.register@finflow.test", "P@ssw0rd!", "FinFlow Team", "127.0.0.1")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("handler.register@finflow.test", result.Value.Email);
        Assert.True(result.Value.RequiresEmailVerification);
        Assert.Equal(90, result.Value.CooldownSeconds);

        var account = await scope.DbContext.Set<Account>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Email == "handler.register@finflow.test");
        var challenge = await scope.DbContext.Set<EmailChallenge>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.AccountId == account.Id && x.Purpose == EmailChallengePurpose.VerifyEmail);

        Assert.Equal(account.Id, result.Value.AccountId);
        Assert.False(account.IsEmailVerified);
        Assert.Equal(account.Email, challenge.Email);
        Assert.Equal("token-hash:verification-token-1", challenge.TokenHash);
        Assert.Equal("otp-hash:100001", challenge.OtpHash);
        Assert.Equal(scope.Clock.UtcNow, challenge.CreatedAt);
        Assert.Equal(scope.Clock.UtcNow, challenge.LastSentAt);
        Assert.Equal(scope.Clock.UtcNow.AddMinutes(15), challenge.ExpiresAt);
        Assert.Single(scope.EmailSender.VerificationEmails);

        var verificationEmail = scope.EmailSender.VerificationEmails.Single();
        Assert.Equal(account.Email, verificationEmail.Email);
        Assert.Contains("verification-token-1", verificationEmail.VerificationLink);
        Assert.Equal("100001", verificationEmail.Otp);
        Assert.Equal(0, await scope.DbContext.Set<Tenant>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await scope.DbContext.Set<Department>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await scope.DbContext.Set<TenantMembership>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await scope.DbContext.Set<RefreshToken>().IgnoreQueryFilters().CountAsync());
    }

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

        var handler = scope.CreateVerifyEmailByTokenHandler();

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

        var handler = scope.CreateVerifyEmailByOtpHandler();

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

        var handler = scope.CreateResendEmailVerificationHandler();

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

    [Fact]
    public async Task RefreshTokenCommandHandler_RotatesToken_ForActiveAccount()
    {
        using var scope = _fixture.CreateScope();

        var tenant = scope.SeedTenant("Workspace", "handler-refresh");
        var department = scope.SeedDepartment("Root", tenant.Id);
        var account = scope.SeedAccount("handler.refresh@finflow.test", "P@ssw0rd!");
        var membership = scope.SeedMembership(account.Id, tenant.Id, RoleType.TenantAdmin);
        const string rawRefreshToken = "handler-refresh-token";
        scope.SeedRefreshToken(rawRefreshToken, account.Id, membership.Id);

        await scope.SaveSeedAsync();

        var handler = scope.CreateRefreshTokenHandler();

        var result = await handler.Handle(new RefreshTokenCommand(new RefreshTokenRequest(rawRefreshToken)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(membership.Id, result.Value.MembershipId);
        Assert.NotEqual(rawRefreshToken, result.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenCommandHandler_RotatesAccountScopedToken_ForAccountSession()
    {
        using var scope = _fixture.CreateScope();

        var account = scope.SeedAccount("handler.account-refresh@finflow.test", "P@ssw0rd!");
        const string rawRefreshToken = "handler-account-refresh-token";
        scope.SeedAccountRefreshToken(rawRefreshToken, account.Id);

        await scope.SaveSeedAsync();

        var handler = scope.CreateRefreshTokenHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(new RefreshTokenRequest(rawRefreshToken)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(account.Id, result.Value.Id);
        Assert.Equal(account.Email, result.Value.Email);
        Assert.Equal("account", result.Value.SessionKind);
        Assert.Null(result.Value.MembershipId);
        Assert.Null(result.Value.Role);
        Assert.Null(result.Value.IdTenant);
        Assert.NotEqual(rawRefreshToken, result.Value.RefreshToken);

        var refreshTokens = await scope.DbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(x => x.AccountId == account.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, refreshTokens.Count);
        Assert.True(refreshTokens[0].IsRevoked);
        Assert.Null(refreshTokens[0].MembershipId);
        Assert.True(refreshTokens[1].IsActive);
        Assert.Null(refreshTokens[1].MembershipId);
    }

    [Fact]
    public async Task RefreshTokenCommandHandler_DoesNotPersistReplacement_WhenWorkspaceMembershipIsInactive()
    {
        using var scope = _fixture.CreateScope();

        var tenant = scope.SeedTenant("Workspace", "handler-refresh-inactive-membership");
        var account = scope.SeedAccount("handler.refresh.inactive@finflow.test", "P@ssw0rd!");
        var membership = scope.SeedMembership(account.Id, tenant.Id, RoleType.TenantAdmin);
        const string rawRefreshToken = "handler-refresh-inactive-membership-token";
        scope.SeedRefreshToken(rawRefreshToken, account.Id, membership.Id);

        await scope.SaveSeedAsync();

        var membershipEntity = await scope.DbContext.Set<TenantMembership>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == membership.Id);
        membershipEntity.Deactivate();
        scope.CurrentTenant.Id = tenant.Id;
        scope.CurrentTenant.MembershipId = membership.Id;
        await scope.DbContext.SaveChangesAsync();
        scope.CurrentTenant.Id = null;
        scope.CurrentTenant.MembershipId = null;
        scope.DbContext.ChangeTracker.Clear();

        var handler = scope.CreateRefreshTokenHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(new RefreshTokenRequest(rawRefreshToken)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantMembershipErrors.NotFound.Code, result.Error.Code);

        var refreshTokens = await scope.DbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(x => x.AccountId == account.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Single(refreshTokens);
        Assert.True(refreshTokens[0].IsActive);
        Assert.False(refreshTokens[0].IsRevoked);
        Assert.Equal(membership.Id, refreshTokens[0].MembershipId);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_RevokesAllRefreshTokens()
    {
        using var scope = _fixture.CreateScope();

        var tenant = scope.SeedTenant("Workspace", "handler-change-password");
        var department = scope.SeedDepartment("Root", tenant.Id);
        var account = scope.SeedAccount("handler.password@finflow.test", "P@ssw0rd!");
        var membership = scope.SeedMembership(account.Id, tenant.Id, RoleType.TenantAdmin);
        scope.SeedRefreshToken("handler-password-token-1", account.Id, membership.Id);
        scope.SeedRefreshToken("handler-password-token-2", account.Id, membership.Id);

        await scope.SaveSeedAsync();

        var handler = scope.CreateChangePasswordHandler();

        var result = await handler.Handle(
            new ChangePasswordCommand(new ChangePasswordRequest(account.Id, "P@ssw0rd!", "N3wP@ssword!")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var refreshTokens = await scope.DbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(x => x.AccountId == account.Id)
            .ToListAsync();

        Assert.All(refreshTokens, token =>
        {
            Assert.True(token.IsRevoked);
            Assert.Equal("Password changed", token.ReasonRevoked);
        });
    }

    [Fact]
    public async Task LogoutCommandHandler_RevokesRefreshToken()
    {
        using var scope = _fixture.CreateScope();

        var tenant = scope.SeedTenant("Workspace", "handler-logout");
        var department = scope.SeedDepartment("Root", tenant.Id);
        var account = scope.SeedAccount("handler.logout@finflow.test", "P@ssw0rd!");
        var membership = scope.SeedMembership(account.Id, tenant.Id, RoleType.TenantAdmin);
        const string rawRefreshToken = "handler-logout-token";
        scope.SeedRefreshToken(rawRefreshToken, account.Id, membership.Id);

        await scope.SaveSeedAsync();

        var handler = scope.CreateLogoutHandler();

        var result = await handler.Handle(new LogoutCommand(new LogoutRequest(rawRefreshToken)), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var token = await scope.DbContext.Set<RefreshToken>()
            .SingleAsync(x => x.AccountId == account.Id);

        Assert.True(token.IsRevoked);
        Assert.Equal("User logout", token.ReasonRevoked);
    }
}
