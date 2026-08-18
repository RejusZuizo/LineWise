using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Persistence;

/// <summary>Who is in on which day, as read from the imported sheet.</summary>
public interface IAvailabilityRepository
{
    Task<IReadOnlyList<Availability>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces imported availability across a date range, leaving anything set by hand
    /// alone. An import is a statement about a whole week, so re-importing corrects the
    /// week rather than merging into it — but it is a statement about what the sheet says,
    /// and the manager marking somebody absent on Tuesday is not something the sheet knows
    /// about.
    /// </summary>
    /// <returns>
    /// The manual records it left in place, so the caller can say what it did not overwrite
    /// rather than leaving the operator to notice.
    /// </returns>
    Task<IReadOnlyList<Availability>> ReplaceImportedAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        IReadOnlyList<Availability> availabilities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one person's status on one date as the manager's own, replacing whatever
    /// was there. Stored as <see cref="AvailabilitySource.Manual"/> whatever the caller
    /// passes: this method is the promise, and a caller that could opt out of it would
    /// make the promise worthless.
    /// </summary>
    Task SetManualAsync(
        Guid employeeId,
        DateOnly date,
        AvailabilityStatus status,
        CancellationToken cancellationToken = default);
}
