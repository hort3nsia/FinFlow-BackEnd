namespace FinFlow.Application.Tenant.DTOs.Requests;

public record CreateSharedTenantRequest(
    Guid AccountId,
    Guid? CurrentMembershipId,
    string Name,
    string TenantCode,
    string Currency = "VND");
