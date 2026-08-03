using Linewise.Domain.Entities;

namespace Linewise.Application.Persistence;

/// <summary>Who is in on which day, as read from the imported sheet.</summary>
public interface IAvailabilityRepository
{
    Task<IReadOnlyList<Availability>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces availability across a date range. An import is a statement about a whole
    /// week, so re-importing corrects the week rather than merging into it.
    /// </summary>
    Task ReplaceAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        IReadOnlyList<Availability> availabilities,
        CancellationToken cancellationToken = default);
}
