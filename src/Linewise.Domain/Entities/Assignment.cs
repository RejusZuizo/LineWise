using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Domain.Entities;

/// <summary>
/// One person on one line for one shift. The output of the engine and the unit the
/// manager drags around the grid.
/// </summary>
public sealed record Assignment
{
    public required DateOnly Date { get; init; }

    public required Guid ShiftId { get; init; }

    public required Guid LineId { get; init; }

    public required Guid EmployeeId { get; init; }

    public required AssignmentRole Role { get; init; }

    /// <summary>
    /// Survives regeneration untouched. Set by every manual move, so the engine cannot
    /// undo a decision the manager has already made.
    /// </summary>
    public bool IsLocked { get; init; }

    public AssignmentSource Source { get; init; } = AssignmentSource.Auto;

    /// <summary>Which rule placed this, and at what preference rank.</summary>
    public AssignmentExplanation Explanation { get; init; } = AssignmentExplanation.ManualOverride;
}
