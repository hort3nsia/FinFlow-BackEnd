using FinFlow.Domain.Abstractions;

namespace FinFlow.Domain.Entities;

public static class EmailChallengeErrors
{
    public static readonly Error AccountRequired = new("EmailChallenge.AccountRequired", "Account ID is required");
    public static readonly Error PurposeRequired = new("EmailChallenge.PurposeRequired", "Purpose is required");
    public static readonly Error ExpirationRequired = new("EmailChallenge.ExpirationRequired", "Expiration must be in the future");
    public static readonly Error AlreadyConsumed = new("EmailChallenge.AlreadyConsumed", "The email challenge has already been consumed");
    public static readonly Error AlreadyRevoked = new("EmailChallenge.AlreadyRevoked", "The email challenge has already been revoked");
    public static readonly Error Expired = new("EmailChallenge.Expired", "The email challenge has expired");
    public static readonly Error InvalidTimestamp = new("EmailChallenge.InvalidTimestamp", "The timestamp must be UTC and not earlier than challenge creation");
    public static readonly Error VerificationLinkBaseUrlRequired = new("EmailChallenge.VerificationLinkBaseUrlRequired", "Verification link base URL is required");
    public static readonly Error EmailDeliveryFailed = new("EmailChallenge.EmailDeliveryFailed", "Unable to deliver verification email");
    public static readonly Error InvalidToken = new("EmailChallenge.InvalidToken", "The verification token is invalid");
    public static readonly Error InvalidOtp = new("EmailChallenge.InvalidOtp", "The verification code is invalid");
}
