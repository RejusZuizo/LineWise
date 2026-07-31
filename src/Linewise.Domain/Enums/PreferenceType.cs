namespace Linewise.Domain.Enums;

/// <summary>How strongly a line preference binds the engine.</summary>
public enum PreferenceType
{
    /// <summary>A ranked wish. Honoured when the line has room.</summary>
    Preferred = 0,

    /// <summary>
    /// This employee belongs on this line. Broken only when their status that day is
    /// <see cref="AvailabilityStatus.Overtime"/>, or when it is impossible.
    /// </summary>
    Mandatory = 1,

    /// <summary>
    /// This employee must never work this line. Absolute, overridden only by a locked
    /// assignment placed by hand.
    /// </summary>
    Blocked = 2,
}
