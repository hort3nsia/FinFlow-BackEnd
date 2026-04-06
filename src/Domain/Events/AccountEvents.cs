using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Events;

public sealed record AccountCreatedDomainEvent(Guid AccountId, string Email, Guid IdTenant) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record AccountDeactivatedDomainEvent(Guid AccountId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record AccountActivatedDomainEvent(Guid AccountId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record AccountRoleChangedDomainEvent(Guid AccountId, RoleType NewRole) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
