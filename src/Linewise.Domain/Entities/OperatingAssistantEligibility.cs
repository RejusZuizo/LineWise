namespace Linewise.Domain.Entities;

/// <summary>
/// Permission for one employee to be an operating assistant on one line.
/// </summary>
/// <remarks>
/// Deliberately a separate record from <see cref="LeaderEligibility"/> rather than one
/// table with a role column. The two are different permissions granted for different
/// reasons — leading a line and assisting on one are not the same competence — and a site
/// will grant them to overlapping but different people. One table would also have made the
/// existing leadership rows carry a value nobody set, which is the kind of migration that
/// goes wrong quietly.
/// </remarks>
public sealed record OperatingAssistantEligibility
{
    public required Guid EmployeeId { get; init; }

    public required Guid LineId { get; init; }
}
