using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <inheritdoc cref="IAbsenceService"/>
public sealed class AbsenceService : IAbsenceService
{
    private readonly IAvailabilityRepository _availability;
    private readonly IRosterRepository _rosters;
    private readonly IAuditLog _auditLog;

    public AbsenceService(
        IAvailabilityRepository availability,
        IRosterRepository rosters,
        IAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(auditLog);

        _availability = availability;
        _rosters = rosters;
        _auditLog = auditLog;
    }

    public async Task<AbsenceResult> MarkAbsentAsync(
        MarkAbsentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vacated = await VacatedSlotsAsync(request.EmployeeId, request.Date, cancellationToken)
            .ConfigureAwait(false);

        await _availability
            .SetManualAsync(request.EmployeeId, request.Date, StatusFor(request.Reason), cancellationToken)
            .ConfigureAwait(false);

        // Identifiers, never a name. The audit log is one of the places personal data would
        // otherwise accumulate quietly for years.
        await _auditLog.AppendAsync(
            AuditAction.RosterEdited,
            $"Marked employee {request.EmployeeId} absent on {request.Date:yyyy-MM-dd}, "
                + $"vacating {vacated.Count} of their places.",
            ReasonText(request),
            cancellationToken).ConfigureAwait(false);

        return new AbsenceResult(vacated);
    }

    public async Task<AbsenceResult> ClearAbsenceAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // Still manual. Somebody who rang in sick and then turned up is a decision the
        // manager made twice, and the second one has no more business being overwritten by
        // the next import than the first did.
        await _availability
            .SetManualAsync(employeeId, date, AvailabilityStatus.Working, cancellationToken)
            .ConfigureAwait(false);

        var held = await VacatedSlotsAsync(employeeId, date, cancellationToken).ConfigureAwait(false);

        await _auditLog.AppendAsync(
            AuditAction.RosterEdited,
            $"Marked employee {employeeId} back in on {date:yyyy-MM-dd}.",
            "Back in.",
            cancellationToken).ConfigureAwait(false);

        return new AbsenceResult(held);
    }

    /// <summary>
    /// What the reason means for availability. Everything except a booked holiday is
    /// <see cref="AvailabilityStatus.Off"/>, because the status enum is the whole of what
    /// is held about why somebody is away and it deliberately has no finer grain.
    /// </summary>
    private static AvailabilityStatus StatusFor(AbsenceReason reason) => reason switch
    {
        AbsenceReason.Holiday => AvailabilityStatus.Holiday,
        _ => AvailabilityStatus.Off,
    };

    /// <summary>
    /// The audit chain's own words. English rather than the current culture: an entry is
    /// written once and read years later, possibly on another machine, and a reason that
    /// changed language with a regional setting would be a poor record of anything.
    /// </summary>
    private static string ReasonText(MarkAbsentRequest request) => request.Reason switch
    {
        AbsenceReason.Holiday => "Holiday.",
        AbsenceReason.SentHome => "Sent home.",
        _ => "Not in today.",
    };

    /// <summary>
    /// The places this person holds on that date, read from the stored draft. Left in the
    /// roster rather than removed: the grid greys the name so the manager can still see who
    /// should have been there, and everything that reads the roster asks
    /// <see cref="Attendance"/> whether a placement is really going to turn up.
    /// </summary>
    private async Task<IReadOnlyList<Assignment>> VacatedSlotsAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var weekStart = MondayOf(date);
        var stored = await _rosters.GetLatestAsync(weekStart, cancellationToken).ConfigureAwait(false);

        return stored is null
            ? []
            : [.. stored.Roster.AllAssignments.Where(assignment =>
                assignment.EmployeeId == employeeId && assignment.Date == date)];
    }

    /// <summary>
    /// The ISO week, matching the engine and the grid. A roster's week is a property of the
    /// factory rather than of the machine's regional settings.
    /// </summary>
    private static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
