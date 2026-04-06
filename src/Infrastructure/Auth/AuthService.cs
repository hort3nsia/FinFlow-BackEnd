using FinFlow.Application.Auth.Dtos;
using FinFlow.Application.Auth.Interfaces;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Accounts;
using FinFlow.Domain.Tenants;
using FinFlow.Domain.Departments;

namespace FinFlow.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITenantRepository _tenantRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _tokenService;

    public AuthService(
        IAccountRepository accountRepo,
        ITenantRepository tenantRepo,
        IDepartmentRepository departmentRepo,
        IUnitOfWork unitOfWork,
        JwtTokenService tokenService)
    {
        _accountRepo = accountRepo;
        _tenantRepo = tenantRepo;
        _departmentRepo = departmentRepo;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepo.GetLoginInfoByEmailAsync(request.Email, cancellationToken);

        if (account == null || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            return Result.Failure<AuthResponse>(AccountErrors.InvalidCurrentPassword);

        if (!account.IsActive)
            return Result.Failure<AuthResponse>(AccountErrors.AlreadyDeactivated);

        var accessToken = _tokenService.GenerateAccessToken(
            account.Id, account.Email, account.Role.ToString(), account.IdTenant, account.IdDepartment);
        var refreshToken = _tokenService.GenerateRefreshToken();

        return Result.Success(new AuthResponse(
            accessToken, refreshToken, account.Id, account.Email, account.Role, account.IdTenant, account.IdDepartment));
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingAccount = await _accountRepo.ExistsByEmailAsync(request.Email, cancellationToken);
        if (existingAccount)
            return Result.Failure<AuthResponse>(AccountErrors.EmailAlreadyExists);

        var existingTenant = await _tenantRepo.ExistsByCodeAsync(request.TenantCode, cancellationToken);
        if (existingTenant)
            return Result.Failure<AuthResponse>(TenantErrors.CodeAlreadyExists);

        var tenantResult = Tenant.Create(request.Name, request.TenantCode, TenancyModel.Shared, "VND");
        if (tenantResult.IsFailure)
            return Result.Failure<AuthResponse>(tenantResult.Error);

        var tenant = tenantResult.Value;

        var departmentResult = Department.Create(request.DepartmentName, tenant.Id);
        if (departmentResult.IsFailure)
            return Result.Failure<AuthResponse>(departmentResult.Error);

        var department = departmentResult.Value;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var accountResult = Account.Create(request.Email, passwordHash, RoleType.TenantAdmin, tenant.Id, department.Id);
        if (accountResult.IsFailure)
            return Result.Failure<AuthResponse>(accountResult.Error);

        var account = accountResult.Value;

        _tenantRepo.Add(tenant);
        _departmentRepo.Add(department);
        _accountRepo.Add(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _tokenService.GenerateAccessToken(
            account.Id, account.Email, account.Role.ToString(), account.IdTenant, account.IdDepartment);
        var refreshToken = _tokenService.GenerateRefreshToken();

        return Result.Success(new AuthResponse(
            accessToken, refreshToken, account.Id, account.Email, account.Role, account.IdTenant, account.IdDepartment));
    }

    public Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var principal = _tokenService.ValidateToken(request.AccessToken);
        if (principal == null)
            return Task.FromResult(Result.Failure<AuthResponse>(AccountErrors.InvalidCurrentPassword));

        var id = Guid.Parse(principal.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
        var email = principal.FindFirst("email")?.Value ?? string.Empty;
        var role = principal.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ?? string.Empty;
        var idTenant = Guid.Parse(principal.FindFirst("IdTenant")?.Value ?? Guid.Empty.ToString());
        var idDepartment = Guid.Parse(principal.FindFirst("IdDepartment")?.Value ?? Guid.Empty.ToString());

        var newAccessToken = _tokenService.GenerateAccessToken(id, email, role, idTenant, idDepartment);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        return Task.FromResult(Result.Success(new AuthResponse(
            newAccessToken, newRefreshToken, id, email, Enum.Parse<RoleType>(role), idTenant, idDepartment)));
    }
}
