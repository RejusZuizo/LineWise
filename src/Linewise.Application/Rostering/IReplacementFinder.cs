using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// Who could take a place on a line today, best first.
/// </summary>
/// <remarks>
/// Ranked, never alphabetical. A list of a hundred and fifty names in alphabetical order is
/// a list the manager has to think their way through at the worst possible moment; the
/// point of this is that the first name on it is usually the right one.
/// </remarks>
public interface IReplacementFinder
{
    Task<IReadOnlyList<ReplacementCandidate>> FindAsync(
        Guid lineId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

/// <param name="AlreadyOnLineId">
/// The line this person is already standing on today, when they are. Taking them fills one
/// hole by opening another, which the picker says out loud rather than leaving to be
/// discovered on the wall.
/// </param>
/// <param name="PreferenceRank">
/// Their choice for this line, where 1 is first choice. Null when they have no opinion
/// about it, which is not the same as disliking it.
/// </param>
public sealed record ReplacementCandidate
{
    public required Guid EmployeeId { get; init; }

    public required string DisplayName { get; init; }

    public required AvailabilityStatus Status { get; init; }

    public int? PreferenceRank { get; init; }

    public bool IsRequiredHere { get; init; }

    public bool CanLead { get; init; }

    public bool CanAssist { get; init; }

    public Guid? AlreadyOnLineId { get; init; }

    public string? AlreadyOnLineName { get; init; }

    /// <summary>Taking them fills one hole and opens another.</summary>
    public bool LeavesAHoleElsewhere => AlreadyOnLineId is not null;
}
