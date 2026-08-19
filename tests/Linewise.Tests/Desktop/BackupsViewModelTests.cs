using Linewise.Application.Abstractions;
using Linewise.Desktop.ViewModels;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// The backup list and the way back from one.
/// </summary>
/// <remarks>
/// Restore is the only genuinely destructive thing in the application, so what matters here
/// is that it is deliberate, that a failure leaves the database alone, and that the screen
/// says the application has to be restarted afterwards.
/// </remarks>
public sealed class BackupsViewModelTests
{
    [Fact]
    public async Task Backups_are_listed_newest_first_as_given()
    {
        var backups = Build(
            new BackupDescriptor("/b/2.db", new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc), 2048),
            new BackupDescriptor("/b/1.db", new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc), 1024));

        await backups.LoadAsync();

        Assert.True(backups.HasBackups);
        Assert.Equal(["/b/2.db", "/b/1.db"], backups.Backups.Select(backup => backup.Path));
    }

    [Fact]
    public async Task An_empty_backup_folder_says_so_rather_than_looking_broken()
    {
        var backups = Build();

        await backups.LoadAsync();

        Assert.False(backups.HasBackups);
        Assert.Empty(backups.Backups);
    }

    [Fact]
    public async Task Taking_a_backup_refreshes_the_list()
    {
        var taken = 0;

        var backups = new BackupsViewModel(
            () => Task.FromResult<IReadOnlyList<BackupDescriptor>>(
                [.. Enumerable.Range(0, taken).Select(i =>
                    new BackupDescriptor($"/b/{i}.db", DateTime.UnixEpoch, 1024))]),
            () =>
            {
                taken++;
                return Task.FromResult("/b/new.db");
            },
            _ => Task.CompletedTask);

        await backups.BackUpNowCommand.ExecuteAsync(null);

        Assert.Single(backups.Backups);
    }

    /// <summary>
    /// The context was opened against the file that has just been replaced. Carrying on with
    /// it would write the old data back over the restored copy.
    /// </summary>
    [Fact]
    public async Task Restoring_says_the_application_must_be_restarted()
    {
        string? restored = null;

        var backups = new BackupsViewModel(
            () => Task.FromResult<IReadOnlyList<BackupDescriptor>>(
                [new BackupDescriptor("/b/1.db", DateTime.UnixEpoch, 1024)]),
            () => Task.FromResult("/b/1.db"),
            path =>
            {
                restored = path;
                return Task.CompletedTask;
            });

        await backups.LoadAsync();
        await backups.Backups[0].RestoreCommand.ExecuteAsync(null);

        Assert.Equal("/b/1.db", restored);
        Assert.True(backups.NeedsRestart);
    }

    /// <summary>
    /// The service checks the backup opens before it overwrites anything, so a failure means
    /// the live database was never touched. The screen must not claim a restart is needed.
    /// </summary>
    [Fact]
    public async Task A_failed_restore_does_not_claim_anything_was_replaced()
    {
        var backups = new BackupsViewModel(
            () => Task.FromResult<IReadOnlyList<BackupDescriptor>>(
                [new BackupDescriptor("/b/1.db", DateTime.UnixEpoch, 1024)]),
            () => Task.FromResult("/b/1.db"),
            _ => throw new FileNotFoundException("Backup file not found."));

        await backups.LoadAsync();
        await backups.Backups[0].RestoreCommand.ExecuteAsync(null);

        Assert.False(backups.NeedsRestart);
        Assert.NotEmpty(backups.Status);
    }

    private static BackupsViewModel Build(params BackupDescriptor[] backups) =>
        new(() => Task.FromResult<IReadOnlyList<BackupDescriptor>>(backups),
            () => Task.FromResult("/b/new.db"),
            _ => Task.CompletedTask);
}
