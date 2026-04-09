using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Commands.CreateSharedTenant;

public record CreateSharedTenantCommand(
    Guid AccountId,
    Guid? CurrentMembershipId,
    string Name,
    string TenantCode,
    string Currency = "VND")
    : ICommand<Result<AuthResponse>>;
