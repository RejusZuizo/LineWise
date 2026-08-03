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

        _context.LineDemands.AddRange(
            demands.Where(demand => demand.Date >= fromInclusive && demand.Date < toExclusive));

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
