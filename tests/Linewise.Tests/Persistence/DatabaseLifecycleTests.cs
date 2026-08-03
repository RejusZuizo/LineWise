using Linewise.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Linewise.Tests.Persistence;

public sealed class DatabaseLifecycleTests
{
    [Fact]
    public async Task MigrationsApplyCleanlyToAnEmptyDatabase()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await database.InScopeAsync<LinewiseDbContext>(async context =>
        {
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        });
    }

    [Fact]
    public async Task DeletingTheDatabaseAndStartingAgainRecreatesIt()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await database.InScopeAsync<LinewiseDbContext>(async context =>
        {
            context.Skills.Add(new Domain.Entities.Skill { Id = Guid.NewGuid(), Name = "Oven ticket" });
            await context.SaveChangesAsync();
        });

        SqliteConnection.ClearAllPools();
        File.Delete(database.DatabasePath);
        Assert.False(File.Exists(database.DatabasePath));

        await database.InitialiseAsync();

        await database.InScopeAsync<LinewiseDbContext>(async context =>
        {
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());

            // Recreated, and empty. Losing the file loses the data; it does not leave the
            // application unable to start, which is the failure mode that matters here.
            Assert.Empty(await context.Skills.ToListAsync());
        });
    }

    [Fact]
    public async Task TheDatabaseFileIsEncryptedAtRest()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        SqliteConnection.ClearAllPools();
        var header = new byte[16];
        await using (var stream = File.OpenRead(database.DatabasePath))
        {
            await stream.ReadExactlyAsync(header);
        }

        // A plain SQLite file opens with this in clear text. An encrypted one does not, and
        // this is the cheapest possible proof that SQLCipher is actually engaged rather than
        // silently falling back to an unencrypted provider.
        Assert.NotEqual("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task TheDatabaseCannotBeReadWithoutTheKey()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        SqliteConnection.ClearAllPools();

        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await using var connection = new SqliteConnection($"Data Source={database.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master";
            await command.ExecuteScalarAsync();
        });
    }
}
