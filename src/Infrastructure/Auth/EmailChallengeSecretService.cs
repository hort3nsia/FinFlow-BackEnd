using System.Security.Cryptography;
using System.Text;
using FinFlow.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace FinFlow.Infrastructure.Auth.Email;

public sealed class EmailChallengeSecretService : IEmailChallengeSecretService
{
    private readonly AuthChallengeOptions _options;

    public EmailChallengeSecretService(IOptions<AuthChallengeOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateVerificationToken()
    {
        var bytes = new byte[Math.Max(16, _options.TokenByteLength)];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string GenerateVerificationOtp()
    {
        var length = _options.OtpLength <= 0 ? 6 : _options.OtpLength;
        var maxExclusive = (int)Math.Pow(10, length);
        var otp = RandomNumberGenerator.GetInt32(maxExclusive);
        return otp.ToString($"D{length}");
    }

    public string HashChallengeToken(string token) => HashWithSecret(token);

    public string HashChallengeOtp(string otp) => HashWithSecret(otp);

    private string HashWithSecret(string value)
    {
        var key = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(_options.TokenHashKey)
            ? "finflow-email-challenge-secret"
            : _options.TokenHashKey);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
