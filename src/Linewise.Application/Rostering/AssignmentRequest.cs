using Linewise.Domain.Entities;

namespace Linewise.Application.Rostering;

/// <summary>
/// Everything the engine needs for one week, as an immutable snapshot. The engine reads
/// no repository and opens no file: whatever is here is the whole world as far as it is
/// concerned, which is what makes it testable and reproducible.
/// </summary>
public sealed record AssignmentRequest
{
    /// <summary>The Monday the week starts on. Used only to label the result.</summary>
    public required DateOnly WeekStart { get; init; }

    public required RosterConfiguration Configuration { get; init; }

    /// <summary>
    /// Every date and shift to generate. A roster day is produced for each, in date then
    /// shift order.
    /// </summary>
    public IReadOnlyList<Shift> Shifts { get; init; } = [];

    /// <summary>
    /// Who is in on which date. An employee with no record for a date is treated as off:
    /// silence is never read as availability.
    /// </summary>
    public IReadOnlyList<Availability> Availabilities { get; init; } = [];

    /// <summary>
    /// Lines whose headcount is raised or lowered for a particular date. Absent means the
    /// line runs at its standard headcount.
    /// </summary>
    public IReadOnlyList<LineDemand> Demands { get; init; } = [];

    /// <summary>
    /// Assignments the manager has already placed by hand. Carried through untouched and
    /// never moved.
    /// </summary>
    public IReadOnlyList<Assignment> LockedAssignments { get; init; } = [];

    /// <summary>
    /// Recent weeks, used to even out who gets their preferred line and who leads. The
    /// caller decides how far back the window reaches.
    /// </summary>
    public IReadOnlyList<HistoricAssignment> History { get; init; } = [];
}
