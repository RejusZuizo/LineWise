using Linewise.Application.Abstractions;

namespace Linewise.Infrastructure;

/// <inheritdoc cref="IClock"/>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
