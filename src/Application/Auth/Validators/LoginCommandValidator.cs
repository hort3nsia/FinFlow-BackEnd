using FinFlow.Application.Auth.Commands.Login;
using FluentValidation;

namespace FinFlow.Application.Auth.Validators;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.TenantCode).NotEmpty();
    }
}
