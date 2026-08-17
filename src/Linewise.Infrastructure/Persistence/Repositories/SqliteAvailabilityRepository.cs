using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
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

    public async Task<IReadOnlyList<Availability>> ReplaceImportedAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        IReadOnlyList<Availability> availabilities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availabilities);

        // Read the manual records first. They are the ones this must not touch, and they
        // are also what the caller is told about on the way out.
        var kept = await _context.Availabilities
            .AsNoTracking()
            .Where(availability => availability.Date >= fromInclusive
                && availability.Date < toExclusive
                && availability.Source == AvailabilitySource.Manual)
            .OrderBy(availability => availability.Date)
            .ThenBy(availability => availability.EmployeeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await _context.Availabilities
            .Where(availability => availability.Date >= fromInclusive
                && availability.Date < toExclusive
                && availability.Source == AvailabilitySource.Imported)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var manual = kept
            .Select(availability => (availability.EmployeeId, availability.Date))
            .ToHashSet();

        _context.Availabilities.AddRange(
            availabilities
                .Where(availability =>
                    availability.Date >= fromInclusive && availability.Date < toExclusive)
                .Where(availability => !manual.Contains((availability.EmployeeId, availability.Date)))
                .Select(availability => availability with { Source = AvailabilitySource.Imported }));

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return kept;
    }

    public async Task SetManualAsync(
        Guid employeeId,
        DateOnly date,
        AvailabilityStatus status,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Availabilities
            .FirstOrDefaultAsync(
                availability => availability.EmployeeId == employeeId && availability.Date == date,
                cancellationToken)
            .ConfigureAwait(false);

        // The record is keyed on employee and date, and a record is immutable, so changing
        // one means removing it and writing the replacement rather than assigning to a
        // property. ADR 0005.
        if (existing is not null)
        {
            _context.Availabilities.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _context.Availabilities.Add(new Availability
        {
            EmployeeId = employeeId,
            Date = date,
            Status = status,
            Source = AvailabilitySource.Manual,
        });

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
