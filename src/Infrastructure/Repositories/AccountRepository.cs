using FinFlow.Domain.Accounts;
using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Repositories;

internal sealed class AccountRepository : IAccountRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AccountRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<AccountLoginInfo?> GetLoginInfoByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Account>()
            .AsNoTracking()
            .Where(a => a.Email == email.ToLowerInvariant())
            .Select(a => new AccountLoginInfo(a.Id, a.Email, a.PasswordHash, a.Role, a.IdTenant, a.IdDepartment, a.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Account>().AnyAsync(a => a.Email == email.ToLowerInvariant(), cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByTenantIdAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Account>().AsNoTracking().Where(a => a.IdTenant == idTenant).ToListAsync(cancellationToken);

    public void Add(Account account) => _dbContext.Set<Account>().Add(account);
    public void Update(Account account) => _dbContext.Set<Account>().Update(account);
    public void Remove(Account account) => _dbContext.Set<Account>().Remove(account);
}
