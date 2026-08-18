using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Rostering;

/// <summary>
/// Topping a week up after the rules change, without disturbing it.
/// </summary>
/// <remarks>
/// The case this exists for, in the manager's words: a line has no leader, somebody is made
/// eligible to lead it, and the roster should say so without anybody pressing generate.
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class GapFillerTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task Making_somebody_eligible_puts_them_on_the_line_that_had_no_leader()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenALineWithNobodyLeadingItAsync(database);

        // The rule change the manager just made.
        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplaceLeaderEligibilityAsync(
                world.Zoe,
                [new LeaderEligibility { EmployeeId = world.Zoe, LineId = world.Ovens }]));

        var filled = await FillAsync(database);

        Assert.True(filled > 0);

        var leader = Assert.Single(
            await AssignmentsAsync(database),
            assignment => assignment.Role == AssignmentRole.LineLeader);

        Assert.Equal(world.Zoe, leader.EmployeeId);
        Assert.Equal(world.Ovens, leader.LineId);
    }

    /// <summary>
    /// The rules chose this, not the manager. Marking it manual would claim a decision
    /// nobody made, and locking it would stop a later generate arranging it better.
    /// </summary>
    [Fact]
    public async Task A_filled_place_is_the_engine_s_work_not_the_manager_s()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenALineWithNobodyLeadingItAsync(database);

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplaceLeaderEligibilityAsync(
                world.Zoe,
                [new LeaderEligibility { EmployeeId = world.Zoe, LineId = world.Ovens }]));

        await FillAsync(database);

        var leader = Assert.Single(
            await AssignmentsAsync(database),
            assignment => assignment.Role == AssignmentRole.LineLeader);

        Assert.Equal(AssignmentSource.Auto, leader.Source);
        Assert.False(leader.IsLocked);
    }

    /// <summary>
    /// Only ever adds. A week that has been reviewed and corrected must not be reshuffled
    /// because somebody edited one person's rules — that is what pressing generate is for.
    /// </summary>
    [Fact]
    public async Task Nobody_already_placed_is_moved()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenALineWithNobodyLeadingItAsync(database);

        var before = await AssignmentsAsync(database);

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplaceLeaderEligibilityAsync(
                world.Zoe,
                [new LeaderEligibility { EmployeeId = world.Zoe, LineId = world.Ovens }]));

        await FillAsync(database);

        var after = await AssignmentsAsync(database);

        // Everything that was there is still there, on the same line.
        foreach (var assignment in before)
        {
            Assert.Contains(
                after,
                a => a.EmployeeId == assignment.EmployeeId && a.LineId == assignment.LineId);
        }
    }

    /// <summary>
    /// Filling one hole by opening another is a decision for the manager, through a picker
    /// that names the line it would empty. It is not something to do behind their back.
    /// </summary>
    [Fact]
    public async Task Nobody_is_taken_off_another_line_to_fill_a_gap()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenALineWithNobodyLeadingItAsync(database);

        // Only somebody already standing on Packing may lead Ovens.
        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplaceLeaderEligibilityAsync(
                world.OnPacking,
                [new LeaderEligibility { EmployeeId = world.OnPacking, LineId = world.Ovens }]));

        await FillAsync(database);

        var onPacking = Assert.Single(
            await AssignmentsAsync(database),
            assignment => assignment.EmployeeId == world.OnPacking);

        Assert.Equal(world.Packing, onPacking.LineId);
    }

    [Fact]
    public async Task A_week_with_nothing_missing_is_left_alone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await GivenALineWithNobodyLeadingItAsync(database);

        // Nothing has changed, and nobody new is eligible for anything.
        var first = await FillAsync(database);
        var second = await FillAsync(database);

        Assert.Equal(0, second);
        Assert.True(first >= 0);
    }

    [Fact]
    public async Task A_closed_line_is_not_filled()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenALineWithNobodyLeadingItAsync(database);

        await database.InScopeAsync<ILineDayService>(service =>
            service.CloseAsync(world.Ovens, Monday));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.ReplaceLeaderEligibilityAsync(
                world.Zoe,
                [new LeaderEligibility { EmployeeId = world.Zoe, LineId = world.Ovens }]));

        await FillAsync(database);

        // A line that is not running is not short of anybody.
        Assert.DoesNotContain(
            await AssignmentsAsync(database),
            assignment => assignment.LineId == world.Ovens && assignment.EmployeeId == world.Zoe);
    }

    [Fact]
    public async Task A_week_that_does_not_exist_fills_nothing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        Assert.Equal(0, await FillAsync(database));
    }

    private static Task<int> FillAsync(TemporaryDatabase database) =>
        database.InScopeAsync<IGapFiller, int>(filler => filler.FillAsync(Monday));

    private static async Task<IReadOnlyList<Assignment>> AssignmentsAsync(TemporaryDatabase database)
    {
        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        return stored is null
            ? []
            : [.. stored.Roster.AllAssignments.Where(assignment => assignment.Date == Monday)];
    }

    /// <summary>
    /// Ovens needs two and has nobody who may lead it. Packing has somebody on it already.
    /// </summary>
    private static async Task<(Guid Ovens, Guid Packing, Guid Zoe, Guid OnPacking)>
        GivenALineWithNobodyLeadingItAsync(TemporaryDatabase database)
    {
        var ovens = Guid.NewGuid();
        var packing = Guid.NewGuid();

        foreach (var (id, name, order, headcount) in
            new[] { (ovens, "Ovens", 1, 2), (packing, "Packing", 2, 1) })
        {
            await database.InScopeAsync<IConfigurationRepository>(repository =>
                repository.SaveLineAsync(new ProductionLine
                {
                    Id = id,
                    Name = name,
                    RequiredHeadcount = headcount,
                    DisplayOrder = order,
                }));
        }

        var zoe = Guid.NewGuid();
        var onPacking = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = zoe, FullName = "Zoe Invented" }));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = onPacking, FullName = "Yusuf Fictional" }));

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.ReplaceImportedAsync(
                Monday,
                Monday.AddDays(7),
                [
                    new Availability { EmployeeId = zoe, Date = Monday, Status = AvailabilityStatus.Working },
                    new Availability { EmployeeId = onPacking, Date = Monday, Status = AvailabilityStatus.Working },
                ]));

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        // Put the board in a known state: Yusuf on Packing, Ovens empty and leaderless.
        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        var shiftId = stored!.Roster.Days.First(day => day.Date == Monday).ShiftId;

        var arranged = stored.Roster with
        {
            Days =
            [
                .. stored.Roster.Days.Select(day => day with
                {
                    Assignments = day.Date != Monday
                        ? []
                        : [
                            new Assignment
                            {
                                Date = Monday,
                                ShiftId = shiftId,
                                LineId = packing,
                                EmployeeId = onPacking,
                                Role = AssignmentRole.Worker,
                            },
                        ],
                }),
            ],
        };

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(Monday, arranged));

        return (ovens, packing, zoe, onPacking);
    }
}
