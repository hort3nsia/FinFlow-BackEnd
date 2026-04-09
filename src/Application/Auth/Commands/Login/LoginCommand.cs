using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password, string TenantCode, string? ClientIp = null)
    : ICommand<Result<AuthResponse>>;
