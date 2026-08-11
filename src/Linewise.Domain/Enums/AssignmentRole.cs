namespace Linewise.Domain.Enums;

/// <summary>What an employee does on the line they are assigned to.</summary>
public enum AssignmentRole
{
    Worker = 0,

    /// <summary>
    /// Leads the line for that date and shift. Leadership is assigned per day, not held
    /// as a fixed attribute, and a leader still counts toward the line headcount.
    /// </summary>
    LineLeader = 1,

    /// <summary>
    /// Second in charge on the line for that date and shift. Chosen the same way as a
    /// leader — per day, from the people permitted to do it — and counting toward the line
    /// headcount in the same way.
    /// </summary>
    /// <remarks>
    /// A line asks for a number of these rather than exactly one, because a long line may
    /// want two and a short one none.
    /// </remarks>
    OperatingAssistant = 2,
}
