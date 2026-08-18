using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ILineDemandRepository"/>
public sealed class SqliteLineDemandRepository : ILineDemandRepository
{
    private readonly LinewiseDbContext _context;

    public SqliteLineDemandRepository(LinewiseDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<IReadOnlyList<LineDemand>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default) =>
        await _context.LineDemands
            .AsNoTracking()
            .Where(demand => demand.Date >= fromInclusive && demand.Date < toExclusive)
            .OrderBy(demand => demand.Date)
            .ThenBy(demand => demand.LineId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task ReplaceAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        IReadOnlyList<LineDemand> demands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demands);

        await _context.LineDemands
            .Where(demand => demand.Date >= fromInclusive && demand.Date < toExclusive)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        _context.Forget<LineDemand>(demand =>
            demand.Date >= fromInclusive && demand.Date < toExclusive);

        _context.LineDemands.AddRange(
            demands.Where(demand => demand.Date >= fromInclusive && demand.Date < toExclusive));

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync(LineDemand demand, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demand);

        var existing = await _context.LineDemands
            .FirstOrDefaultAsync(
                row => row.LineId == demand.LineId && row.Date == demand.Date,
                cancellationToken)
            .ConfigureAwait(false);

        // The record is keyed on line and date and is immutable, so changing one means
        // removing it and writing the replacement. ADR 0005.
        if (existing is not null)
        {
            _context.LineDemands.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _context.LineDemands.Add(demand);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
