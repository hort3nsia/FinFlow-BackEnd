using FinFlow.Domain.Enums;

namespace FinFlow.Application.Tenant.DTOs.Responses;

public record TenantApprovalResponse(
    Guid RequestId,
    TenantApprovalStatus Status,
    string Message,
    DateTime ExpiresAt
);
