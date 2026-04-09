namespace FinFlow.Application.Tenant.DTOs.Requests;

public record CreateIsolatedTenantRequest(
    Guid AccountId,
    Guid? CurrentMembershipId,
    string Name,
    string TenantCode,
    string Currency,
    CompanyInfoRequest? CompanyInfo);
