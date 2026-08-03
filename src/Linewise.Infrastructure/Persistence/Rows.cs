namespace Linewise.Infrastructure.Persistence;

// The domain models an employee's aliases and skills as collections on the record, which is
// the right shape for the engine. A relational store wants them as rows. These types exist
// only inside this assembly to bridge the two: the domain stays free of join entities, and
// the database stays normalised rather than hiding lists inside delimited strings.

internal sealed class EmployeeAliasRow
{
    public Guid EmployeeId { get; set; }

    public string Alias { get; set; } = string.Empty;
}

internal sealed class EmployeeSkillRow
{
    public Guid EmployeeId { get; set; }

    public Guid SkillId { get; set; }
}

internal sealed class LineSkillRow
{
    public Guid LineId { get; set; }

    public Guid SkillId { get; set; }
}

/// <summary>
/// One date and shift within a saved roster.
/// </summary>
/// <remarks>
/// Assignments could be grouped by date and shift on the way out, which would almost work.
/// It would lose a day that happens to hold nothing, and it would leave the order of days
/// to be guessed at rather than recorded. Storing the day makes the round trip exact.
/// </remarks>
internal sealed class RosterDayRow
{
    public Guid RosterVersionId { get; set; }

    public DateOnly Date { get; set; }

    public Guid ShiftId { get; set; }

    /// <summary>Position within the week, as the engine produced it.</summary>
    public int Ordinal { get; set; }
}
