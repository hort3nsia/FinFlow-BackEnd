namespace FinFlow.Application.Auth.Dtos;

public record LoginRequest(string Email, string Password, string TenantCode);
public record RegisterRequest(string Email, string Password, string Name, string TenantCode, string DepartmentName = "Root");
public record CreateSharedTenantRequest(Guid AccountId, Guid? CurrentMembershipId, string Name, string TenantCode, string Currency = "VND");
public record CompanyInfoRequest(
    string CompanyName,
    string TaxCode,
    string? Address = null,
    string? Phone = null,
    string? ContactPerson = null,
    string? BusinessType = null,
    int? EmployeeCount = null);
public record CreateIsolatedTenantRequest(
    Guid AccountId,
    Guid? CurrentMembershipId,
    string Name,
    string TenantCode,
    string Currency,
    CompanyInfoRequest CompanyInfo);
public record RefreshTokenRequest(string RefreshToken);
public record SwitchWorkspaceRequest(Guid AccountId, Guid MembershipId, string CurrentRefreshToken);
public record InviteMemberRequest(Guid InviterAccountId, Guid InviterMembershipId, string Email, FinFlow.Domain.Enums.RoleType Role);
public record AcceptInviteRequest(string InviteToken, string Password, string? ClientIp = null);
public record ChangePasswordRequest(Guid AccountId, string CurrentPassword, string NewPassword);
