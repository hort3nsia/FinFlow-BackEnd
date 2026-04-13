using FinFlow.Application.Auth.Commands.ResendEmailVerification;
using FluentValidation;

namespace FinFlow.Application.Auth.Validators;

public sealed class ResendEmailVerificationCommandValidator : AbstractValidator<ResendEmailVerificationCommand>
{
    public ResendEmailVerificationCommandValidator()
    {
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
    }
}
