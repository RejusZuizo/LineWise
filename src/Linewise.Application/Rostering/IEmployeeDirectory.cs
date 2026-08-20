using Linewise.Domain.Entities;

namespace Linewise.Application.Rostering;

/// <summary>
/// Adding people and taking them off the roster, without going through an import.
/// </summary>
/// <remarks>
/// Until now people could only arrive through the availability sheet. Somebody hired on a
/// Tuesday could not be entered, and somebody who left could not be taken out, which makes
/// the product wrong between imports rather than merely incomplete.
/// </remarks>
public interface IEmployeeDirectory
{
    /// <summary>Adds somebody, and says who was added so the screen can select them.</summary>
    Task<Employee> AddAsync(
        string fullName,
        bool isTemporary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes somebody off the roster without deleting them.
    /// </summary>
    /// <remarks>
    /// Never a hard delete. Historic rosters name people by identifier, and removing the row
    /// would turn every week they ever worked into a sheet full of unknowns. A leaver stops
    /// being available and stays in the record, which is also what the retention position
    /// assumes.
    /// </remarks>
    Task DeactivateAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Puts somebody back, for the leaver who returns or the mistake.</summary>
    Task ReactivateAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
