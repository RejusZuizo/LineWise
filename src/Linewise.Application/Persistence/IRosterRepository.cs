using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Persistence;

/// <summary>Stores and retrieves generated rosters.</summary>
public interface IRosterRepository
{
    /// <summary>
    /// Saves the roster as the current draft for its week, replacing any existing draft.
    /// Called continuously while editing, so a crash loses nothing.
    /// </summary>
    Task<RosterVersion> SaveDraftAsync(
        DateOnly weekStart,
        RosterWeek roster,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the draft for a week: increments the version number, stamps the time and
    /// the Windows account, and writes an audit entry. An explicit act, never automatic.
    /// </summary>
    Task<RosterVersion> PublishAsync(
        DateOnly weekStart,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent version for a week, draft or published, or null if none.</summary>
    Task<StoredRoster?> GetLatestAsync(DateOnly weekStart, CancellationToken cancellationToken = default);

    /// <summary>Every version of a week, newest first.</summary>
    Task<IReadOnlyList<RosterVersion>> GetVersionsAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Past placements over a date range, reduced to what the engine's fairness ledger needs.
    /// This is the rolling history window, and the caller decides how far back it reaches.
    /// </summary>
    Task<IReadOnlyList<HistoricAssignment>> GetHistoryAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default);
}

/// <param name="Version">Which version this is, and whether it has been published.</param>
/// <param name="Roster">The roster itself, as the engine produced it.</param>
public sealed record StoredRoster(RosterVersion Version, RosterWeek Roster);
