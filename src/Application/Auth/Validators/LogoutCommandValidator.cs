using FinFlow.Application.Auth.Commands.Logout;
using FluentValidation;

namespace FinFlow.Application.Auth.Validators;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
