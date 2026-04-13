namespace FinFlow.Application.Common.Abstractions;

public interface IEmailChallengeSecretService
{
    string GenerateVerificationToken();
    string GenerateVerificationOtp();
    string HashChallengeToken(string token);
    string HashChallengeOtp(string otp);
}
