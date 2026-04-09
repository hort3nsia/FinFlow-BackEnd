using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string Name,
    string TenantCode,
    string DepartmentName = "Root",
    string? ClientIp = null)
    : ICommand<Result<AuthResponse>>;
