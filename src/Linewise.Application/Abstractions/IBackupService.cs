namespace Linewise.Application.Abstractions;

/// <summary>
/// Encrypted copies of the database, and the way back from one.
/// </summary>
/// <remarks>
/// A backup nobody has ever restored is a hope, not a backup, so restore is part of this
/// interface and part of the tests rather than an exercise for the worst possible day.
/// </remarks>
public interface IBackupService
{
    /// <summary>
    /// Takes a copy and prunes the oldest beyond the retention count. Returns the path
    /// written.
    /// </summary>
    Task<string> BackupAsync(CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<BackupDescriptor>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the live database with the named backup.</summary>
    Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default);
}

/// <param name="Path">Full path to the backup file.</param>
/// <param name="TakenAtUtc">When it was taken.</param>
/// <param name="SizeInBytes">How large it is, for the restore dialog.</param>
public sealed record BackupDescriptor(string Path, DateTime TakenAtUtc, long SizeInBytes);
