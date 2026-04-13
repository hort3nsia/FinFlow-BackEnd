using System.Net;
using System.Net.Mail;
using FinFlow.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace FinFlow.Infrastructure.Auth.Email;

internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailSenderOptions _smtpOptions;
    private readonly EmailDeliveryOptions _deliveryOptions;

    public SmtpEmailSender(
        IOptions<SmtpEmailSenderOptions> smtpOptions,
        IOptions<EmailDeliveryOptions> deliveryOptions)
    {
        _smtpOptions = smtpOptions.Value;
        _deliveryOptions = deliveryOptions.Value;
    }

    public Task SendVerificationEmailAsync(string email, string verificationLink, string otp, CancellationToken cancellationToken = default)
    {
        var subject = string.IsNullOrWhiteSpace(_deliveryOptions.VerificationSubject)
            ? "Verify your email"
            : _deliveryOptions.VerificationSubject;

        var body = $"""
            FinFlow email verification

            Verification link:
            {verificationLink}

            OTP fallback:
            {otp}
            """;

        return SendAsync(email, subject, body, cancellationToken);
    }

    public Task SendPasswordResetEmailAsync(string email, string resetLink, string otp, CancellationToken cancellationToken = default)
    {
        var subject = string.IsNullOrWhiteSpace(_deliveryOptions.PasswordResetSubject)
            ? "Reset your password"
            : _deliveryOptions.PasswordResetSubject;

        var body = $"""
            FinFlow password reset

            Reset link:
            {resetLink}

            OTP fallback:
            {otp}
            """;

        return SendAsync(email, subject, body, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        using var message = new MailMessage
        {
            From = new MailAddress(_deliveryOptions.SenderAddress, _deliveryOptions.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.UseTls,
            Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
            throw new InvalidOperationException("EmailSmtp:Host is required.");
        if (_smtpOptions.Port <= 0)
            throw new InvalidOperationException("EmailSmtp:Port must be a positive number.");
        if (string.IsNullOrWhiteSpace(_smtpOptions.Username))
            throw new InvalidOperationException("EmailSmtp:Username is required.");
        if (string.IsNullOrWhiteSpace(_smtpOptions.Password))
            throw new InvalidOperationException("EmailSmtp:Password is required.");
        if (string.IsNullOrWhiteSpace(_deliveryOptions.SenderAddress))
            throw new InvalidOperationException("EmailDelivery:SenderAddress is required.");
        if (string.IsNullOrWhiteSpace(_deliveryOptions.SenderName))
            throw new InvalidOperationException("EmailDelivery:SenderName is required.");
    }
}
