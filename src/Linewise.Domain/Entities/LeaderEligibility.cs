namespace Linewise.Domain.Entities;

/// <summary>
/// Permission for one employee to lead one line. Leadership is chosen per day from the
/// people who hold this, not stored as an attribute of the employee.
/// </summary>
public sealed record LeaderEligibility
{
    public required Guid EmployeeId { get; init; }

    public required Guid LineId { get; init; }
}
