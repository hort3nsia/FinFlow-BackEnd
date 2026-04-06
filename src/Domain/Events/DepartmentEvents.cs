using FinFlow.Domain.Abstractions;

namespace FinFlow.Domain.Events;

public sealed record DepartmentCreatedDomainEvent(Guid DepartmentId, string Name, Guid IdTenant) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record DepartmentDeactivatedDomainEvent(Guid DepartmentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record DepartmentActivatedDomainEvent(Guid DepartmentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
