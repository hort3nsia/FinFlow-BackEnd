using FinFlow.Application.Common;
using FinFlow.Application.Tenant.DTOs.Requests;
using FinFlow.Application.Tenant.DTOs.Responses;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Commands.ApproveTenant;

public record ApproveTenantCommand(ApproveTenantRequest Request) : ICommand<Result<TenantApprovalDecisionResponse>>;
