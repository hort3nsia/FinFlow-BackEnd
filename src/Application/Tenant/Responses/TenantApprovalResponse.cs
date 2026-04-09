using FinFlow.Domain.Enums;

namespace FinFlow.Application.Tenant.Responses;

public record TenantApprovalResponse(
    Guid RequestId,
    TenantApprovalStatus Status,
    string Message,
    DateTime ExpiresAt
);
