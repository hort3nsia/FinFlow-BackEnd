using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinFlow.Domain.Entities;

public sealed class EmailChallenge : Entity
{
    private const int DefaultMaxOtpAttempts = 5;

    private EmailChallenge(
        Guid id,
        Guid accountId,
        EmailChallengePurpose purpose,
        string email,
        string tokenHash,
        string? otpHash,
        DateTime createdAtUtc,
        DateTime? lastSentAtUtc,
        DateTime expiresAtUtc,
        int maxOtpAttempts,
        int otpAttemptCount)
    {
        Id = id;
        AccountId = accountId;
        Purpose = purpose;
        Email = email;
        TokenHash = tokenHash;
        OtpHash = otpHash;
        CreatedAt = createdAtUtc;
        LastSentAt = lastSentAtUtc;
        ExpiresAt = expiresAtUtc;
        MaxOtpAttempts = maxOtpAttempts;
        OtpAttemptCount = otpAttemptCount;
    }

    private EmailChallenge() { }

    public string Email { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public EmailChallengePurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string? OtpHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSentAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public int MaxOtpAttempts { get; private set; } = DefaultMaxOtpAttempts;
    public int OtpAttemptCount { get; private set; }

    [NotMapped]
    public int OtpFailedAttemptCount => OtpAttemptCount;

    public bool IsConsumed => ConsumedAt.HasValue;
    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpiredAt(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        return nowUtc >= ExpiresAt;
    }

    public bool IsUsableAt(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        return nowUtc >= CreatedAt && nowUtc < ExpiresAt && !IsRevoked && !IsConsumed;
    }

    public bool CanResendAt(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        return IsUsableAt(nowUtc);
    }

    public static Result<EmailChallenge> Create(
        Guid accountId,
        EmailChallengePurpose purpose,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string email = "",
        string tokenHash = "",
        string? otpHash = null,
        DateTime? lastSentAtUtc = null,
        int maxOtpAttempts = DefaultMaxOtpAttempts,
        int otpAttemptCount = 0)
    {
        if (accountId == Guid.Empty)
            return Result.Failure<EmailChallenge>(EmailChallengeErrors.AccountRequired);

        if (!Enum.IsDefined(typeof(EmailChallengePurpose), purpose))
            return Result.Failure<EmailChallenge>(EmailChallengeErrors.PurposeRequired);

        if (createdAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc)
            return Result.Failure<EmailChallenge>(EmailChallengeErrors.InvalidTimestamp);

        if (lastSentAtUtc.HasValue && lastSentAtUtc.Value.Kind != DateTimeKind.Utc)
            return Result.Failure<EmailChallenge>(EmailChallengeErrors.InvalidTimestamp);

        if (expiresAtUtc <= createdAtUtc)
            return Result.Failure<EmailChallenge>(EmailChallengeErrors.ExpirationRequired);

        var challenge = new EmailChallenge(
            Guid.NewGuid(),
            accountId,
            purpose,
            email.Trim().ToLowerInvariant(),
            tokenHash,
            otpHash,
            createdAtUtc,
            lastSentAtUtc ?? createdAtUtc,
            expiresAtUtc,
            maxOtpAttempts <= 0 ? DefaultMaxOtpAttempts : maxOtpAttempts,
            otpAttemptCount < 0 ? 0 : otpAttemptCount);

        return Result.Success(challenge);
    }

    public Result Consume(DateTime consumedAtUtc)
    {
        if (consumedAtUtc.Kind != DateTimeKind.Utc || consumedAtUtc < CreatedAt)
            return Result.Failure(EmailChallengeErrors.InvalidTimestamp);

        if (IsConsumed)
            return Result.Failure(EmailChallengeErrors.AlreadyConsumed);

        if (IsRevoked)
            return Result.Failure(EmailChallengeErrors.AlreadyRevoked);

        if (IsExpiredAt(consumedAtUtc))
            return Result.Failure(EmailChallengeErrors.Expired);

        ConsumedAt = consumedAtUtc;
        return Result.Success();
    }

    public Result Revoke(DateTime revokedAtUtc)
    {
        if (revokedAtUtc.Kind != DateTimeKind.Utc || revokedAtUtc < CreatedAt)
            return Result.Failure(EmailChallengeErrors.InvalidTimestamp);

        if (IsConsumed)
            return Result.Failure(EmailChallengeErrors.AlreadyConsumed);

        if (IsRevoked)
            return Result.Failure(EmailChallengeErrors.AlreadyRevoked);

        RevokedAt = revokedAtUtc;
        return Result.Success();
    }

    public Result RegisterFailedOtpAttempt(DateTime failedAtUtc)
    {
        if (failedAtUtc.Kind != DateTimeKind.Utc || failedAtUtc < CreatedAt)
            return Result.Failure(EmailChallengeErrors.InvalidTimestamp);

        if (!IsUsableAt(failedAtUtc))
        {
            if (IsConsumed)
                return Result.Failure(EmailChallengeErrors.AlreadyConsumed);

            if (IsRevoked)
                return Result.Failure(EmailChallengeErrors.AlreadyRevoked);

            return Result.Failure(EmailChallengeErrors.Expired);
        }

        OtpAttemptCount++;

        if (OtpAttemptCount >= MaxOtpAttempts)
            RevokedAt = failedAtUtc;

        return Result.Success();
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC", nameof(value));
    }
}
