using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// Marking somebody absent for a day, and saying which slots that leaves to fill.
/// </summary>
/// <remarks>
/// Writes availability rather than the roster. The assignments the absent person holds are
/// left exactly as they are, so the grid can grey the name rather than lose it and the
/// manager can still see who should have been there. What stops the roster being wrong is
/// that everything reading it asks <see cref="Attendance"/> who is actually in.
/// </remarks>
public interface IAbsenceService
{
    Task<AbsenceResult> MarkAbsentAsync(
        MarkAbsentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts somebody back, for the person who rang in sick and then turned up anyway.
    /// Restores them to Working and leaves their assignments alone, which is all that is
    /// needed because those were never removed.
    /// </summary>
    Task<AbsenceResult> ClearAbsenceAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

public sealed record MarkAbsentRequest
{
    public required Guid EmployeeId { get; init; }

    public required DateOnly Date { get; init; }

    public required AbsenceReason Reason { get; init; }
}

/// <param name="Vacated">
/// The slots the absent person was holding. Empty when they were not rostered that day,
/// which is an ordinary outcome rather than a failure: somebody can be marked absent
/// before a roster exists.
/// </param>
public sealed record AbsenceResult(IReadOnlyList<Assignment> Vacated);
