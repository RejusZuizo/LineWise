using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// A placement from an earlier week, reduced to what fairness needs to know. Kept
/// separate from a full assignment so the caller can supply a rolling window cheaply.
/// </summary>
public sealed record HistoricAssignment
{
    public required DateOnly Date { get; init; }

    public required Guid EmployeeId { get; init; }

    public required Guid LineId { get; init; }

    public required AssignmentRole Role { get; init; }
}
