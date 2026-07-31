using Linewise.Domain.Rostering;

namespace Linewise.Application.Rostering;

/// <summary>Builds a roster.</summary>
public interface IAssignmentEngine
{
    /// <summary>
    /// Generates a whole week. Never a single day: fairness only means anything across a
    /// week, and generating a day at a time lets one person take the worst line five days
    /// running with every individual day looking correct.
    /// </summary>
    /// <remarks>
    /// Never throws for a business rule failure. Anything that cannot be satisfied comes
    /// back as a warning on the day it affects.
    /// </remarks>
    RosterWeek Generate(AssignmentRequest request);
}
