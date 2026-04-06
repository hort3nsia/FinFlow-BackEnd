using FinFlow.Domain.Departments;
using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Repositories;

internal sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DepartmentRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Department>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Department>> GetByTenantIdAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Department>().AsNoTracking().Where(d => d.IdTenant == idTenant).ToListAsync(cancellationToken);

    public void Add(Department department) => _dbContext.Set<Department>().Add(department);
    public void Update(Department department) => _dbContext.Set<Department>().Update(department);
    public void Remove(Department department) => _dbContext.Set<Department>().Remove(department);
}
