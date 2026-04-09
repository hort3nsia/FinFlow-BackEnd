using FinFlow.Application.Membership.Commands.AcceptInvite;
using FluentValidation;

namespace FinFlow.Application.Membership.Validators;

public sealed class AcceptInviteCommandValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteCommandValidator()
    {
        RuleFor(x => x.InviteToken).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
