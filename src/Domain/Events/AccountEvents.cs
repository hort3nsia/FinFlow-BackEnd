using FinFlow.Domain.Abstractions;
namespace FinFlow.Domain.Events;

public sealed record AccountCreatedDomainEvent(Guid AccountId, string Email, DateTime CreatedAtUtc) : IDomainEvent
{
    public DateTime OccurredOn { get; } = CreatedAtUtc;
}

public sealed record AccountDeactivatedDomainEvent(Guid AccountId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record AccountActivatedDomainEvent(Guid AccountId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record AccountEmailVerifiedDomainEvent(Guid AccountId, DateTime VerifiedAtUtc) : IDomainEvent
{
    public DateTime OccurredOn { get; } = VerifiedAtUtc;
}

