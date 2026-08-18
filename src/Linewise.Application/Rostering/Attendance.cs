using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// Who is actually going to turn up, as opposed to who the roster says. Shared by the
/// grid, the counts and the printed sheet so the three cannot disagree about whether
/// somebody is there.
/// </summary>
/// <remarks>
/// An absence is derived from availability rather than stamped on the assignment. The
/// assignment stays exactly as it was, which is what lets the grid grey a name out rather
/// than lose it: the manager can still see who should have been there. ADR 0015.
/// </remarks>
public sealed class Attendance
{
    private readonly HashSet<(Guid EmployeeId, DateOnly Date)> _absent;

    public Attendance(IEnumerable<Availability> availability)
    {
        ArgumentNullException.ThrowIfNull(availability);

        _absent = availability
            .Where(record => record.Status is AvailabilityStatus.Off or AvailabilityStatus.Holiday)
            .Select(record => (record.EmployeeId, record.Date))
            .ToHashSet();
    }

    /// <summary>Nobody is absent. For a caller that has no availability to hand.</summary>
    public static Attendance Everybody { get; } = new([]);

    /// <summary>
    /// Whether this placement is somebody who will not be in.
    /// </summary>
    /// <remarks>
    /// Only a record that says <see cref="AvailabilityStatus.Off"/> or
    /// <see cref="AvailabilityStatus.Holiday"/> counts. A person with no record at all is
    /// not absent here, which is the opposite of the rule the engine applies when it
    /// decides who to place.
    /// <para>
    /// The difference is deliberate and matters. The engine is answering "may I place
    /// this person", where silence has to mean no. This is answering "did the roster's
    /// decision get overturned", where silence means nothing happened. Reading silence as
    /// absence here would grey out every name in a week whose sheet was never imported,
    /// and print a wall sheet with nobody on it.
    /// </para>
    /// </remarks>
    public bool IsAbsent(Guid employeeId, DateOnly date) => _absent.Contains((employeeId, date));

    public bool IsAbsent(Assignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return IsAbsent(assignment.EmployeeId, assignment.Date);
    }
}
