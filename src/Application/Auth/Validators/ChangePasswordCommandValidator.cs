using FinFlow.Application.Auth.Commands.ChangePassword;
using FluentValidation;

namespace FinFlow.Application.Auth.Validators;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
