using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Rostering;

/// <summary>
/// Who is offered to fill a place, and in what order.
/// </summary>
/// <remarks>
/// The order is the feature. A list of names in alphabetical order is one the manager has to
/// think their way through at the worst possible moment, and the point of this is that the
/// first name on it is usually the right one.
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class ReplacementFinderTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task Somebody_required_on_the_line_is_offered_first()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await PreferAsync(database, world.Zoe, world.Ovens, rank: 1, PreferenceType.Preferred);
        await PreferAsync(database, world.Yusuf, world.Ovens, rank: 1, PreferenceType.Mandatory);

        var offered = await FindAsync(database, world.Ovens);

        Assert.Equal(world.Yusuf, offered[0].EmployeeId);
        Assert.True(offered[0].IsRequiredHere);
    }

    [Fact]
    public async Task A_higher_choice_is_offered_before_a_lower_one()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await PreferAsync(database, world.Zoe, world.Ovens, rank: 3, PreferenceType.Preferred);
        await PreferAsync(database, world.Yusuf, world.Ovens, rank: 1, PreferenceType.Preferred);

        var offered = await FindAsync(database, world.Ovens);

        Assert.Equal(world.Yusuf, offered[0].EmployeeId);
        Assert.Equal(1, offered[0].PreferenceRank);
    }

    /// <summary>
    /// Never alphabetical. Ada sorts first by name and last by preference, and preference is
    /// what decides.
    /// </summary>
    [Fact]
    public async Task The_list_is_not_in_name_order()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await PreferAsync(database, world.Zoe, world.Ovens, rank: 1, PreferenceType.Preferred);

        var offered = await FindAsync(database, world.Ovens);

        Assert.Equal("Zoe Invented", offered[0].DisplayName);
    }

    /// <summary>
    /// Somebody standing about costs nothing. Somebody already on a line costs that line.
    /// </summary>
    [Fact]
    public async Task Somebody_free_is_offered_before_somebody_already_on_a_line()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        // Yusuf is on Packing already, and he is this line's first choice. Zoe is free and
        // has no opinion. Free still wins.
        await PreferAsync(database, world.Yusuf, world.Ovens, rank: 1, PreferenceType.Preferred);
        await PlaceAsync(database, world.Yusuf, world.Packing);

        var offered = await FindAsync(database, world.Ovens);

        Assert.False(offered[0].LeavesAHoleElsewhere);
        Assert.Contains(offered, candidate => candidate.EmployeeId == world.Yusuf);
    }

    [Fact]
    public async Task Taking_somebody_off_another_line_says_which_line()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await PlaceAsync(database, world.Yusuf, world.Packing);

        var offered = await FindAsync(database, world.Ovens);
        var taken = Assert.Single(offered, candidate => candidate.EmployeeId == world.Yusuf);

        Assert.True(taken.LeavesAHoleElsewhere);
        Assert.Equal("Packing", taken.AlreadyOnLineName);
    }

    /// <summary>
    /// Absolute, exactly as in the engine. Not ranked low — not offered, because offering
    /// them invites the one click that puts somebody where they may not be.
    /// </summary>
    [Fact]
    public async Task Somebody_blocked_from_the_line_is_not_offered_at_all()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await PreferAsync(database, world.Zoe, world.Ovens, rank: 0, PreferenceType.Blocked);

        var offered = await FindAsync(database, world.Ovens);

        Assert.DoesNotContain(offered, candidate => candidate.EmployeeId == world.Zoe);
    }

    [Fact]
    public async Task Somebody_lacking_a_required_skill_is_not_offered()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        var skill = new Skill { Id = Guid.NewGuid(), Name = "Oven ticket" };

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveSkillAsync(skill));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveLineAsync(new ProductionLine
            {
                Id = world.Ovens,
                Name = "Ovens",
                RequiredHeadcount = 3,
                DisplayOrder = 1,
                RequiredSkillIds = new HashSet<Guid> { skill.Id },
            }));

        Assert.Empty(await FindAsync(database, world.Ovens));
    }

    [Fact]
    public async Task Somebody_who_is_off_that_day_is_not_offered()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.SetManualAsync(world.Zoe, Monday, AvailabilityStatus.Off));

        var offered = await FindAsync(database, world.Ovens);

        Assert.DoesNotContain(offered, candidate => candidate.EmployeeId == world.Zoe);
    }

    /// <summary>
    /// Both are available. One of them is being paid a premium, so at equal preference the
    /// cheaper answer is offered first.
    /// </summary>
    [Fact]
    public async Task Overtime_is_offered_below_plain_availability()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.SetManualAsync(world.Yusuf, Monday, AvailabilityStatus.Overtime));

        var offered = await FindAsync(database, world.Ovens);

        Assert.Equal(AvailabilityStatus.Working, offered[0].Status);
    }

    /// <summary>
    /// The rule the whole of phase 6 rests on: a decision made at seven survives the
    /// generate somebody presses at eight.
    /// </summary>
    [Fact]
    public async Task A_placement_is_locked_and_survives_regenerating()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await database.InScopeAsync<IAbsenceService>(service =>
            service.PlaceAsync(world.Zoe, world.Ovens, Monday));

        var placed = await AssignmentsAsync(database);
        var mine = Assert.Single(placed, a => a.EmployeeId == world.Zoe);

        Assert.True(mine.IsLocked);
        Assert.Equal(AssignmentSource.Manual, mine.Source);
        Assert.Equal(world.Ovens, mine.LineId);

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        var after = await AssignmentsAsync(database);

        Assert.Contains(after, a => a.EmployeeId == world.Zoe && a.LineId == world.Ovens && a.IsLocked);
    }

    /// <summary>
    /// One place per person per day. A manual placement that quietly broke it would put one
    /// name on two lines and the wall sheet would disagree with itself.
    /// </summary>
    [Fact]
    public async Task Placing_somebody_takes_them_off_the_line_they_were_on()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await PlaceAsync(database, world.Yusuf, world.Packing);

        await database.InScopeAsync<IAbsenceService>(service =>
            service.PlaceAsync(world.Yusuf, world.Ovens, Monday));

        var placed = (await AssignmentsAsync(database)).Where(a => a.EmployeeId == world.Yusuf).ToList();

        Assert.Single(placed);
        Assert.Equal(world.Ovens, placed[0].LineId);
    }

    private static Task<IReadOnlyList<ReplacementCandidate>> FindAsync(TemporaryDatabase database, Guid lineId) =>
        database.InScopeAsync<IReplacementFinder, IReadOnlyList<ReplacementCandidate>>(
            finder => finder.FindAsync(lineId, Monday));

    private static async Task<IReadOnlyList<Assignment>> AssignmentsAsync(TemporaryDatabase database)
    {
        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        return [.. stored!.Roster.AllAssignments.Where(assignment => assignment.Date == Monday)];
    }

    private static Task PlaceAsync(TemporaryDatabase database, Guid employeeId, Guid lineId) =>
        database.InScopeAsync<IAbsenceService>(service =>
            service.PlaceAsync(employeeId, lineId, Monday));

    private static Task PreferAsync(
        TemporaryDatabase database,
        Guid employeeId,
        Guid lineId,
        int rank,
        PreferenceType type) =>
        database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplacePreferencesAsync(
                employeeId,
                [new LinePreference { EmployeeId = employeeId, LineId = lineId, Rank = rank, Type = type }]));

    /// <summary>
    /// Two lines, two people who are in, and a generated week to place them into. The names
    /// sort in the opposite order to every ranking under test, so an alphabetical answer
    /// cannot pass by accident.
    /// </summary>
    private static async Task<(Guid Ovens, Guid Packing, Guid Zoe, Guid Yusuf)> GivenAFactoryAsync(
        TemporaryDatabase database)
    {
        var ovens = Guid.NewGuid();
        var packing = Guid.NewGuid();

        foreach (var (id, name, order) in new[] { (ovens, "Ovens", 1), (packing, "Packing", 2) })
        {
            await database.InScopeAsync<IConfigurationRepository>(repository =>
                repository.SaveLineAsync(new ProductionLine
                {
                    Id = id,
                    Name = name,
                    RequiredHeadcount = 3,
                    DisplayOrder = order,
                }));
        }

        var zoe = Guid.NewGuid();
        var yusuf = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = zoe, FullName = "Zoe Invented" }));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = yusuf, FullName = "Yusuf Fictional" }));

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.ReplaceImportedAsync(
                Monday,
                Monday.AddDays(7),
                [
                    new Availability { EmployeeId = zoe, Date = Monday, Status = AvailabilityStatus.Working },
                    new Availability { EmployeeId = yusuf, Date = Monday, Status = AvailabilityStatus.Working },
                ]));

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        // The generated week places both of them somewhere. Clear the day so each test
        // starts from a known board rather than from whatever the engine decided.
        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        var cleared = stored!.Roster with
        {
            Days = [.. stored.Roster.Days.Select(day => day with { Assignments = [] })],
        };

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(Monday, cleared));

        return (ovens, packing, zoe, yusuf);
    }
}
