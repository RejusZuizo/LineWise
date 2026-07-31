namespace Linewise.Application.Rostering;

/// <summary>
/// A running tally of who has had what, seeded from the history window and updated as the
/// week is generated. Reading it is how a tie break decides who is owed a good line.
/// </summary>
/// <remarks>
/// It updates during generation on purpose. If it only held past weeks, the same person
/// could take their first choice five days running and every day would still look fair.
/// </remarks>
public interface IAssignmentLedger
{
    /// <summary>How often this employee landed on a line they had asked for.</summary>
    int PreferredPlacements(Guid employeeId);

    /// <summary>How often this employee was placed at all.</summary>
    int TotalPlacements(Guid employeeId);

    /// <summary>How often this employee has led a line.</summary>
    int LeadCount(Guid employeeId);

    /// <summary>The last date this employee led a line, or null if they never have.</summary>
    DateOnly? LastLedOn(Guid employeeId);
}
