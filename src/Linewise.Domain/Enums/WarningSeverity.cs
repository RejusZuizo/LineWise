namespace Linewise.Domain.Enums;

/// <summary>How much attention a warning deserves.</summary>
public enum WarningSeverity
{
    /// <summary>Worth knowing. The roster is usable as it stands.</summary>
    Notice = 0,

    /// <summary>
    /// The roster does not meet its requirements and needs the manager's attention.
    /// Still not an exception: the engine never throws for a business rule failure.
    /// </summary>
    Error = 1,
}
