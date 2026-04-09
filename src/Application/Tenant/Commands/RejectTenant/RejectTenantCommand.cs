using FinFlow.Application.Common;
using FinFlow.Application.Tenant.Responses;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Commands.RejectTenant;

public record RejectTenantCommand(Guid RequestId, string Reason) : ICommand<Result<TenantApprovalDecisionResponse>>;
