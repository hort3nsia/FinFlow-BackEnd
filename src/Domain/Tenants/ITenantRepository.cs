using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Tenants;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByCodeAsync(string tenantCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string tenantCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    void Add(Tenant tenant);
    void Update(Tenant tenant);
    void Remove(Tenant tenant);
}
