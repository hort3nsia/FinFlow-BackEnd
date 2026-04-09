using FinFlow.Domain.Enums;

namespace FinFlow.Application.Tenant.DTOs.Responses;

public record PendingTenantApprovalResponse(
    Guid RequestId,
    string TenantCode,
    string Name,
    string CompanyName,
    string TaxCode,
    string? RequestedByEmail,
    int? EmployeeCount,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    TenantApprovalStatus Status
);
