namespace Linewise.Domain.Enums;

/// <summary>
/// Whether an employee can be rostered on a given date. Read from the imported
/// availability sheet.
/// </summary>
public enum AvailabilityStatus
{
    /// <summary>Not available. The engine will not place this employee.</summary>
    Off = 0,

    /// <summary>Available on their normal pattern.</summary>
    Working = 1,

    /// <summary>
    /// Available, but working beyond their normal pattern. This is the only status
    /// that permits a mandatory line preference to be broken.
    /// </summary>
    Overtime = 2,
}
