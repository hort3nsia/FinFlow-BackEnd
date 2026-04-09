using FinFlow.Application.Common;
using FinFlow.Application.Tenant.Responses;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Queries.GetPendingTenantRequests;

public record GetPendingTenantRequestsQuery() : IQuery<Result<IReadOnlyList<PendingTenantApprovalResponse>>>;
