using FinFlow.Application.Membership.Commands.SwitchWorkspace;
using FluentValidation;

namespace FinFlow.Application.Membership.Validators;

public sealed class SwitchWorkspaceCommandValidator : AbstractValidator<SwitchWorkspaceCommand>
{
    public SwitchWorkspaceCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.CurrentRefreshToken).NotEmpty();
    }
}
