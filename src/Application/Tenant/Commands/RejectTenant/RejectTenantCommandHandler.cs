using FinFlow.Application.Tenant.DTOs.Responses;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Domain.TenantApprovals;

namespace FinFlow.Application.Tenant.Commands.RejectTenant;

public sealed class RejectTenantCommandHandler : MediatR.IRequestHandler<RejectTenantCommand, Result<TenantApprovalDecisionResponse>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantApprovalRequestRepository _tenantApprovalRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectTenantCommandHandler(
        ICurrentTenant currentTenant,
        ITenantApprovalRequestRepository tenantApprovalRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _currentTenant = currentTenant;
        _tenantApprovalRequestRepository = tenantApprovalRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TenantApprovalDecisionResponse>> Handle(RejectTenantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentTenant.IsSuperAdmin)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.Unauthorized);

        var approvalRequest = await _tenantApprovalRequestRepository.GetByIdForUpdateAsync(request.Request.RequestId, cancellationToken);
        if (approvalRequest == null)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.NotFound);

        var rejectResult = approvalRequest.Reject(request.Request.Reason);
        if (rejectResult.IsFailure)
            return Result.Failure<TenantApprovalDecisionResponse>(rejectResult.Error);

        _tenantApprovalRequestRepository.Update(approvalRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TenantApprovalDecisionResponse(
            approvalRequest.Id,
            approvalRequest.Status,
            null,
            null,
            approvalRequest.Name));
    }
}
