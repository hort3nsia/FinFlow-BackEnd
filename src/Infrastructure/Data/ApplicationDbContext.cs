using FinFlow.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure;

public class ApplicationDbContext : DbContext, IUnitOfWork
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is Entity entity)
            {
                if (entity.Id == Guid.Empty)
                    entry.Property("Id").CurrentValue = Guid.NewGuid();
            }

            if (entry.State == EntityState.Deleted && entry.Entity is Entity)
            {
                entry.State = EntityState.Modified;
                if (entry.Metadata.FindProperty("IsActive") != null)
                    entry.Property("IsActive").CurrentValue = false;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
