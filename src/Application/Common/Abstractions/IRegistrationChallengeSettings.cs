namespace FinFlow.Application.Common.Abstractions;

public interface IRegistrationChallengeSettings
{
    int VerificationTokenLifetimeMinutes { get; }
    int VerificationCooldownSeconds { get; }
    string VerificationLinkBaseUrl { get; }
}
