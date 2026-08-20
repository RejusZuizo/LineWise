using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Rostering;

/// <summary>
/// Adding people and taking them off the roster.
/// </summary>
/// <remarks>
/// Until now people could only arrive through the availability sheet, so somebody hired on a
/// Tuesday could not be entered and a leaver could not be taken out.
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class EmployeeDirectoryTests
{
    [Fact]
    public async Task Somebody_added_by_hand_is_on_the_roster()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var added = await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("Ada Fictional", isTemporary: false));

        var stored = Assert.Single((await ConfigurationAsync(database)).Employees);

        Assert.Equal(added.Id, stored.Id);
        Assert.True(stored.IsActive);
        Assert.False(stored.IsTemporary);
    }

    [Fact]
    public async Task Agency_staff_are_marked_as_such()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("Bram Invented", isTemporary: true));

        Assert.True(Assert.Single((await ConfigurationAsync(database)).Employees).IsTemporary);
    }

    [Fact]
    public async Task A_name_is_trimmed_on_the_way_in()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("  Cleo Notreal  ", isTemporary: false));

        Assert.Equal("Cleo Notreal", Assert.Single((await ConfigurationAsync(database)).Employees).FullName);
    }

    /// <summary>
    /// Never a hard delete. Historic rosters name people by identifier, and removing the row
    /// would turn every week they ever worked into a sheet full of unknowns.
    /// </summary>
    [Fact]
    public async Task A_leaver_is_kept_and_only_stops_being_active()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var added = await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("Ada Fictional", isTemporary: false));

        await database.InScopeAsync<IEmployeeDirectory>(
            directory => directory.DeactivateAsync(added.Id));

        var stored = Assert.Single((await ConfigurationAsync(database)).Employees);

        Assert.Equal(added.Id, stored.Id);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task A_leaver_who_returns_is_put_back_rather_than_entered_again()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var added = await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("Ada Fictional", isTemporary: false));

        await database.InScopeAsync<IEmployeeDirectory>(
            directory => directory.DeactivateAsync(added.Id));

        await database.InScopeAsync<IEmployeeDirectory>(
            directory => directory.ReactivateAsync(added.Id));

        var stored = Assert.Single((await ConfigurationAsync(database)).Employees);

        Assert.True(stored.IsActive);
    }

    /// <summary>
    /// Everything else about them survives. Deactivating is not a way of clearing somebody's
    /// rules by the back door.
    /// </summary>
    [Fact]
    public async Task Deactivating_keeps_their_rules_and_their_aliases()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var id = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee
            {
                Id = id,
                FullName = "Ada Fictional",
                Aliases = ["A. Fictional"],
            }));

        var line = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveLineAsync(new ProductionLine { Id = line, Name = "Ovens", RequiredHeadcount = 2 }));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplacePreferencesAsync(
                id,
                [new LinePreference { EmployeeId = id, LineId = line, Rank = 1, Type = PreferenceType.Preferred }]));

        await database.InScopeAsync<IEmployeeDirectory>(directory => directory.DeactivateAsync(id));

        var configuration = await ConfigurationAsync(database);
        var stored = Assert.Single(configuration.Employees);

        Assert.False(stored.IsActive);
        Assert.Contains("A. Fictional", stored.Aliases);
        Assert.Single(configuration.Preferences);
    }

    [Fact]
    public async Task Deactivating_somebody_already_gone_does_nothing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var added = await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("Ada Fictional", isTemporary: false));

        await database.InScopeAsync<IEmployeeDirectory>(d => d.DeactivateAsync(added.Id));
        await database.InScopeAsync<IEmployeeDirectory>(d => d.DeactivateAsync(added.Id));

        var entries = await database.InScopeAsync<IAuditLog, IReadOnlyList<AuditAction>>(
            async log => [.. (await log.ReadAllAsync()).Select(entry => entry.Action)]);

        // One deactivation, not two. An audit chain that records a change nobody made is one
        // nobody can rely on.
        Assert.Single(entries, action => action == AuditAction.EmployeeDeactivated);
    }

    [Fact]
    public async Task Adding_and_leaving_are_both_written_to_the_audit_chain()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var added = await database.InScopeAsync<IEmployeeDirectory, Employee>(
            directory => directory.AddAsync("Ada Fictional", isTemporary: false));

        await database.InScopeAsync<IEmployeeDirectory>(d => d.DeactivateAsync(added.Id));

        var entries = await database.InScopeAsync<IAuditLog, IReadOnlyList<(AuditAction Action, string Summary)>>(
            async log => [.. (await log.ReadAllAsync()).Select(entry => (entry.Action, entry.Summary))]);

        Assert.Contains(entries, entry => entry.Action == AuditAction.EmployeeAdded);
        Assert.Contains(entries, entry => entry.Action == AuditAction.EmployeeDeactivated);

        // Identifiers, never names.
        Assert.All(entries, entry =>
            Assert.DoesNotContain("Ada", entry.Summary, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<Application.Rostering.RosterConfiguration> ConfigurationAsync(
        TemporaryDatabase database) =>
        database.InScopeAsync<IConfigurationRepository, Application.Rostering.RosterConfiguration>(
            repository => repository.GetAsync());
}
