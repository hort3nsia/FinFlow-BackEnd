using FinFlow.Application.Auth.Support;
using FinFlow.Application.Common.Abstractions;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Accounts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.PasswordResetChallenges;

namespace FinFlow.Application.Auth.Commands.CheckPasswordResetOtp;

public sealed class CheckPasswordResetOtpCommandHandler : MediatR.IRequestHandler<CheckPasswordResetOtpCommand, Result>
{
    private readonly IPasswordResetChallengeRepository _challengeRepository;
    private readonly IPasswordResetChallengeSecretService _secretService;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoginRateLimiter _rateLimiter;

    public CheckPasswordResetOtpCommandHandler(
        IPasswordResetChallengeRepository challengeRepository,
        IPasswordResetChallengeSecretService secretService,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILoginRateLimiter rateLimiter)
    {
        _challengeRepository = challengeRepository;
        _secretService = secretService;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _rateLimiter = rateLimiter;
    }

    public async Task<Result> Handle(CheckPasswordResetOtpCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Rate limit checks to prevent brute force
        if (await _rateLimiter.IsBlockedAsync(null, request.Email))
            return Result.Failure(AccountErrors.TooManyRequests);

        var accountInfo = await _accountRepository.GetLoginInfoByEmailAsync(request.Email, cancellationToken);
        if (accountInfo is null || !accountInfo.IsActive)
        {
            await _rateLimiter.RecordFailureAsync(null, request.Email);
            return Result.Failure(PasswordResetChallengeErrors.InvalidOtp);
        }

        var challenge = await _challengeRepository.GetLatestByAccountIdForUpdateAsync(accountInfo.Id, cancellationToken);
        if (challenge is null)
            return Result.Failure(PasswordResetChallengeErrors.InvalidOtp);

        var canConsume = challenge.EnsureCanBeConsumed(DateTime.UtcNow);
        if (canConsume.IsFailure)
            return canConsume;

        var otpHash = _secretService.HashOtp(request.Otp);
        if (!string.Equals(challenge.OtpHash, otpHash, StringComparison.Ordinal))
        {
            await _rateLimiter.RecordFailureAsync(null, request.Email);

            var failedAttemptResult = challenge.RegisterFailedOtpAttempt(DateTime.UtcNow);
            if (failedAttemptResult.IsFailure)
                return failedAttemptResult;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure(PasswordResetChallengeErrors.InvalidOtp);
        }

        // OTP is correct! Do not consume it yet.
        return Result.Success();
    }
}
