using FinFlow.Application.Auth.DTOs.Responses;
using FinFlow.Application.Common;
using FinFlow.Application.Tenant.DTOs.Requests;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Commands.CreateSharedTenant;

public record CreateSharedTenantCommand(CreateSharedTenantRequest Request)
    : ICommand<Result<AuthResponse>>;
