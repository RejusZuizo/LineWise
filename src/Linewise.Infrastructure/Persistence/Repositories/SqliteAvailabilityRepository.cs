using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IAvailabilityRepository"/>
public sealed class SqliteAvailabilityRepository : IAvailabilityRepository
{
    private readonly LinewiseDbContext _context;

    public SqliteAvailabilityRepository(LinewiseDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<IReadOnlyList<Availability>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default) =>
        await _context.Availabilities
            .AsNoTracking()
            .Where(availability => availability.Date >= fromInclusive && availability.Date < toExclusive)
            .OrderBy(availability => availability.Date)
            .ThenBy(availability => availability.EmployeeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task ReplaceAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        IReadOnlyList<Availability> availabilities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availabilities);

        await _context.Availabilities
            .Where(availability => availability.Date >= fromInclusive && availability.Date < toExclusive)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        _context.Availabilities.AddRange(
            availabilities.Where(availability =>
                availability.Date >= fromInclusive && availability.Date < toExclusive));

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
