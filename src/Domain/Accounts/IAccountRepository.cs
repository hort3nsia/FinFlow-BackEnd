using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Accounts;

public record AccountLoginInfo(Guid Id, string Email, string PasswordHash, RoleType Role, Guid IdTenant, Guid IdDepartment, bool IsActive);

public interface IAccountRepository
{
    Task<AccountLoginInfo?> GetLoginInfoByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetByTenantIdAsync(Guid idTenant, CancellationToken cancellationToken = default);
    void Add(Account account);
    void Update(Account account);
    void Remove(Account account);
}
