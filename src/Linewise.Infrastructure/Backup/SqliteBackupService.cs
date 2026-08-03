using System.Globalization;
using Linewise.Application.Abstractions;
using Linewise.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Linewise.Infrastructure.Backup;

/// <inheritdoc cref="IBackupService"/>
public sealed class SqliteBackupService : IBackupService
{
    private const string FilePrefix = "linewise-";
    private const string FileExtension = ".db";

    private readonly LinewiseDatabaseOptions _options;
    private readonly IDatabaseKeyProvider _keyProvider;
    private readonly IClock _clock;

    public SqliteBackupService(
        IOptions<LinewiseDatabaseOptions> options,
        IDatabaseKeyProvider keyProvider,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(keyProvider);
        ArgumentNullException.ThrowIfNull(clock);

        _options = options.Value;
        _keyProvider = keyProvider;
        _clock = clock;
    }

    public async Task<string> BackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.ResolvedBackupDirectory);

        var target = Path.Combine(
            _options.ResolvedBackupDirectory,
            FilePrefix + _clock.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + FileExtension);

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // VACUUM INTO, never File.Copy. Copying a live database captures it mid write and
        // leaves the write ahead log behind, which produces a file that looks fine until the
        // day somebody needs it. The copy inherits the encryption key.
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $target";
        command.Parameters.AddWithValue("$target", target);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        Prune();

        return target;
    }

    public Task<IReadOnlyList<BackupDescriptor>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ListBackups());

    public async Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Backup file not found.", backupPath);
        }

        // Prove the backup opens and reads with the current key before overwriting anything.
        // Restoring an unreadable file over a working database turns a bad day into a
        // catastrophe, and the check costs one query.
        await EnsureReadableAsync(backupPath, cancellationToken).ConfigureAwait(false);

        // Pooled connections hold file handles open, and on Windows the copy fails or, worse,
        // a later write from a stale pooled handle lands on the restored file.
        SqliteConnection.ClearAllPools();

        var databasePath = _options.ResolvedDatabasePath;
        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(backupPath, databasePath, overwrite: true);

        // The write ahead log and shared memory files belong to the database being replaced.
        // Leaving them would let SQLite replay changes from a database that no longer exists.
        DeleteIfPresent(databasePath + "-wal");
        DeleteIfPresent(databasePath + "-shm");
    }

    private string ConnectionString =>
        LinewiseConnection.BuildConnectionString(_options.ResolvedDatabasePath, _keyProvider.GetKey());

    private async Task EnsureReadableAsync(string backupPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            LinewiseConnection.BuildConnectionString(backupPath, _keyProvider.GetKey()));

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master";
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<BackupDescriptor> ListBackups()
    {
        if (!Directory.Exists(_options.ResolvedBackupDirectory))
        {
            return [];
        }

        return new DirectoryInfo(_options.ResolvedBackupDirectory)
            .GetFiles(FilePrefix + "*" + FileExtension)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new BackupDescriptor(file.FullName, file.LastWriteTimeUtc, file.Length))
            .ToList();
    }

    private void Prune()
    {
        foreach (var stale in ListBackups().Skip(Math.Max(1, _options.BackupsToKeep)))
        {
            DeleteIfPresent(stale.Path);
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
