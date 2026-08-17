namespace Linewise.Domain.Enums;

/// <summary>
/// Who said an employee was in or out on a date: the sheet, or the manager.
/// </summary>
/// <remarks>
/// The same distinction <see cref="AssignmentSource"/> draws for an assignment, and it
/// exists for the same reason. An import is a statement about a whole week and replaces
/// the week wholesale; without provenance, marking somebody absent on Tuesday would be
/// undone by the next re-import of that week, silently and with nothing on screen saying
/// so.
/// </remarks>
public enum AvailabilitySource
{
    /// <summary>
    /// Read from the availability sheet. Replaced by the next import covering that date.
    /// </summary>
    Imported = 0,

    /// <summary>
    /// Set in the application by the manager. Survives a re-import of the same week, and
    /// is only changed by somebody changing it again.
    /// </summary>
    Manual = 1,
}
