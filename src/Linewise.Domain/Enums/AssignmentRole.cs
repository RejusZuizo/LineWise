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
}
