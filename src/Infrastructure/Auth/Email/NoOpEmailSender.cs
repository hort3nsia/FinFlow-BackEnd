using FinFlow.Application.Common.Abstractions;

namespace FinFlow.Infrastructure.Auth.Email;

internal sealed class NoOpEmailSender : IEmailSender
{
    public Task SendVerificationEmailAsync(string email, string verificationLink, string otp, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("NoOpEmailSender is for tests only and must not be used as the runtime email sender.");

    public Task SendPasswordResetEmailAsync(string email, string resetLink, string otp, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("NoOpEmailSender is for tests only and must not be used as the runtime email sender.");
}
