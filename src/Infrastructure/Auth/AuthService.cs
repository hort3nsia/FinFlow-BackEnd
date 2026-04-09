using FinFlow.Application.Auth.Dtos;
using FinFlow.Application.Auth.Interfaces;
using FinFlow.Application.Auth.Commands.ChangePassword;
using FinFlow.Application.Auth.Commands.Login;
using FinFlow.Application.Auth.Commands.Logout;
using FinFlow.Application.Auth.Commands.RefreshToken;
using FinFlow.Application.Auth.Commands.Register;
using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common.Abstractions;
using FinFlow.Application.Membership.Commands.AcceptInvite;
using FinFlow.Application.Membership.Commands.InviteMember;
using FinFlow.Application.Membership.Commands.SwitchWorkspace;
using FinFlow.Application.Membership.Responses;
using FinFlow.Application.Tenant.Responses;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Accounts;
using FinFlow.Domain.Interfaces;
using FinFlow.Domain.Tenants;
using FinFlow.Domain.TenantApprovals;
using FinFlow.Domain.TenantMemberships;
using FinFlow.Domain.Departments;
using FinFlow.Domain.Invitations;
using FinFlow.Domain.RefreshTokens;
using MediatR;

namespace FinFlow.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITenantRepository _tenantRepo;
    private readonly ITenantApprovalRequestRepository _tenantApprovalRequestRepo;
    private readonly ITenantMembershipRepository _membershipRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly IInvitationRepository _invitationRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _tokenService;
    private readonly ILoginRateLimiter _rateLimiter;
    private readonly ICurrentTenant _currentTenant;
    private readonly IMediator _mediator;

    public AuthService(
        IAccountRepository accountRepo,
        ITenantRepository tenantRepo,
        ITenantApprovalRequestRepository tenantApprovalRequestRepo,
        ITenantMembershipRepository membershipRepo,
        IDepartmentRepository departmentRepo,
        IInvitationRepository invitationRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IUnitOfWork unitOfWork,
        JwtTokenService tokenService,
        ILoginRateLimiter rateLimiter,
        ICurrentTenant currentTenant,
        IMediator mediator)
    {
        _accountRepo = accountRepo;
        _tenantRepo = tenantRepo;
        _tenantApprovalRequestRepo = tenantApprovalRequestRepo;
        _membershipRepo = membershipRepo;
        _departmentRepo = departmentRepo;
        _invitationRepo = invitationRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _rateLimiter = rateLimiter;
        _currentTenant = currentTenant;
        _mediator = mediator;
    }

    private AuthResponse CreateAuthResponse(AccountLoginInfo account, TenantMembershipSummary membership, string accessToken, string refreshToken) =>
        new(accessToken, refreshToken, account.Id, membership.Id, account.Email, membership.Role, membership.IdTenant);

    private async Task<Result> EnsureTenantCreationActorAsync(Guid accountId, Guid? currentMembershipId, CancellationToken cancellationToken)
    {
        var account = await _accountRepo.GetLoginInfoByIdAsync(accountId, cancellationToken);
        if (account == null || !account.IsActive)
            return Result.Failure(AccountErrors.Unauthorized);

        if (_currentTenant.IsSuperAdmin)
            return Result.Success();

        if (!currentMembershipId.HasValue)
            return Result.Failure(AccountErrors.Unauthorized);

        var currentMembership = await _membershipRepo.GetByIdAsync(currentMembershipId.Value, cancellationToken);
        if (currentMembership == null || !currentMembership.IsActive)
            return Result.Failure(AccountErrors.Unauthorized);

        if (currentMembership.AccountId != accountId)
            return Result.Failure(AccountErrors.Unauthorized);

        if (currentMembership.Role != RoleType.TenantAdmin)
            return Result.Failure(TenantErrors.Forbidden);

        return Result.Success();
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(
            new LoginCommand(request.Email, request.Password, request.TenantCode, clientIp),
            cancellationToken);
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(
            new RegisterCommand(request.Email, request.Password, request.Name, request.TenantCode, request.DepartmentName, clientIp),
            cancellationToken);
    }

    public async Task<Result<AuthResponse>> CreateSharedTenantAsync(CreateSharedTenantRequest request, CancellationToken cancellationToken = default)
    {
        var actorCheck = await EnsureTenantCreationActorAsync(request.AccountId, request.CurrentMembershipId, cancellationToken);
        if (actorCheck.IsFailure)
            return Result.Failure<AuthResponse>(actorCheck.Error);

        var account = await _accountRepo.GetLoginInfoByIdAsync(request.AccountId, cancellationToken);
        if (account == null || !account.IsActive)
            return Result.Failure<AuthResponse>(AccountErrors.Unauthorized);

        if (await _membershipRepo.ExistsOwnerByAccountIdAsync(request.AccountId, cancellationToken))
            return Result.Failure<AuthResponse>(TenantErrors.UserAlreadyHasTenant);

        if (await _tenantRepo.ExistsByCodeAsync(request.TenantCode, cancellationToken))
            return Result.Failure<AuthResponse>(TenantErrors.CodeAlreadyExists);

        if (await _tenantApprovalRequestRepo.IsTenantCodeBlockedAsync(request.TenantCode, DateTime.UtcNow, cancellationToken))
            return Result.Failure<AuthResponse>(TenantErrors.CodeBlocked);

        var tenantResult = Tenant.Create(request.Name, request.TenantCode, TenancyModel.Shared, request.Currency);
        if (tenantResult.IsFailure)
            return Result.Failure<AuthResponse>(tenantResult.Error);

        var tenant = tenantResult.Value;

        var departmentResult = Department.Create("Root", tenant.Id);
        if (departmentResult.IsFailure)
            return Result.Failure<AuthResponse>(departmentResult.Error);

        var department = departmentResult.Value;

        var membershipResult = TenantMembership.Create(account.Id, tenant.Id, RoleType.TenantAdmin, isOwner: true);
        if (membershipResult.IsFailure)
            return Result.Failure<AuthResponse>(membershipResult.Error);

        var membership = membershipResult.Value;

        var refreshTokenRaw = _tokenService.GenerateRefreshToken();
        var refreshTokenResult = RefreshToken.Create(
            refreshTokenRaw,
            account.Id,
            membership.Id,
            _tokenService.RefreshTokenExpirationDays);

        if (refreshTokenResult.IsFailure)
            return Result.Failure<AuthResponse>(refreshTokenResult.Error);

        var originalTenantId = _currentTenant.Id;
        var originalMembershipId = _currentTenant.MembershipId;
        var originalIsSuperAdmin = _currentTenant.IsSuperAdmin;

        try
        {
            _currentTenant.Id = tenant.Id;
            _currentTenant.MembershipId = membership.Id;
            _currentTenant.IsSuperAdmin = false;

            _tenantRepo.Add(tenant);
            _departmentRepo.Add(department);
            _membershipRepo.Add(membership);
            _refreshTokenRepo.Add(refreshTokenResult.Value);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _currentTenant.Id = originalTenantId;
            _currentTenant.MembershipId = originalMembershipId;
            _currentTenant.IsSuperAdmin = originalIsSuperAdmin;
        }

        var accessToken = _tokenService.GenerateAccessToken(
            account.Id,
            account.Email,
            membership.Role.ToString(),
            membership.IdTenant,
            membership.Id);

        return Result.Success(CreateAuthResponse(
            account,
            new TenantMembershipSummary(membership.Id, membership.AccountId, membership.IdTenant, membership.Role, membership.IsOwner, membership.IsActive, membership.CreatedAt),
            accessToken,
            refreshTokenRaw));
    }

    public async Task<Result<TenantApprovalResponse>> CreateIsolatedTenantAsync(CreateIsolatedTenantRequest request, CancellationToken cancellationToken = default)
    {
        var actorCheck = await EnsureTenantCreationActorAsync(request.AccountId, request.CurrentMembershipId, cancellationToken);
        if (actorCheck.IsFailure)
            return Result.Failure<TenantApprovalResponse>(actorCheck.Error);

        if (await _membershipRepo.ExistsOwnerByAccountIdAsync(request.AccountId, cancellationToken))
            return Result.Failure<TenantApprovalResponse>(TenantErrors.UserAlreadyHasTenant);

        if (await _tenantRepo.ExistsByCodeAsync(request.TenantCode, cancellationToken)
            || await _tenantApprovalRequestRepo.ExistsPendingByTenantCodeAsync(request.TenantCode, cancellationToken))
            return Result.Failure<TenantApprovalResponse>(TenantErrors.CodeAlreadyExists);

        if (await _tenantApprovalRequestRepo.IsTenantCodeBlockedAsync(request.TenantCode, DateTime.UtcNow, cancellationToken))
            return Result.Failure<TenantApprovalResponse>(TenantErrors.CodeBlocked);

        var approvalResult = TenantApprovalRequest.Create(
            request.TenantCode,
            request.Name,
            request.CompanyInfo.CompanyName,
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
        _tenantApprovalRequestRepo.Add(approvalRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TenantApprovalResponse(
            approvalRequest.Id,
            approvalRequest.Status,
            "Waiting for approval",
            approvalRequest.ExpiresAt));
    }

    public async Task<Result<IReadOnlyList<PendingTenantApprovalResponse>>> GetPendingTenantRequestsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.IsSuperAdmin)
            return Result.Failure<IReadOnlyList<PendingTenantApprovalResponse>>(TenantApprovalRequestErrors.Unauthorized);

        var requests = await _tenantApprovalRequestRepo.GetPendingAsync(cancellationToken);
        var responses = new List<PendingTenantApprovalResponse>(requests.Count);

        foreach (var request in requests)
        {
            var requester = await _accountRepo.GetByIdAsync(request.RequestedById, cancellationToken);
            responses.Add(new PendingTenantApprovalResponse(
                request.Id,
                request.TenantCode,
                request.Name,
                request.CompanyName,
                request.TaxCode,
                requester?.Email,
                request.EmployeeCount,
                request.CreatedAt,
                request.ExpiresAt,
                request.Status));
        }

        return Result.Success<IReadOnlyList<PendingTenantApprovalResponse>>(responses);
    }

    public async Task<Result<TenantApprovalDecisionResponse>> ApproveTenantAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.IsSuperAdmin)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.Unauthorized);

        var approvalRequest = await _tenantApprovalRequestRepo.GetByIdForUpdateAsync(requestId, cancellationToken);
        if (approvalRequest == null)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.NotFound);

        if (await _tenantApprovalRequestRepo.IsTenantCodeBlockedAsync(approvalRequest.TenantCode, DateTime.UtcNow, cancellationToken))
            return Result.Failure<TenantApprovalDecisionResponse>(TenantErrors.CodeBlocked);

        if (await _tenantRepo.ExistsByCodeAsync(approvalRequest.TenantCode, cancellationToken))
            return Result.Failure<TenantApprovalDecisionResponse>(TenantErrors.CodeAlreadyExists);

        if (await _membershipRepo.ExistsOwnerByAccountIdAsync(approvalRequest.RequestedById, cancellationToken))
            return Result.Failure<TenantApprovalDecisionResponse>(TenantErrors.UserAlreadyHasTenant);

        var tenantResult = Tenant.Create(
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

        _tenantRepo.Add(tenant);
        _membershipRepo.Add(membershipResult.Value);
        _tenantApprovalRequestRepo.Update(approvalRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TenantApprovalDecisionResponse(
            approvalRequest.Id,
            approvalRequest.Status,
            tenant.Id,
            tenant.TenantCode,
            tenant.Name));
    }

    public async Task<Result<TenantApprovalDecisionResponse>> RejectTenantAsync(Guid requestId, string reason, CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.IsSuperAdmin)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.Unauthorized);

        var approvalRequest = await _tenantApprovalRequestRepo.GetByIdForUpdateAsync(requestId, cancellationToken);
        if (approvalRequest == null)
            return Result.Failure<TenantApprovalDecisionResponse>(TenantApprovalRequestErrors.NotFound);

        var rejectResult = approvalRequest.Reject(reason);
        if (rejectResult.IsFailure)
            return Result.Failure<TenantApprovalDecisionResponse>(rejectResult.Error);

        _tenantApprovalRequestRepo.Update(approvalRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TenantApprovalDecisionResponse(
            approvalRequest.Id,
            approvalRequest.Status,
            null,
            null,
            approvalRequest.Name));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
    }

    public async Task<Result<AuthResponse>> SwitchWorkspaceAsync(SwitchWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(
            new SwitchWorkspaceCommand(request.AccountId, request.MembershipId, request.CurrentRefreshToken),
            cancellationToken);
    }

    public async Task<Result<InvitationResponse>> InviteMemberAsync(InviteMemberRequest request, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(
            new InviteMemberCommand(request.InviterAccountId, request.InviterMembershipId, request.Email, request.Role),
            cancellationToken);
    }

    public async Task<Result<AuthResponse>> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(
            new AcceptInviteCommand(request.InviteToken, request.Password, request.ClientIp),
            cancellationToken);
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(
            new ChangePasswordCommand(request.AccountId, request.CurrentPassword, request.NewPassword),
            cancellationToken);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(new LogoutCommand(refreshToken), cancellationToken);
    }
}
