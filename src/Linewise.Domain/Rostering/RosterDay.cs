using Linewise.Domain.Entities;

namespace Linewise.Domain.Rostering;

/// <summary>Everything assigned for one date and one shift, plus what went wrong.</summary>
public sealed record RosterDay
{
    public required DateOnly Date { get; init; }

    public required Guid ShiftId { get; init; }

    public IReadOnlyList<Assignment> Assignments { get; init; } = [];

    public IReadOnlyList<RosterWarning> Warnings { get; init; } = [];
}
