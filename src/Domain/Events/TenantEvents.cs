using FinFlow.Domain.Abstractions;

namespace FinFlow.Domain.Events;

public sealed record TenantCreatedDomainEvent(Guid TenantId, string TenantCode, string Name) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record TenantDeactivatedDomainEvent(Guid TenantId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record TenantActivatedDomainEvent(Guid TenantId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
