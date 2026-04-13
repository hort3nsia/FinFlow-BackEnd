using FinFlow.Application.Auth.Commands.ResendEmailVerification;
using FinFlow.Application.Auth.Commands.VerifyEmailByOtp;
using FinFlow.Application.Auth.Commands.VerifyEmailByToken;
using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Application.Auth.Validators;

namespace FinFlow.UnitTests.Application.Auth;

public sealed class AuthEmailVerificationValidatorTests
{
    [Fact]
    public void VerifyEmailAndResendValidators_ReturnErrors_ForMissingFields()
    {
        var verifyByTokenValidator = new VerifyEmailByTokenCommandValidator();
        var verifyByOtpValidator = new VerifyEmailByOtpCommandValidator();
        var resendValidator = new ResendEmailVerificationCommandValidator();

        var verifyByTokenResult = verifyByTokenValidator.Validate(
            new VerifyEmailByTokenCommand(new VerifyEmailByTokenRequest("")));
        var verifyByOtpResult = verifyByOtpValidator.Validate(
            new VerifyEmailByOtpCommand(new VerifyEmailByOtpRequest("", "")));
        var resendResult = resendValidator.Validate(
            new ResendEmailVerificationCommand(new ResendEmailVerificationRequest("")));

        Assert.Contains(verifyByTokenResult.Errors, x => x.PropertyName == "Request.Token");
        Assert.Contains(verifyByOtpResult.Errors, x => x.PropertyName == "Request.Email");
        Assert.Contains(verifyByOtpResult.Errors, x => x.PropertyName == "Request.Otp");
        Assert.Contains(resendResult.Errors, x => x.PropertyName == "Request.Email");
    }
}
