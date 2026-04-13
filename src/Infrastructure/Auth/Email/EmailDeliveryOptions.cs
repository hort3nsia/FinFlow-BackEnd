namespace FinFlow.Infrastructure.Auth.Email;

public sealed class EmailDeliveryOptions
{
    public string SenderAddress { get; set; } = "no-reply@example.com";
    public string SenderName { get; set; } = "FinFlow";
    public string VerificationSubject { get; set; } = "Verify your email";
    public string PasswordResetSubject { get; set; } = "Reset your password";
}
