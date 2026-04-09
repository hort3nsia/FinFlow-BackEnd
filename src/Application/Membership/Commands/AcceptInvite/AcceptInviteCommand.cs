using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Membership.Commands.AcceptInvite;

public record AcceptInviteCommand(string InviteToken, string Password, string? ClientIp = null)
    : ICommand<Result<AuthResponse>>;
