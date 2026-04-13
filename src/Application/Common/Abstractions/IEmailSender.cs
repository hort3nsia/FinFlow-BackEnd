namespace FinFlow.Application.Common.Abstractions;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string email, string verificationLink, string otp, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string email, string resetLink, string otp, CancellationToken cancellationToken = default);
}
