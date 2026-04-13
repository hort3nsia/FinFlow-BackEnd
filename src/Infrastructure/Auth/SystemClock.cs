using FinFlow.Application.Common.Abstractions;

namespace FinFlow.Infrastructure.Auth;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
