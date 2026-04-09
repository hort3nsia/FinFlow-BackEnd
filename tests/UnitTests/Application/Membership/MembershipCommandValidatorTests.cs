using FinFlow.Application.Membership.Commands.AcceptInvite;
using FinFlow.Application.Membership.Commands.InviteMember;
using FinFlow.Application.Membership.Commands.SwitchWorkspace;
using FinFlow.Application.Membership.Validators;
using FinFlow.Domain.Enums;

namespace FinFlow.UnitTests.Application.Membership;

public sealed class MembershipCommandValidatorTests
{
    [Fact]
    public void SwitchWorkspaceCommandValidator_ReturnsErrors_ForMissingFields()
    {
        var validator = new SwitchWorkspaceCommandValidator();
        var command = new SwitchWorkspaceCommand(Guid.Empty, Guid.Empty, "");

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SwitchWorkspaceCommand.AccountId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SwitchWorkspaceCommand.MembershipId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SwitchWorkspaceCommand.CurrentRefreshToken));
    }

    [Fact]
    public void InviteMemberCommandValidator_ReturnsErrors_ForInvalidEmailAndMissingIds()
    {
        var validator = new InviteMemberCommandValidator();
        var command = new InviteMemberCommand(Guid.Empty, Guid.Empty, "invalid", RoleType.Staff);

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(InviteMemberCommand.InviterAccountId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(InviteMemberCommand.InviterMembershipId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(InviteMemberCommand.Email));
    }

    [Fact]
    public void AcceptInviteCommandValidator_ReturnsErrors_ForMissingFields()
    {
        var validator = new AcceptInviteCommandValidator();
        var command = new AcceptInviteCommand("", "", null);

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(AcceptInviteCommand.InviteToken));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(AcceptInviteCommand.Password));
    }
}
