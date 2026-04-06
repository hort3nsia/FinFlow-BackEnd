using FinFlow.Domain.Entities;
using FinFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Repositories;

internal sealed class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _dbContext;

    public TenantRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Tenant>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Tenant?> GetByCodeAsync(string tenantCode, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Tenant>().AsNoTracking().FirstOrDefaultAsync(t => t.TenantCode == tenantCode, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(string tenantCode, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Tenant>().AnyAsync(t => t.TenantCode == tenantCode, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Tenant>().AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);

    public void Add(Tenant tenant) => _dbContext.Set<Tenant>().Add(tenant);
    public void Update(Tenant tenant) => _dbContext.Set<Tenant>().Update(tenant);
    public void Remove(Tenant tenant) => _dbContext.Set<Tenant>().Remove(tenant);
}
