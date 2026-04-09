using FinFlow.Domain.Enums;

namespace FinFlow.Application.Tenant.DTOs.Responses;

public record TenantApprovalDecisionResponse(
    Guid RequestId,
    TenantApprovalStatus Status,
    Guid? TenantId,
    string? TenantCode,
    string? Name
);
