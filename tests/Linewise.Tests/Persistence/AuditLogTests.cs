using Linewise.Application.Abstractions;
using Linewise.Domain.Auditing;
using Linewise.Domain.Enums;
using Linewise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Linewise.Tests.Persistence;

[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class AuditLogTests
{
    [Fact]
    public async Task AnIntactChainVerifies()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        var verification = await database.InScopeAsync<IAuditLog, AuditChainVerification>(
            log => log.VerifyAsync());

        Assert.True(verification.IsIntact);
        Assert.Null(verification.FirstBrokenSequence);
    }

    [Fact]
    public async Task EachEntryPointsAtTheOneBeforeIt()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        var entries = await database.InScopeAsync<IAuditLog, IReadOnlyList<AuditEntry>>(
            log => log.ReadAllAsync());

        Assert.Equal(3, entries.Count);
        Assert.Equal(AuditChain.GenesisHash, entries[0].PreviousHash);
        Assert.Equal(entries[0].Hash, entries[1].PreviousHash);
        Assert.Equal(entries[1].Hash, entries[2].PreviousHash);
        Assert.Equal("test.operator", entries[0].UserName);
    }

    [Fact]
    public async Task AnEditedEntryIsDetected()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        // Straight past the application, the way somebody with a SQLite browser and the key
        // would do it. The chain, not the code, is what catches this.
        await database.InScopeAsync<LinewiseDbContext>(context =>
            context.Database.ExecuteSqlRawAsync(
                "UPDATE AuditEntries SET Summary = 'Nothing to see here' WHERE Sequence = 2"));

        var verification = await database.InScopeAsync<IAuditLog, AuditChainVerification>(
            log => log.VerifyAsync());

        Assert.False(verification.IsIntact);
        Assert.Equal(AuditChainBreak.HashMismatch, verification.Break);
        Assert.Equal(2, verification.FirstBrokenSequence);
    }

    [Fact]
    public async Task ARemovedEntryIsDetected()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        await database.InScopeAsync<LinewiseDbContext>(context =>
            context.Database.ExecuteSqlRawAsync("DELETE FROM AuditEntries WHERE Sequence = 2"));

        var verification = await database.InScopeAsync<IAuditLog, AuditChainVerification>(
            log => log.VerifyAsync());

        Assert.False(verification.IsIntact);
        Assert.Equal(AuditChainBreak.BrokenLink, verification.Break);

        // The third entry still points at the second, which is no longer there.
        Assert.Equal(3, verification.FirstBrokenSequence);
    }

    [Fact]
    public async Task TheLogRefusesToBeEditedThroughTheApplication()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        await database.InScopeAsync<LinewiseDbContext>(async context =>
        {
            var entry = await context.AuditEntries.OrderBy(candidate => candidate.Sequence).FirstAsync();
            context.Entry(entry).Property(candidate => candidate.Summary).CurrentValue = "Rewritten";

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task TheLogRefusesToBeDeletedThroughTheApplication()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        await database.InScopeAsync<LinewiseDbContext>(async context =>
        {
            var entry = await context.AuditEntries.OrderBy(candidate => candidate.Sequence).FirstAsync();
            context.AuditEntries.Remove(entry);

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task TimestampsComeBackAsUtc()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AppendThreeAsync(database);

        var entries = await database.InScopeAsync<IAuditLog, IReadOnlyList<AuditEntry>>(
            log => log.ReadAllAsync());

        // SQLite has no time zone concept and hands back an Unspecified kind. Without the
        // converter putting it back, the next conversion would shift every timestamp by the
        // local offset and the hashes would stop matching.
        Assert.All(entries, entry => Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind));
    }

    private static async Task AppendThreeAsync(TemporaryDatabase database)
    {
        await database.InScopeAsync<IAuditLog>(async log =>
        {
            await log.AppendAsync(AuditAction.ConfigurationChanged, "Line 1 headcount set to 4.", "New contract.");
            database.Clock.Advance(TimeSpan.FromMinutes(5));

            await log.AppendAsync(AuditAction.RosterGenerated, "Roster generated for week 2026-08-03.", string.Empty);
            database.Clock.Advance(TimeSpan.FromMinutes(5));

            await log.AppendAsync(AuditAction.RosterPublished, "Roster version 1 published.", "Signed off.");
        });
    }
}
