using FinFlow.Application.Auth.Commands.RefreshToken;
using FluentValidation;

namespace FinFlow.Application.Auth.Validators;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
