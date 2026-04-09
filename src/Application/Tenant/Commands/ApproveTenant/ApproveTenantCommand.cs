using FinFlow.Application.Common;
using FinFlow.Application.Tenant.Responses;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Commands.ApproveTenant;

public record ApproveTenantCommand(Guid RequestId) : ICommand<Result<TenantApprovalDecisionResponse>>;
