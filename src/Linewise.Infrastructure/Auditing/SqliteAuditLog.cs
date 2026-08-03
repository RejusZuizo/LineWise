using Linewise.Application.Abstractions;
using Linewise.Domain.Auditing;
using Linewise.Domain.Enums;
using Linewise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Auditing;

/// <inheritdoc cref="IAuditLog"/>
public sealed class SqliteAuditLog : IAuditLog
{
    private readonly LinewiseDbContext _context;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public SqliteAuditLog(LinewiseDbContext context, IClock clock, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        _context = context;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<AuditEntry> AppendAsync(
        AuditAction action,
        string summary,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var previousHash = await _context.AuditEntries
            .OrderByDescending(entry => entry.Sequence)
            .Select(entry => entry.Hash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? AuditChain.GenesisHash;

        var entry = AuditChain.Link(
            _clock.UtcNow,
            _currentUser.Name,
            action,
            summary,
            reason ?? string.Empty,
            previousHash);

        _context.AuditEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry;
    }

    public async Task<IReadOnlyList<AuditEntry>> ReadAllAsync(CancellationToken cancellationToken = default) =>
        await _context.AuditEntries
            .AsNoTracking()
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default) =>
        AuditChain.Verify(await ReadAllAsync(cancellationToken).ConfigureAwait(false));
}
