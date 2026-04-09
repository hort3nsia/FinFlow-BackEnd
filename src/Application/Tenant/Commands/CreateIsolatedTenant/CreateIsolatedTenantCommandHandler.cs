using FinFlow.Application.Tenant.DTOs.Responses;
using FinFlow.Application.Tenant.Support;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using FinFlow.Domain.TenantApprovals;
using FinFlow.Domain.TenantMemberships;
using FinFlow.Domain.Tenants;
using TenantApprovalRequestEntity = FinFlow.Domain.Entities.TenantApprovalRequest;

namespace FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;

public sealed class CreateIsolatedTenantCommandHandler : MediatR.IRequestHandler<CreateIsolatedTenantCommand, Result<TenantApprovalResponse>>
{
    private readonly TenantCreationActorAuthorizationService _authorizationService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantApprovalRequestRepository _tenantApprovalRequestRepository;
    private readonly ITenantMembershipRepository _membershipRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIsolatedTenantCommandHandler(
        TenantCreationActorAuthorizationService authorizationService,
        ITenantRepository tenantRepository,
        ITenantApprovalRequestRepository tenantApprovalRequestRepository,
        ITenantMembershipRepository membershipRepository,
        IUnitOfWork unitOfWork)
    {
        _authorizationService = authorizationService;
        _tenantRepository = tenantRepository;
        _tenantApprovalRequestRepository = tenantApprovalRequestRepository;
        _membershipRepository = membershipRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TenantApprovalResponse>> Handle(CreateIsolatedTenantCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        
        var actorCheck = await _authorizationService.EnsureCanCreateTenantAsync(request.AccountId, request.CurrentMembershipId, cancellationToken);
        if (actorCheck.IsFailure)
            return Result.Failure<TenantApprovalResponse>(actorCheck.Error);

        if (await _membershipRepository.ExistsOwnerByAccountIdAsync(request.AccountId, cancellationToken))
            return Result.Failure<TenantApprovalResponse>(TenantErrors.UserAlreadyHasTenant);

        if (await _tenantRepository.ExistsByCodeAsync(request.TenantCode, cancellationToken)
            || await _tenantApprovalRequestRepository.ExistsPendingByTenantCodeAsync(request.TenantCode, cancellationToken))
            return Result.Failure<TenantApprovalResponse>(TenantErrors.CodeAlreadyExists);

        if (await _tenantApprovalRequestRepository.IsTenantCodeBlockedAsync(request.TenantCode, DateTime.UtcNow, cancellationToken))
            return Result.Failure<TenantApprovalResponse>(TenantErrors.CodeBlocked);

        var approvalResult = TenantApprovalRequestEntity.Create(
            request.TenantCode,
            request.Name,
            request.CompanyInfo!.CompanyName,
            request.CompanyInfo.TaxCode,
            request.CompanyInfo.Address,
            request.CompanyInfo.Phone,
            request.CompanyInfo.ContactPerson,
            request.CompanyInfo.BusinessType,
            request.CompanyInfo.EmployeeCount,
            request.Currency,
            request.AccountId,
            DateTime.UtcNow.AddDays(7));

        if (approvalResult.IsFailure)
            return Result.Failure<TenantApprovalResponse>(approvalResult.Error);

        var approvalRequest = approvalResult.Value;
        _tenantApprovalRequestRepository.Add(approvalRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TenantApprovalResponse(
            approvalRequest.Id,
            approvalRequest.Status,
            "Waiting for approval",
            approvalRequest.ExpiresAt));
    }
}
