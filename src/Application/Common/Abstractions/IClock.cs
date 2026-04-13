namespace FinFlow.Application.Common.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
