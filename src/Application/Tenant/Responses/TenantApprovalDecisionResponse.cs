using FinFlow.Domain.Enums;

namespace FinFlow.Application.Tenant.Responses;

public record TenantApprovalDecisionResponse(
    Guid RequestId,
    TenantApprovalStatus Status,
    Guid? TenantId,
    string? TenantCode,
    string? Name
);
