using FinFlow.Application.Membership.Commands.InviteMember;
using FluentValidation;

namespace FinFlow.Application.Membership.Validators;

public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        RuleFor(x => x.InviterAccountId).NotEmpty();
        RuleFor(x => x.InviterMembershipId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
