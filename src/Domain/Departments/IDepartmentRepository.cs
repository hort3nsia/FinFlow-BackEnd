using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Departments;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetByTenantIdAsync(Guid idTenant, CancellationToken cancellationToken = default);
    void Add(Department department);
    void Update(Department department);
    void Remove(Department department);
}
