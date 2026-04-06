namespace FinFlow.Domain.Abstractions;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
