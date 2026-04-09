using FinFlow.Application.Tenant.DTOs.Responses;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Interfaces;
using FinFlow.Domain.TenantApprovals;
using FinFlow.Domain.TenantMemberships;
using FinFlow.Domain.Tenants;
using TenantEntity = FinFlow.Domain.Entities.Tenant;

namespace FinFlow.Application.Tenant.Commands.ApproveTenant;

public sealed class ApproveTenantCommandHandler : MediatR.IRequestHandler<ApproveTenantCommand, Result<TenantApprovalDecisionResponse>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantApprovalRequestRepository _tenantApprovalRequestRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantMembershipRepository _membershipRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveTenantCommandHandler(
        ICurrentTenant currentTenant,
        ITenantApprovalRequestRepository tenantApprovalRequestRepository,
        ITenantRepository tenantRepository,
        ITenantMembershipRepository membershipRepository,
        IUnitOfWork unitOfWork)
    {
        _currentTenant = currentTenant;
        _tenantApprovalRequestRepository = tenantApprovalRequestRepository;
        _tenantRepository = tenantRepository;
        _membershipRepository = membershipRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TenantApprovalDecisionResponse>> Handle(ApproveTenantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentTenant.IsSuperAdmin)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.Unauthorized);

        var approvalRequest = await _tenantApprovalRequestRepository.GetByIdForUpdateAsync(request.Request.RequestId, cancellationToken);
        if (approvalRequest == null)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.NotFound);

        if (await _tenantApprovalRequestRepository.IsTenantCodeBlockedAsync(approvalRequest.TenantCode, DateTime.UtcNow, cancellationToken))
            return Result.Failure<TenantApprovalDecisionResponse>(TenantErrors.CodeBlocked);

        if (await _tenantRepository.ExistsByCodeAsync(approvalRequest.TenantCode, cancellationToken))
            return Result.Failure<TenantApprovalDecisionResponse>(TenantErrors.CodeAlreadyExists);

        if (await _membershipRepository.ExistsOwnerByAccountIdAsync(approvalRequest.RequestedById, cancellationToken))
            return Result.Failure<TenantApprovalDecisionResponse>(TenantErrors.UserAlreadyHasTenant);

        var tenantResult = TenantEntity.Create(
            approvalRequest.Name,
            approvalRequest.TenantCode,
            TenancyModel.Isolated,
            approvalRequest.Currency,
            approvalRequest.CompanyName,
            approvalRequest.TaxCode);

        if (tenantResult.IsFailure)
            return Result.Failure<TenantApprovalDecisionResponse>(tenantResult.Error);

        var tenant = tenantResult.Value;
        var membershipResult = TenantMembership.Create(
            approvalRequest.RequestedById,
            tenant.Id,
            RoleType.TenantAdmin,
            isOwner: true);

        if (membershipResult.IsFailure)
            return Result.Failure<TenantApprovalDecisionResponse>(membershipResult.Error);

        var approveResult = approvalRequest.Approve();
        if (approveResult.IsFailure)
            return Result.Failure<TenantApprovalDecisionResponse>(approveResult.Error);

        _tenantRepository.Add(tenant);
        _membershipRepository.Add(membershipResult.Value);
        _tenantApprovalRequestRepository.Update(approvalRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TenantApprovalDecisionResponse(
            approvalRequest.Id,
            approvalRequest.Status,
            tenant.Id,
            tenant.TenantCode,
            tenant.Name));
    }
}
