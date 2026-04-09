using FinFlow.Application.Tenant.DTOs.Responses;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Accounts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Domain.TenantApprovals;

namespace FinFlow.Application.Tenant.Queries.GetPendingTenantRequests;

public sealed class GetPendingTenantRequestsQueryHandler : MediatR.IRequestHandler<GetPendingTenantRequestsQuery, Result<IReadOnlyList<PendingTenantApprovalResponse>>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantApprovalRequestRepository _tenantApprovalRequestRepository;
    private readonly IAccountRepository _accountRepository;

    public GetPendingTenantRequestsQueryHandler(
        ICurrentTenant currentTenant,
        ITenantApprovalRequestRepository tenantApprovalRequestRepository,
        IAccountRepository accountRepository)
    {
        _currentTenant = currentTenant;
        _tenantApprovalRequestRepository = tenantApprovalRequestRepository;
        _accountRepository = accountRepository;
    }

    public async Task<Result<IReadOnlyList<PendingTenantApprovalResponse>>> Handle(GetPendingTenantRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentTenant.IsSuperAdmin)
            return Result.Failure<IReadOnlyList<PendingTenantApprovalResponse>>(TenantApprovalRequestErrors.Unauthorized);

        var requests = await _tenantApprovalRequestRepository.GetPendingAsync(cancellationToken);
        var responses = new List<PendingTenantApprovalResponse>(requests.Count);

        foreach (var item in requests)
        {
            var requester = await _accountRepository.GetByIdAsync(item.RequestedById, cancellationToken);
            responses.Add(new PendingTenantApprovalResponse(
                item.Id,
                item.TenantCode,
                item.Name,
                item.CompanyName,
                item.TaxCode,
                requester?.Email,
                item.EmployeeCount,
                item.CreatedAt,
                item.ExpiresAt,
                item.Status));
        }

        return Result.Success<IReadOnlyList<PendingTenantApprovalResponse>>(responses);
    }
}
