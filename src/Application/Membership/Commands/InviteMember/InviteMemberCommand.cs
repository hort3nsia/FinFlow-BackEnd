using FinFlow.Application.Common;
using FinFlow.Application.Membership.Responses;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Enums;

namespace FinFlow.Application.Membership.Commands.InviteMember;

public record InviteMemberCommand(Guid InviterAccountId, Guid InviterMembershipId, string Email, RoleType Role)
    : ICommand<Result<InvitationResponse>>;
