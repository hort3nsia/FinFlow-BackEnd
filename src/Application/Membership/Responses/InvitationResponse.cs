using FinFlow.Domain.Enums;

namespace FinFlow.Application.Membership.Responses;

public record InvitationResponse(
    Guid InvitationId,
    string InviteToken,
    string Email,
    RoleType Role,
    Guid IdTenant,
    DateTime ExpiresAt
);
