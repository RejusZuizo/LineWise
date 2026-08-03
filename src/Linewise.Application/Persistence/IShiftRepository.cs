using Linewise.Domain.Entities;

namespace Linewise.Application.Persistence;

/// <summary>The shifts a roster is generated against.</summary>
public interface IShiftRepository
{
    Task<IReadOnlyList<Shift>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves any of these shifts that do not exist yet and returns the full set for the
    /// range, so a week can be rostered without the caller worrying whether it is the first
    /// time.
    /// </summary>
    Task<IReadOnlyList<Shift>> EnsureAsync(
        IReadOnlyList<Shift> shifts,
        CancellationToken cancellationToken = default);
}
