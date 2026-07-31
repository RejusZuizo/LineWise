using Linewise.Domain.Entities;

namespace Linewise.Domain.Rostering;

/// <summary>
/// A set of roster days generated together. The engine works a week at a time because
/// fairness is only meaningful across a week: generating one day at a time lets the same
/// person take the worst line five days running with every individual day looking right.
/// </summary>
public sealed record RosterWeek
{
    /// <summary>The Monday the week starts on.</summary>
    public required DateOnly WeekStart { get; init; }

    public IReadOnlyList<RosterDay> Days { get; init; } = [];

    public IEnumerable<Assignment> AllAssignments => Days.SelectMany(day => day.Assignments);

    public IEnumerable<RosterWarning> AllWarnings => Days.SelectMany(day => day.Warnings);
}
