using FinFlow.Application.Common.Abstractions;

namespace FinFlow.Infrastructure.Auth.Email;

public sealed class AuthChallengeOptions : IRegistrationChallengeSettings
{
    public int VerificationTokenLifetimeMinutes { get; set; } = 15;
    public int VerificationCooldownSeconds { get; set; } = 90;
    public int OtpLength { get; set; } = 6;
    public int TokenByteLength { get; set; } = 32;
    public string TokenHashKey { get; set; } = string.Empty;
    public string VerificationLinkBaseUrl { get; set; } = string.Empty;
}
