using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Membership.Commands.SwitchWorkspace;

public record SwitchWorkspaceCommand(Guid AccountId, Guid MembershipId, string CurrentRefreshToken)
    : ICommand<Result<AuthResponse>>;
