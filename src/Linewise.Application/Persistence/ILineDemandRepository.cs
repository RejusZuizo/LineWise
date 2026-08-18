using Linewise.Domain.Entities;

namespace Linewise.Application.Persistence;

/// <summary>
/// How busy each line is on each day.
/// </summary>
/// <remarks>
/// Kept apart from the line itself because it is a property of a date rather than of the
/// line, and apart from availability because it is about the work rather than the people.
/// </remarks>
public interface ILineDemandRepository
{
    Task<IReadOnlyList<LineDemand>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the demands across a date range. Setting a week's demand is one decision
    /// about that week, so it is stored as one.
    /// </summary>
    Task ReplaceAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        IReadOnlyList<LineDemand> demands,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one line's entry for one date, replacing whatever was there. Closing a line
    /// for a day is a decision about that day alone, and rewriting the week around it would
    /// take the rest of the week's demands with it.
    /// </summary>
    Task SetAsync(LineDemand demand, CancellationToken cancellationToken = default);
}
