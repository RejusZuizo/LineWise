using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Linewise.Tests.Persistence;

public sealed class BackupRestoreTests
{
    [Fact]
    public async Task ABackupRestoresToAWorkingDatabase()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var skillId = Guid.NewGuid();
        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveSkillAsync(new Skill { Id = skillId, Name = "Oven ticket" }));

        var backupPath = await database.InScopeAsync<IBackupService, string>(
            service => service.BackupAsync());

        Assert.True(File.Exists(backupPath));

        // Something happens that the operator wants undone.
        await database.InScopeAsync<LinewiseDbContext>(context =>
            context.Database.ExecuteSqlRawAsync("DELETE FROM Skills"));

        await database.InScopeAsync<IBackupService>(service => service.RestoreAsync(backupPath));

        var skills = await database.InScopeAsync<IConfigurationRepository, Application.Rostering.RosterConfiguration>(
            repository => repository.GetAsync());

        var restored = Assert.Single(skills.Skills);
        Assert.Equal(skillId, restored.Id);
        Assert.Equal("Oven ticket", restored.Name);
    }

    [Fact]
    public async Task ARestoredDatabaseIsStillEncryptedAndStillMigrated()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var backupPath = await database.InScopeAsync<IBackupService, string>(
            service => service.BackupAsync());

        await database.InScopeAsync<IBackupService>(service => service.RestoreAsync(backupPath));

        // The copy inherits the key, so the restored file has to be unreadable without it
        // and complete enough to carry on working against.
        var header = new byte[16];
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await using (var stream = File.OpenRead(database.DatabasePath))
        {
            await stream.ReadExactlyAsync(header);
        }

        Assert.NotEqual("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(header));

        await database.InScopeAsync<LinewiseDbContext>(async context =>
            Assert.Empty(await context.Database.GetPendingMigrationsAsync()));
    }

    [Fact]
    public async Task OnlyTheRetainedNumberOfBackupsIsKept()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        for (var i = 0; i < 13; i++)
        {
            await database.InScopeAsync<IBackupService>(service => service.BackupAsync());
            database.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var backups = await database.InScopeAsync<IBackupService, IReadOnlyList<BackupDescriptor>>(
            service => service.ListAsync());

        Assert.Equal(10, backups.Count);
    }

    [Fact]
    public async Task RestoringSomethingThatIsNotAReadableBackupIsRefused()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var rubbish = Path.Combine(database.Root, "not-a-backup.db");
        await File.WriteAllTextAsync(rubbish, "this is not a database");

        // Overwriting a working database with an unreadable file turns a bad day into a
        // catastrophe, so the restore proves it can read the backup first.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            database.InScopeAsync<IBackupService>(service => service.RestoreAsync(rubbish)));

        await database.InScopeAsync<LinewiseDbContext>(async context =>
            Assert.Empty(await context.Database.GetPendingMigrationsAsync()));
    }

    [Fact]
    public async Task AMissingBackupFileIsReported()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            database.InScopeAsync<IBackupService>(service =>
                service.RestoreAsync(Path.Combine(database.Root, "nothing-here.db"))));
    }
}
