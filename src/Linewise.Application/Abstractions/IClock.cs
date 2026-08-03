namespace Linewise.Application.Abstractions;

/// <summary>
/// The current time, injected rather than read from a static, so that anything depending on
/// "now" can be tested without waiting for it.
/// </summary>
public interface IClock
{
    /// <summary>Always UTC. Local time exists only at the point something is displayed.</summary>
    DateTime UtcNow { get; }
}
