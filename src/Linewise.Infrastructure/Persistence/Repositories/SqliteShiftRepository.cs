using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IShiftRepository"/>
public sealed class SqliteShiftRepository : IShiftRepository
{
    private readonly LinewiseDbContext _context;

    public SqliteShiftRepository(LinewiseDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<IReadOnlyList<Shift>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default) =>
        await _context.Shifts
            .AsNoTracking()
            .Where(shift => shift.Date >= fromInclusive && shift.Date < toExclusive)
            .OrderBy(shift => shift.Date)
            .ThenBy(shift => shift.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Shift>> EnsureAsync(
        IReadOnlyList<Shift> shifts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        if (shifts.Count == 0)
        {
            return [];
        }

        var dates = shifts.Select(shift => shift.Date).ToHashSet();

        var existing = await _context.Shifts
            .Where(shift => dates.Contains(shift.Date))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Matched on date and shift name rather than on identifier, because a caller that
        // generates a week from scratch has no way of knowing the identifier used last time.
        var known = existing
            .Select(shift => (shift.Date, shift.Name))
            .ToHashSet();

        foreach (var shift in shifts.Where(shift => !known.Contains((shift.Date, shift.Name))))
        {
            _context.Shifts.Add(shift);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var from = dates.Min();
        var to = dates.Max().AddDays(1);

        return await GetAsync(from, to, cancellationToken).ConfigureAwait(false);
    }
}
