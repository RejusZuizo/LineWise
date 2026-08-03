using Linewise.Application.Abstractions;
using Linewise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Linewise.Infrastructure;

/// <summary>
/// Brings the database up to date at startup, and takes a copy first.
/// </summary>
public sealed class DatabaseInitialiser
{
    private readonly LinewiseDbContext _context;
    private readonly IBackupService _backupService;
    private readonly LinewiseDatabaseOptions _options;

    public DatabaseInitialiser(
        LinewiseDbContext context,
        IBackupService backupService,
        IOptions<LinewiseDatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(backupService);
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _backupService = backupService;
        _options = options.Value;
    }

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        // Backup first, then migrate. The other way round leaves no way back from a
        // migration that goes wrong, which is the one moment a backup is most wanted.
        // Skipped on first run, when there is nothing yet to copy.
        if (_options.BackupOnStartup && File.Exists(_options.ResolvedDatabasePath))
        {
            await _backupService.BackupAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_options.MigrateOnStartup)
        {
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
