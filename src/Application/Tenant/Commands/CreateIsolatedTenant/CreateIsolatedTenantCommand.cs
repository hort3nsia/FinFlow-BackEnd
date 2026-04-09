using FinFlow.Application.Common;
using FinFlow.Application.Tenant.Responses;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;

public record CreateIsolatedTenantCommand(
    Guid AccountId,
    Guid? CurrentMembershipId,
    string Name,
    string TenantCode,
    string Currency,
    CompanyInfoModel CompanyInfo)
    : ICommand<Result<TenantApprovalResponse>>;
